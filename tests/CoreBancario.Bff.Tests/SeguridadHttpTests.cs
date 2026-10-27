using System.Net;
using System.Net.Http.Json;
using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// RNF-02 (CSRF): X-CSRF: 1 y Sec-Fetch-Site same-origin en /api/** y /bff/**, con sus exenciones, y ningun GET que cambie
/// datos. Una peticion rechazada nunca llega a la API ni cambia la sesion.
/// </summary>
public class SeguridadHttpTests
{
    private static async Task<ProblemaDelBff> ProblemaAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<ProblemaDelBff>(Ayudas.Ct))!;

    [Theory]
    [InlineData("POST", "/api/clientes")]
    [InlineData("GET", "/api/mis-cuentas")]
    [InlineData("POST", "/bff/salir")]
    [InlineData("POST", "/bff/actividad")]
    [InlineData("GET", "/bff/sesion")]
    public async Task F004_CA06_PeticionSinEncabezadoAntiCsrf_SeRechazaSinLlegarALaApi(string metodo, string ruta)
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.EnviarAsync(new HttpMethod(metodo), ruta, sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("peticion-no-confiable", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
        Assert.Equal(0, bff.Identidad.Cierres);
        Assert.Equal(0, bff.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_CA06_RespuestaAntiCsrf_EsProblemDetailsConCodigoYTraceIdSinDetail()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Post, "/bff/salir", sesion, conEncabezadoCsrf: false);

        // Assert
        var problema = await ProblemaAsync(respuesta);
        Assert.Equal(403, problema.Status);
        Assert.False(string.IsNullOrWhiteSpace(problema.Title));
        Assert.False(string.IsNullOrWhiteSpace(problema.TraceId));
        Assert.Null(problema.Detail);
    }

    [Theory]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task F004_CA06_SecFetchSiteDistintoDeSameOrigin_SeRechazaSinLlegarALaApi(string secFetchSite)
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        using var contenido = JsonContent.Create(new { numero = "1" });

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Post, "/api/clientes", sesion, secFetchSite: secFetchSite, contenido: contenido);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("peticion-no-confiable", (await ProblemaAsync(respuesta)).Codigo);
        Assert.Empty(bff.Api.Peticiones);
    }

    [Fact]
    public async Task F004_CA06_EncabezadoAntiCsrfConOtroValor_SeRechaza()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/bff/salir");
        solicitud.Headers.Add("Cookie", sesion.Cookie);
        solicitud.Headers.Add("X-CSRF", "0");

        // Act
        using var respuesta = await bff.Cliente.SendAsync(solicitud, Ayudas.Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task F004_CA06_ConEncabezadoYSameOrigin_LaPeticionPasaYLlegaALaApi()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Post, "/api/clientes", sesion, secFetchSite: "same-origin",
            contenido: JsonContent.Create(new { numero = "1" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Single(bff.Api.Peticiones);
    }

    [Fact]
    public async Task F004_CA06_ConEncabezadoYSinSecFetchSite_LaPeticionPasa()
    {
        // Arrange: un cliente que no es un navegador (sin Sec-Fetch-Site) pero que manda X-CSRF: 1.
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Get, "/bff/sesion", sesion, secFetchSite: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ---- La cookie de sesion ----

    [Fact]
    public async Task F004_CA06_CookieDeSesion_EsSameSiteStrictYHttpOnly()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        var sesion = await bff.AbrirSesionAsync();

        // Assert
        var cookie = sesion.SetCookie.ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F004_RNF01_CookieDeSesion_NoTieneExpiresNiLlevaTokens()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        var sesion = await bff.AbrirSesionAsync();

        // Assert
        var cookie = sesion.SetCookie.ToLowerInvariant();
        Assert.DoesNotContain("expires=", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("max-age=", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("eyj", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.RefreshToken, sesion.SetCookie, StringComparison.Ordinal);
    }

    // ---- Exenciones ----

    [Theory]
    [InlineData("/bff/signin-oidc/clientes")]
    [InlineData("/bff/signin-oidc/empleados")]
    public async Task F004_RNF02_RegresosDeOidc_NoExigenElEncabezadoAntiCsrf(string ruta)
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Get, ruta, conEncabezadoCsrf: false, secFetchSite: "cross-site");

        // Assert
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---- Ningun GET cambia datos ----

    [Theory]
    [InlineData("/bff/actividad")]
    [InlineData("/bff/salir")]
    public async Task F004_RNF02_OperacionesDelBffQueCambianDatos_NoRespondenAGet(string ruta)
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync(ruta, sesion);
        using var consulta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.Equal(0, bff.Identidad.Cierres);
        Assert.Equal(0, bff.Identidad.Renovaciones);
    }
}
