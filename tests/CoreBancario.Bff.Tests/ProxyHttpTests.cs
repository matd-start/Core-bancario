using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// /api/{**resto}: el proxy hacia la API (ApiFalsa detras de IForwarderHttpClientFactory). Resuelve el token de la sesion o
/// responde 401/503 sin llamar a la API; reenvia con el Bearer de la sesion y nada mas.
/// </summary>
public class ProxyHttpTests
{
    private static async Task<ProblemaDelBff> ProblemaAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<ProblemaDelBff>(Ayudas.Ct))!;

    // ---- Sin sesion ----

    [Fact]
    public async Task F004_RNF01_ProxySinSesion_Responde401SinSesionYNoLlamaALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sin-sesion", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
    }

    [Fact]
    public async Task F004_CA07_ProxyConSesionVencidaPorInactividad_Responde401SinLlamarALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(301));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-vencida-por-inactividad", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
    }

    // ---- Lo que se reenvia ----

    [Fact]
    public async Task F004_RNF01_ProxyConSesion_ReenviaSinElPrefijoApiYConElBearerDeLaSesion()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        var peticion = Assert.Single(bff.Api.Peticiones);
        Assert.Equal("GET", peticion.Metodo);
        Assert.Equal("/mis-cuentas", peticion.RutaYConsulta);
        Assert.Equal($"Bearer {sesion.AccessToken}", peticion.Encabezado("Authorization"));
    }

    [Fact]
    public async Task F004_RNF01_ProxyNoReenviaLaCookieALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Null(Assert.Single(bff.Api.Peticiones).Encabezado("Cookie"));
    }

    [Fact]
    public async Task F004_RNF01_ProxyNoReenviaElEncabezadoAntiCsrfALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Null(Assert.Single(bff.Api.Peticiones).Encabezado("X-CSRF"));
    }

    [Fact]
    public async Task F004_RNF01_ProxyReemplazaLaAuthorizationQueTraigaElNavegador()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Get, "/api/mis-cuentas", sesion, authorization: "Bearer token-falso-del-navegador");

        // Assert
        Assert.Equal($"Bearer {sesion.AccessToken}", Assert.Single(bff.Api.Peticiones).Encabezado("Authorization"));
    }

    [Fact]
    public async Task F004_RNF04_ProxyConservaLaConsultaDeLaPeticion()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(Configuracion.AreaDeSesion.Empleados);

        // Act
        using var respuesta = await bff.GetAsync("/api/clientes/por-documento?tipo=CC&numero=1234567", sesion);

        // Assert
        Assert.Equal("/clientes/por-documento?tipo=CC&numero=1234567", Assert.Single(bff.Api.Peticiones).RutaYConsulta);
    }

    [Fact]
    public async Task F004_RNF02_ProxyReenviaElMetodoYElCuerpo()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(Configuracion.AreaDeSesion.Empleados);
        bff.Api.Estado = HttpStatusCode.Created;
        bff.Api.Cuerpo = """{"id":"abc"}""";
        using var contenido = new StringContent("""{"tipo":"CC"}""", Encoding.UTF8, "application/json");

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Post, "/api/clientes", sesion, contenido: contenido);

        // Assert
        var peticion = Assert.Single(bff.Api.Peticiones);
        Assert.Equal("POST", peticion.Metodo);
        Assert.Equal("""{"tipo":"CC"}""", peticion.Cuerpo);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("""{"id":"abc"}""", await respuesta.Content.ReadAsStringAsync(Ayudas.Ct));
    }

    // ---- Lo que vuelve ----

    [Fact]
    public async Task F004_RNF01_ProxyQuitaElSetCookieDeLaRespuestaDeLaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Api.EncabezadosDeRespuesta.Add(("Set-Cookie", "api=secreto; Path=/"));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.False(respuesta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task F004_CL11_ProxyResponde_NoStoreYNosniffAunqueLaApiNoLoPida()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Contains("no-store", respuesta.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task F004_CL24_ProxyConservaElEstadoYElCuerpoDeErrorDeLaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Api.Estado = HttpStatusCode.NotFound;
        bff.Api.Cuerpo = """{"codigo":"no-encontrado","status":404}""";

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas/00000000-0000-0000-0000-000000000001", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("""{"codigo":"no-encontrado","status":404}""", await respuesta.Content.ReadAsStringAsync(Ayudas.Ct));
    }

    // ---- RN-25 y la renovacion del access token ----

    [Fact]
    public async Task F004_RN25_ProxyNuncaRegistraActividad()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(200));
        using var datos = await bff.GetAsync("/api/mis-cuentas", sesion);
        bff.Avanzar(TimeSpan.FromSeconds(101));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task F004_CL12_ProxyConTokenPorVencer_RenuevaYReenviaElNuevo()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(241));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Equal(1, bff.Identidad.Renovaciones);
        Assert.Equal("Bearer access-renovado-1", Assert.Single(bff.Api.Peticiones).Encabezado("Authorization"));
    }

    [Fact]
    public async Task F004_CL12_ProxyConTokenVigente_NoRenueva()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(100));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Equal(0, bff.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_CL12_ProxyConRenovacionRechazada_Responde401SesionRevocadaSinLlamarALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Identidad.Modo = ModoDeIdentidad.Rechaza;
        bff.Avanzar(TimeSpan.FromSeconds(245));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-revocada", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
    }

    [Fact]
    public async Task F004_CL12_ProxyConKeycloakSinRespuesta_Responde503SinLlamarALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Identidad.Modo = ModoDeIdentidad.NoResponde;
        bff.Avanzar(TimeSpan.FromSeconds(245));

        // Act
        using var respuesta = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("identidad-sin-respuesta", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
    }

    [Fact]
    public async Task F004_CL12_ProxyConKeycloakSinRespuesta_LaSesionSigueAbierta()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Identidad.Modo = ModoDeIdentidad.NoResponde;
        bff.Avanzar(TimeSpan.FromSeconds(245));
        using var fallida = await bff.GetAsync("/api/mis-cuentas", sesion);

        // Act
        using var consulta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }

    // ---- RNF-04: el numero de documento no llega al log ----

    [Fact]
    public async Task F004_RNF04_BuscarPorDocumento_ElLogDelBffNoContieneElNumero()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(Configuracion.AreaDeSesion.Empleados);
        var numero = Random.Shared.Next(1_000_000, 1_000_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Act
        using var respuesta = await bff.GetAsync($"/api/clientes/por-documento?tipo=CC&numero={numero}", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(bff.Logs.QueContienen(numero));
    }
}
