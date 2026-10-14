using CoreBancario.Api.Autenticacion;
using CoreBancario.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CoreBancario.Api.Tests.ApiFixture))]

namespace CoreBancario.Api.Tests;

/// <summary>
/// Un contenedor PostgreSQL y una WebApplicationFactory por proyecto de pruebas, compartidos por todas sus clases
/// (xUnit v3 AssemblyFixture). El contenedor usa la misma imagen que docker-compose.yml y las migraciones reales
/// (RNF-02). El aislamiento entre pruebas es por datos unicos (ver Ayudas), sin limpiar la base.
/// Desde la 003 la API exige sesion: los tokens los firma EmisorDePruebas con claves RSA propias (una por realm,
/// ADR-0018) y el fixture las registra como configuracion OIDC fija de cada esquema, asi JwtBearer no descarga nada.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Todo lo que la API registra en el log (RNF-03, RNF-04, RNF-05).</summary>
    public ProveedorDeLogsEnMemoria Logs { get; } = new();

    public string CadenaDeConexion => _contenedor.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _contenedor.StartAsync();

        var opciones = new DbContextOptionsBuilder<CoreBancarioDbContext>();
        opciones.UsarPostgres(_contenedor.GetConnectionString());
        await using (var db = new CoreBancarioDbContext(opciones.Options))
            await db.Database.MigrateAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(constructor =>
        {
            constructor.UseSetting("ConnectionStrings:CoreBancario", _contenedor.GetConnectionString());
            constructor.UseSetting("Autenticacion:Audiencia", EmisorDePruebas.Audiencia);
            constructor.UseSetting("Autenticacion:Clientes:Emisor", EmisorDePruebas.UrlDe(EmisorDeToken.Clientes));
            constructor.UseSetting("Autenticacion:Empleados:Emisor", EmisorDePruebas.UrlDe(EmisorDeToken.Empleados));
            constructor.ConfigureLogging(registro => registro.AddProvider(Logs));
            constructor.ConfigureTestServices(servicios =>
            {
                servicios.Configure<JwtBearerOptions>(EsquemasDeAutenticacion.Clientes,
                    o => FijarConfiguracionOidc(o, EmisorDeToken.Clientes));
                servicios.Configure<JwtBearerOptions>(EsquemasDeAutenticacion.Empleados,
                    o => FijarConfiguracionOidc(o, EmisorDeToken.Empleados));
            });
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        await _contenedor.DisposeAsync();
    }

    private static void FijarConfiguracionOidc(JwtBearerOptions opciones, EmisorDeToken emisor)
    {
        opciones.Configuration = new OpenIdConnectConfiguration { Issuer = EmisorDePruebas.UrlDe(emisor) };
        opciones.Configuration.SigningKeys.Add(EmisorDePruebas.ClaveDe(emisor));
    }

    /// <summary>Un cliente HTTP contra la API real, con el token de un Operador (las pruebas de la 002 lo usan).</summary>
    public HttpClient CrearCliente() => CrearClienteConToken(EmisorDePruebas.CrearTokenDeOperador());

    /// <summary>Un cliente HTTP sin ninguna credencial.</summary>
    public HttpClient CrearClienteAnonimo() => Factory.CreateClient();

    /// <summary>Un cliente HTTP con el token de un Cliente (realm clientes, rol cliente) cuyo sub es el indicado.</summary>
    public HttpClient CrearClienteComoCliente(string sub) =>
        CrearClienteConToken(EmisorDePruebas.CrearTokenDeCliente(sub));

    /// <summary>Un cliente HTTP con exactamente el token indicado en la cabecera Authorization.</summary>
    public HttpClient CrearClienteConToken(string token) => ConToken(Factory.CreateClient(), token);

    /// <summary>Un cliente HTTP de Operador contra una API derivada (ConServicios).</summary>
    public HttpClient CrearClienteDe(WebApplicationFactory<Program> fabrica) =>
        ConToken(fabrica.CreateClient(), EmisorDePruebas.CrearTokenDeOperador());

    /// <summary>Una API derivada que sustituye servicios (por ejemplo, el generador de numeros o el reloj).</summary>
    public WebApplicationFactory<Program> ConServicios(Action<IServiceCollection> configurar) =>
        Factory.WithWebHostBuilder(constructor => constructor.ConfigureTestServices(configurar));

    /// <summary>
    /// ADR-0016: vincula el sub de un Cliente con su ClienteId, directamente en la base (como la 002 preparaba cuentas
    /// con saldo). Por SQL, para no depender de que la entidad ya este mapeada.
    /// </summary>
    public async Task VincularAsync(Guid clienteId, string sub)
    {
        using var alcance = Factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CoreBancarioDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO accesos_digitales (cliente_id, sub, fecha_de_vinculo) VALUES ({clienteId}, {sub}, {DateTimeOffset.UtcNow})",
            TestContext.Current.CancellationToken);
    }

    private static HttpClient ConToken(HttpClient http, string token)
    {
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}
