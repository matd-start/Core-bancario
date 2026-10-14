using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using CoreBancario.Infrastructure.Identidad;
using CoreBancario.Infrastructure.Persistencia;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CoreBancario.Keycloak.Tests.KeycloakFixture))]

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// ADR-0018, niveles "adaptador" y "punta a punta": UN Keycloak real (la misma imagen que docker-compose.yml) que importa
/// LOS REALMS DE PRODUCCION de infra/keycloak/realms y monta sus temas, un PostgreSQL y la API real apuntando a ese
/// Keycloak. Tras arrancar, usa el administrador de master para bajar los tiempos (inactividad y espera del bloqueo) y
/// registrar la redirect_uri de las pruebas: no hay una segunda copia del realm que pueda desincronizarse.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    public const string Imagen = "quay.io/keycloak/keycloak:26.3.3";
    public const string RealmDeClientes = "clientes";
    public const string RealmDeEmpleados = "empleados";

    /// <summary>Inactividad de las sesiones durante las pruebas (en produccion: 5 y 15 min, RN-22).</summary>
    public static readonly TimeSpan InactividadEnLasPruebas = TimeSpan.FromSeconds(40);

    /// <summary>Espera del bloqueo durante las pruebas (en produccion: 15 min, RN-23).</summary>
    public static readonly TimeSpan EsperaDelBloqueoEnLasPruebas = TimeSpan.FromSeconds(10);

    private const string AdminDeMaster = "admin-de-pruebas";
    private const string ClaveDelAdminDeMaster = "Clave-del-admin-de-pruebas-1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private KeycloakContainer? _keycloak;

    public string UrlDeKeycloak { get; private set; } = "";

    /// <summary>
    /// Secreto de la cuenta de servicio core-bancario-api-admin (003b). Es aleatorio en cada ejecucion: el realm lo recibe
    /// por la variable KC_CLIENTES_API_SECRET y la API de las pruebas, por Keycloak:Administracion:ClientSecret.
    /// </summary>
    public string SecretoDeServicio { get; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    public AdministracionDeKeycloak Admin { get; private set; } = null!;

    public WebApplicationFactory<Program> Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // El primer arranque de Keycloak tarda minutos (unos 188 s en el experimento): el tiempo de espera por defecto no basta.
        TestcontainersSettings.WaitStrategyTimeout = TimeSpan.FromMinutes(10);

        var keycloak = ConstruirKeycloak();
        _keycloak = keycloak;
        await Task.WhenAll(keycloak.StartAsync(), _postgres.StartAsync());
        UrlDeKeycloak = keycloak.GetBaseAddress().TrimEnd('/');

        await MigrarLaBaseAsync();

        Admin = new AdministracionDeKeycloak(UrlDeKeycloak, AdminDeMaster, ClaveDelAdminDeMaster);
        foreach (var realm in new[] { RealmDeClientes, RealmDeEmpleados })
        {
            await Admin.ActualizarRealmAsync(realm, new JsonObject
            {
                ["ssoSessionIdleTimeout"] = (int)InactividadEnLasPruebas.TotalSeconds,
                ["waitIncrementSeconds"] = (int)EsperaDelBloqueoEnLasPruebas.TotalSeconds,
                ["maxFailureWaitSeconds"] = (int)EsperaDelBloqueoEnLasPruebas.TotalSeconds,
                ["quickLoginCheckMilliSeconds"] = 1,
                ["minimumQuickLoginWaitSeconds"] = 1,
            });
            await Admin.RegistrarUriDeRedireccionAsync(realm, NavegadorSimulado.ClientId, NavegadorSimulado.UriDeRedireccion);
        }

        Api = new WebApplicationFactory<Program>().WithWebHostBuilder(constructor =>
        {
            constructor.UseSetting("ConnectionStrings:CoreBancario", _postgres.GetConnectionString());
            constructor.UseSetting("Autenticacion:Audiencia", "core-bancario-api");
            constructor.UseSetting("Autenticacion:Clientes:Emisor", $"{UrlDeKeycloak}/realms/{RealmDeClientes}");
            constructor.UseSetting("Autenticacion:Empleados:Emisor", $"{UrlDeKeycloak}/realms/{RealmDeEmpleados}");
            // 003b: la API real gestiona accesos con su cuenta de servicio contra este Keycloak.
            constructor.UseSetting("Keycloak:Administracion:UrlBase", UrlDeKeycloak);
            constructor.UseSetting("Keycloak:Administracion:Realm", RealmDeClientes);
            constructor.UseSetting("Keycloak:Administracion:ClientId", "core-bancario-api-admin");
            constructor.UseSetting("Keycloak:Administracion:ClientSecret", SecretoDeServicio);
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Api is not null)
            await Api.DisposeAsync();

        Admin?.Dispose();

        if (_keycloak is not null)
            await _keycloak.DisposeAsync();

        await _postgres.DisposeAsync();
    }

    private KeycloakContainer ConstruirKeycloak()
    {
        var infra = Path.Combine(Ayudas.RaizDelRepositorio(), "infra", "keycloak");
        var realms = new DirectoryInfo(Path.Combine(infra, "realms"));
        var temaBase = new DirectoryInfo(Path.Combine(infra, "themes", "core-bancario"));
        var temaDeClientes = new DirectoryInfo(Path.Combine(infra, "themes", "core-bancario-clientes"));

        // Se copian (no se montan) para que funcione igual con un Docker remoto. Si no existen, falla aqui con un
        // mensaje claro: son los archivos de las tareas T05 y T06.
        foreach (var directorio in new[] { realms, temaBase, temaDeClientes })
        {
            if (!directorio.Exists)
                throw new DirectoryNotFoundException($"Falta {directorio.FullName} (tareas T05 y T06 del plan de la 003).");
        }

        return new KeycloakBuilder(Imagen)
            .WithUsername(AdminDeMaster)
            .WithPassword(ClaveDelAdminDeMaster)
            // Los realms usan el marcador ${KC_CLIENTES_API_SECRET} (003b): Keycloak lo resuelve con esta variable.
            .WithEnvironment("KC_CLIENTES_API_SECRET", SecretoDeServicio)
            .WithResourceMapping(realms, "/opt/keycloak/data/import")
            .WithResourceMapping(temaBase, "/opt/keycloak/themes/core-bancario")
            .WithResourceMapping(temaDeClientes, "/opt/keycloak/themes/core-bancario-clientes")
            .WithCommand("--import-realm")
            .Build();
    }

    private async Task MigrarLaBaseAsync()
    {
        await using var db = CrearContexto();
        await db.Database.MigrateAsync();
    }

    private CoreBancarioDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<CoreBancarioDbContext>();
        opciones.UsarPostgres(_postgres.GetConnectionString());
        return new CoreBancarioDbContext(opciones.Options);
    }

    /// <summary>Un navegador nuevo (cookies propias) apuntando a este Keycloak.</summary>
    public NavegadorSimulado NuevoNavegador() => new(UrlDeKeycloak);

    /// <summary>Un cliente HTTP contra la API real, con el access token indicado (o sin credencial si es null).</summary>
    public HttpClient CrearClienteDeApi(string? accessToken = null)
    {
        var http = Api.CreateClient();
        if (accessToken is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return http;
    }

    /// <summary>
    /// El adaptador real (003b) contra este Keycloak, con la cuenta de servicio y el secreto del fixture: la misma cadena
    /// HttpClient - TokenDeServicioHandler que arma AddInfraestructura.
    /// </summary>
    public ProveedorDeIdentidadKeycloak CrearProveedorDeIdentidad()
    {
        var opciones = Options.Create(new OpcionesDeKeycloak
        {
            UrlBase = new Uri(UrlDeKeycloak),
            Realm = RealmDeClientes,
            ClientId = "core-bancario-api-admin",
            ClientSecret = SecretoDeServicio,
        });
        var token = new TokenDeServicioHandler(new FabricaDeClientesSimple(), opciones, TimeProvider.System)
        {
            InnerHandler = new HttpClientHandler(),
        };
        var http = new HttpClient(token) { BaseAddress = new Uri(UrlDeKeycloak), Timeout = opciones.Value.TiempoDeEspera };
        return new ProveedorDeIdentidadKeycloak(http, opciones);
    }

    private sealed class FabricaDeClientesSimple : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    // ---- Usuarios de prueba (plan, seccion 1.1: lo que hara HabilitarAccesoDigital en la 003b) ----

    /// <summary>
    /// Un Cliente registrado en la base y su usuario en el realm clientes, con las mismas condiciones que dejara
    /// habilitar su acceso: nombre de usuario = AccesoDigital.NombreDeUsuarioDe(documento), clave temporal y las acciones
    /// UPDATE_PASSWORD y CONFIGURE_TOTP. El vinculo sub - cliente se inserta directo en la base.
    /// </summary>
    public async Task<(Guid ClienteId, Documento Documento, UsuarioDePrueba Usuario)> CrearClienteConAccesoAsync(
        bool habilitado = true, bool vincular = true)
    {
        var documento = Documento.Crear(TipoDocumento.CC, Ayudas.NumeroDeDocumentoUnico());
        var cliente = Cliente.Registrar(documento, NombreDePersona.Crear("María José"), NombreDePersona.Crear("Núñez"),
            Correo.Crear("maria@example.com"), Telefono.Crear("+573001234567"), DateTimeOffset.UtcNow);
        await GuardarClienteAsync(cliente);

        var usuario = await CrearUsuarioDeClienteAsync(documento, habilitado);
        if (vincular)
            await VincularAsync(cliente.Id, usuario.Sub);

        return (cliente.Id, documento, usuario);
    }

    /// <summary>El usuario del realm clientes de un documento (sin tocar la base de la API).</summary>
    public async Task<UsuarioDePrueba> CrearUsuarioDeClienteAsync(Documento documento, bool habilitado = true)
    {
        var nombre = AccesoDigital.NombreDeUsuarioDe(documento);
        var clave = Ayudas.ClaveTemporal();
        var sub = await Admin.CrearUsuarioAsync(RealmDeClientes, nombre, clave, temporal: true,
            ["UPDATE_PASSWORD", "CONFIGURE_TOTP"], habilitado);
        return new UsuarioDePrueba(RealmDeClientes, nombre, sub, clave);
    }

    /// <summary>Un operador en el realm empleados con el rol operador. Su OTP se inscribe en su primer ingreso.</summary>
    public async Task<UsuarioDePrueba> CrearOperadorAsync()
    {
        var nombre = $"operador-{Guid.NewGuid():N}"[..16];
        var clave = Ayudas.ClaveValida();
        var sub = await Admin.CrearUsuarioAsync(RealmDeEmpleados, nombre, clave, temporal: false, []);
        await Admin.AsignarRolDeRealmAsync(RealmDeEmpleados, sub, "operador");
        return new UsuarioDePrueba(RealmDeEmpleados, nombre, sub, clave);
    }

    private async Task GuardarClienteAsync(Cliente cliente)
    {
        await using var db = CrearContexto();
        db.Clientes.Add(cliente);
        await db.GuardarCambiosAsync(Ayudas.Ct);
    }

    /// <summary>ADR-0016: inserta el vinculo directamente en la base (por SQL, sin depender del mapeo de la entidad).</summary>
    public async Task VincularAsync(Guid clienteId, string sub)
    {
        await using var db = CrearContexto();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO accesos_digitales (cliente_id, sub, fecha_de_vinculo) VALUES ({clienteId}, {sub}, {DateTimeOffset.UtcNow})",
            Ayudas.Ct);
    }

    // ---- Sesiones de punta a punta ----

    /// <summary>
    /// Inicia sesion de verdad (pantallas de Keycloak, cambio de clave e inscripcion del OTP si se los piden) y devuelve
    /// los tokens. Falla si Keycloak no emite el codigo.
    /// </summary>
    public async Task<(TokensOidc Tokens, IReadOnlyList<TipoDePagina> Recorrido)> IniciarSesionAsync(
        NavegadorSimulado navegador, UsuarioDePrueba usuario)
    {
        var inicio = await navegador.IniciarAsync(usuario.Realm);
        var (ultima, recorrido) = await navegador.ConducirAsync(inicio, usuario);
        Assert.True(ultima.Tipo == TipoDePagina.CodigoEmitido,
            $"Keycloak no emitio el codigo. Pantalla: {ultima.Tipo}. Errores: {ultima.MensajesDeError}. Recorrido: {string.Join(" > ", recorrido)}");
        return (await navegador.CanjearCodigoAsync(ultima), recorrido);
    }

    /// <summary>Un operador nuevo que inicia sesion de verdad (inscribiendo su OTP) y su access token.</summary>
    public async Task<string> ObtenerTokenDeOperadorAsync()
    {
        var operador = await CrearOperadorAsync();
        using var navegador = NuevoNavegador();
        var (tokens, _) = await IniciarSesionAsync(navegador, operador);
        return tokens.AccessToken;
    }
}
