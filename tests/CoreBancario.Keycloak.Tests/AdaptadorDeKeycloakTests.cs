using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using static CoreBancario.Keycloak.Tests.Ayudas;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// Plan, sección 4 ("Llamadas a Keycloak"): el adaptador real contra un Keycloak real. Comprueba que cada petición hace en
/// Keycloak lo que el adaptador cree: la clave temporal, las acciones obligatorias, el borrado del segundo factor, el
/// cierre de sesiones y la marca de ADR-0017 en el último paso. El orden de las peticiones y la traducción de errores se
/// prueban sin Keycloak en Infrastructure.Tests.
/// </summary>
public class AdaptadorDeKeycloakTests(KeycloakFixture kc)
{
    private static string NombreDeUsuarioNuevo() =>
        AccesoDigital.NombreDeUsuarioDe(Documento.Crear(TipoDocumento.CC, NumeroDeDocumentoUnico()));

    private static ClaveTemporal ClaveNueva() => new(Ayudas.ClaveTemporal());

    /// <summary>Un usuario con el segundo factor inscrito y una sesión abierta, creado como lo deja un primer ingreso completo.</summary>
    private async Task<(UsuarioDePrueba Usuario, TokensOidc Tokens, NavegadorSimulado Navegador)> UsuarioConSesionAbiertaAsync()
    {
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, usuario);
        return (usuario, tokens, navegador);
    }

    private static IEnumerable<string> Textos(JsonNode? arreglo) => arreglo!.AsArray().Select(n => (string)n!);

    // ---- CA-01: crear ----

    [Fact]
    public async Task F003_CA01_CrearAsync_UsuarioHabilitadoConClaveTemporalAccionesObligatoriasYMarca()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var nombre = NombreDeUsuarioNuevo();
        var marca = Guid.NewGuid();

        // Act
        var sub = await proveedor.CrearAsync(nombre, ClaveNueva(), marca, Ct);

        // Assert
        var usuario = await kc.Admin.ObtenerUsuarioAsync(KeycloakFixture.RealmDeClientes, sub);
        Assert.True((bool?)usuario["enabled"]);
        Assert.Equal(nombre.ToLowerInvariant(), (string?)usuario["username"]);
        Assert.Equivalent(new[] { "UPDATE_PASSWORD", "CONFIGURE_TOTP" }, Textos(usuario["requiredActions"]));
        Assert.Equal([marca.ToString()], Textos(usuario["attributes"]?["ultima_solicitud_de_acceso"]));
        Assert.Contains("password", await kc.Admin.TiposDeCredencialAsync(KeycloakFixture.RealmDeClientes, sub));
    }

    [Fact]
    public async Task F003_CA01_CrearAsync_ElPrimerIngresoConLaClaveTemporalExigeCambiarlaEInscribirElOtp()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var nombre = NombreDeUsuarioNuevo();
        var clave = ClaveNueva();
        var sub = await proveedor.CrearAsync(nombre, clave, Guid.NewGuid(), Ct);
        var usuario = new UsuarioDePrueba(KeycloakFixture.RealmDeClientes, nombre, sub, clave.Valor);
        using var navegador = kc.NuevoNavegador();

        // Act
        var (tokens, recorrido) = await kc.IniciarSesionAsync(navegador, usuario);

        // Assert: la clave es temporal (RN-21) y el segundo factor se inscribe en el primer ingreso (RN-24).
        Assert.Contains(TipoDePagina.CambioDeClave, recorrido);
        Assert.Contains(TipoDePagina.ConfigurarOtp, recorrido);
        Assert.Equal(sub, (string?)CuerpoDelJwt(tokens.AccessToken)["sub"]);
    }

    // ---- ADR-0017: lecturas ----

    [Fact]
    public async Task F003_ADR0017_BuscarPorNombreDeUsuario_IgnoraMayusculasYDevuelveNullSiNoExiste()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var nombre = NombreDeUsuarioNuevo();
        var sub = await proveedor.CrearAsync(nombre, ClaveNueva(), Guid.NewGuid(), Ct);

        // Act
        var enMayusculas = await proveedor.BuscarPorNombreDeUsuarioAsync(nombre.ToUpperInvariant(), Ct);
        var enMinusculas = await proveedor.BuscarPorNombreDeUsuarioAsync(nombre.ToLowerInvariant(), Ct);
        var inexistente = await proveedor.BuscarPorNombreDeUsuarioAsync(NombreDeUsuarioNuevo(), Ct);

        // Assert
        Assert.Equal(sub, enMayusculas?.Sub);
        Assert.Equal(sub, enMinusculas?.Sub);
        Assert.Null(inexistente);
    }

    [Fact]
    public async Task F003_ADR0017_BuscarPorNombreDeUsuario_NoDevuelveUsuariosConUnNombreParecido()
    {
        // Arrange: la búsqueda es exacta; "CC123" no debe encontrar a "CC1234567".
        var proveedor = kc.CrearProveedorDeIdentidad();
        var nombre = NombreDeUsuarioNuevo();
        await proveedor.CrearAsync(nombre, ClaveNueva(), Guid.NewGuid(), Ct);

        // Act
        var parcial = await proveedor.BuscarPorNombreDeUsuarioAsync(nombre[..^1], Ct);

        // Assert
        Assert.Null(parcial);
    }

    [Fact]
    public async Task F003_ADR0017_ObtenerAsync_DevuelveHabilitadoYMarcaDelUsuario()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var marca = Guid.NewGuid();
        var sub = await proveedor.CrearAsync(NombreDeUsuarioNuevo(), ClaveNueva(), marca, Ct);

        // Act
        var usuario = await proveedor.ObtenerAsync(sub, Ct);

        // Assert
        Assert.Equal(new UsuarioDeIdentidad(sub, true, marca), usuario);
    }

    [Fact]
    public async Task F003_ADR0017_ObtenerAsync_SubInexistente_DevuelveNull()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();

        // Act
        var usuario = await proveedor.ObtenerAsync(Guid.NewGuid().ToString(), Ct);

        // Assert
        Assert.Null(usuario);
    }

    // ---- CA-02 / CL-13: restablecer ----

    [Fact]
    public async Task F003_CA02_ReiniciarAcceso_BorraOtpCierraSesionesYPoneClaveTemporal()
    {
        // Arrange: un usuario con el segundo factor inscrito y una sesión abierta.
        var proveedor = kc.CrearProveedorDeIdentidad();
        var (usuario, tokens, navegador) = await UsuarioConSesionAbiertaAsync();
        using var _ = navegador;
        Assert.Contains("otp", await kc.Admin.TiposDeCredencialAsync(usuario.Realm, usuario.Sub));
        var marca = Guid.NewGuid();

        // Act
        await proveedor.ReiniciarAccesoAsync(usuario.Sub, ClaveNueva(), marca, Ct);

        // Assert
        var tipos = await kc.Admin.TiposDeCredencialAsync(usuario.Realm, usuario.Sub);
        Assert.DoesNotContain("otp", tipos);
        Assert.Contains("password", tipos);
        Assert.Equal(0, await kc.Admin.SesionesAbiertasAsync(usuario.Realm, usuario.Sub));
        using var renovacion = await navegador.RenovarAsync(usuario.Realm, tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, renovacion.StatusCode);
        var representacion = await kc.Admin.ObtenerUsuarioAsync(usuario.Realm, usuario.Sub);
        Assert.True((bool?)representacion["enabled"]);
        Assert.Equivalent(new[] { "UPDATE_PASSWORD", "CONFIGURE_TOTP" }, Textos(representacion["requiredActions"]));
        Assert.Equal([marca.ToString()], Textos(representacion["attributes"]?["ultima_solicitud_de_acceso"]));
    }

    [Fact]
    public async Task F003_CA02_ReiniciarAcceso_LaClaveTemporalNuevaExigeCambiarlaEInscribirDeNuevoElOtp()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var (usuario, _, navegador) = await UsuarioConSesionAbiertaAsync();
        navegador.Dispose();
        var claveAnterior = usuario.Clave;
        var temporal = ClaveNueva();
        await proveedor.ReiniciarAccesoAsync(usuario.Sub, temporal, Guid.NewGuid(), Ct);

        // Act: entra con la clave temporal, como lo haría el cliente tras el restablecimiento.
        usuario.Clave = temporal.Valor;
        usuario.Otp = null;
        usuario.ClaveNueva = ClaveValida();
        using var nuevoNavegador = kc.NuevoNavegador();
        var (_, recorrido) = await kc.IniciarSesionAsync(nuevoNavegador, usuario);

        // Assert
        Assert.Contains(TipoDePagina.CambioDeClave, recorrido);
        Assert.Contains(TipoDePagina.ConfigurarOtp, recorrido);
        Assert.NotEqual(claveAnterior, temporal.Valor);
    }

    [Fact]
    public async Task F003_CL16_ReiniciarAcceso_DeUnUsuarioDeshabilitado_LoHabilita()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync(habilitado: false);

        // Act
        await proveedor.ReiniciarAccesoAsync(usuario.Sub, ClaveNueva(), Guid.NewGuid(), Ct);

        // Assert
        var representacion = await kc.Admin.ObtenerUsuarioAsync(usuario.Realm, usuario.Sub);
        Assert.True((bool?)representacion["enabled"]);
    }

    // ---- CA-03: deshabilitar ----

    [Fact]
    public async Task F003_CA03_DeshabilitarAsync_DeshabilitaCierraSesionesYPoneMarca()
    {
        // Arrange
        var proveedor = kc.CrearProveedorDeIdentidad();
        var (usuario, tokens, navegador) = await UsuarioConSesionAbiertaAsync();
        using var _ = navegador;
        var marca = Guid.NewGuid();

        // Act
        await proveedor.DeshabilitarAsync(usuario.Sub, marca, Ct);

        // Assert
        var representacion = await kc.Admin.ObtenerUsuarioAsync(usuario.Realm, usuario.Sub);
        Assert.False((bool?)representacion["enabled"]);
        Assert.Equal([marca.ToString()], Textos(representacion["attributes"]?["ultima_solicitud_de_acceso"]));
        Assert.Equal(0, await kc.Admin.SesionesAbiertasAsync(usuario.Realm, usuario.Sub));
        using var renovacion = await navegador.RenovarAsync(usuario.Realm, tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, renovacion.StatusCode);
    }

    // ---- ADR-0014: menor privilegio ----

    [Fact]
    public async Task F003_ADR0014_CuentaDeServicio_NoPuedeModificarElRealm()
    {
        // Arrange: el token de la cuenta de servicio (client credentials con el secreto del fixture).
        using var http = new HttpClient { BaseAddress = new Uri(kc.UrlDeKeycloak) };
        using var token = await http.PostAsync($"/realms/{KeycloakFixture.RealmDeClientes}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "core-bancario-api-admin",
                ["client_secret"] = kc.SecretoDeServicio,
            }), Ct);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var accessToken = (string)(await token.Content.ReadFromJsonAsync<JsonNode>(Ct))!["access_token"]!;
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/admin/realms/{KeycloakFixture.RealmDeClientes}")
        {
            Content = JsonContent.Create(new JsonObject { ["displayName"] = "cambiado-por-la-api" }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        using var respuesta = await http.SendAsync(solicitud, Ct);

        // Assert: solo manage-users y view-users (ADR-0014): el realm no se toca.
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
