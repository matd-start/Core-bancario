using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RN-22 (inactividad y vida maxima), CL-10 (cerrar sesion) y CL-11. Con la inactividad bajada por el fixture a 40 s:
/// los valores reales (5 y 15 min, 30 min y 9 h) los verifican las pruebas de configuracion de Api.Tests.
/// </summary>
public class SesionesTests(KeycloakFixture kc)
{
    private static readonly TimeSpan EsperaMasAllaDeLaInactividad = KeycloakFixture.InactividadEnLasPruebas + TimeSpan.FromSeconds(5);

    private static async Task<string?> ErrorDeRenovarAsync(HttpResponseMessage respuesta, int estadoEsperado)
    {
        Assert.Equal(estadoEsperado, (int)respuesta.StatusCode);
        return (string?)(await respuesta.Content.ReadFromJsonAsync<JsonNode>(Ayudas.Ct))!["error"];
    }

    [Fact]
    public async Task F003_CL11_ClienteInactivo_SesionNoSeRenueva()
    {
        // Arrange
        var (_, _, cliente) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, cliente);

        // Act
        await Task.Delay(EsperaMasAllaDeLaInactividad, Ayudas.Ct);
        using var renovacion = await navegador.RenovarAsync(cliente.Realm, tokens.RefreshToken);

        // Assert
        Assert.Equal("invalid_grant", await ErrorDeRenovarAsync(renovacion, 400));
    }

    [Fact]
    public async Task F003_CA10_OperadorInactivo_SesionNoSeRenueva()
    {
        // Arrange
        var operador = await kc.CrearOperadorAsync();
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, operador);

        // Act
        await Task.Delay(EsperaMasAllaDeLaInactividad, Ayudas.Ct);
        using var renovacion = await navegador.RenovarAsync(operador.Realm, tokens.RefreshToken);

        // Assert
        Assert.Equal("invalid_grant", await ErrorDeRenovarAsync(renovacion, 400));
    }

    [Fact]
    public async Task F003_CA11_CerrarSesion_ElRefreshTokenYaNoSirve()
    {
        // Arrange
        var (_, _, cliente) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, cliente);

        // Act
        await navegador.CerrarSesionAsync(cliente.Realm, tokens.IdToken);
        using var renovacion = await navegador.RenovarAsync(cliente.Realm, tokens.RefreshToken);

        // Assert
        Assert.Equal("invalid_grant", await ErrorDeRenovarAsync(renovacion, 400));
    }

    [Fact]
    public async Task F003_CL10_CerrarSesion_ElAccessTokenYaEntregadoSigueAceptadoHastaQueVence()
    {
        // Arrange: la API valida el token sin preguntarle a Keycloak (ADR-0015): la ventana de revocacion es de hasta 5 min.
        var (_, _, cliente) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, cliente);
        await navegador.CerrarSesionAsync(cliente.Realm, tokens.IdToken);
        using var api = kc.CrearClienteDeApi(tokens.AccessToken);

        // Act
        var misCuentas = await api.GetAsync("/mis-cuentas", Ayudas.Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, misCuentas.StatusCode);
    }
}
