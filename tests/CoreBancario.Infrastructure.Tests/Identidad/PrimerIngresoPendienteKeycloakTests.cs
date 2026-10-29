using System.Net;
using CoreBancario.Infrastructure.Identidad;
using Microsoft.Extensions.Options;
using static CoreBancario.Infrastructure.Tests.Identidad.ManejadorHttpDeGuion;

namespace CoreBancario.Infrastructure.Tests.Identidad;

/// <summary>
/// RF-16, CA-29: ProveedorDeIdentidadKeycloak.ComoUsuario lee requiredActions de la representación del usuario.
/// UPDATE_PASSWORD o CONFIGURE_TOTP pendientes = el primer ingreso aún no ocurrió. Con el HttpMessageHandler falso de la 003.
/// </summary>
public class PrimerIngresoPendienteKeycloakTests
{
    private const string Sub = "9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProveedorDeIdentidadKeycloak CrearProveedor(ManejadorHttpDeGuion manejador)
    {
        var http = new HttpClient(manejador) { BaseAddress = new Uri("http://keycloak.test/") };
        var opciones = Options.Create(new OpcionesDeKeycloak { UrlBase = http.BaseAddress, Realm = "clientes" });
        return new ProveedorDeIdentidadKeycloak(http, opciones);
    }

    private static string UsuarioConAcciones(string? requiredActions)
    {
        var acciones = requiredActions is null ? "" : $$""","requiredActions":{{requiredActions}}""";
        return $$"""{"id":"{{Sub}}","username":"cc1234567","enabled":true{{acciones}} }""";
    }

    private static Task<Application.AccesosDigitales.UsuarioDeIdentidad?> ObtenerConAccionesAsync(string? requiredActions) =>
        CrearProveedor(new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, UsuarioConAcciones(requiredActions))))
            .ObtenerAsync(Sub, Ct);

    [Theory]
    [InlineData("""["UPDATE_PASSWORD"]""")]
    [InlineData("""["CONFIGURE_TOTP"]""")]
    [InlineData("""["UPDATE_PASSWORD","CONFIGURE_TOTP"]""")]
    [InlineData("""["VERIFY_EMAIL","CONFIGURE_TOTP"]""")]
    public async Task F004_RF16_UsuarioConAccionesObligatorias_PrimerIngresoPendiente(string requiredActions)
    {
        // Arrange / Act
        var usuario = await ObtenerConAccionesAsync(requiredActions);

        // Assert
        Assert.NotNull(usuario);
        Assert.True(usuario.PrimerIngresoPendiente);
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""["VERIFY_EMAIL"]""")]
    public async Task F004_RF16_UsuarioSinLasAccionesDelPrimerIngreso_NoPendiente(string requiredActions)
    {
        // Arrange / Act
        var usuario = await ObtenerConAccionesAsync(requiredActions);

        // Assert
        Assert.NotNull(usuario);
        Assert.False(usuario.PrimerIngresoPendiente);
    }

    [Fact]
    public async Task F004_RF16_UsuarioSinRequiredActions_NoPendiente()
    {
        // Arrange / Act
        var usuario = await ObtenerConAccionesAsync(null);

        // Assert
        Assert.NotNull(usuario);
        Assert.False(usuario.PrimerIngresoPendiente);
    }

    [Fact]
    public async Task F004_RF16_BuscarPorNombreDeUsuario_TambienLeeLasAccionesObligatorias()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, $"[{UsuarioConAcciones("""["UPDATE_PASSWORD"]""")}]"));

        // Act
        var usuario = await CrearProveedor(manejador).BuscarPorNombreDeUsuarioAsync("CC1234567", Ct);

        // Assert
        Assert.NotNull(usuario);
        Assert.True(usuario.PrimerIngresoPendiente);
    }
}
