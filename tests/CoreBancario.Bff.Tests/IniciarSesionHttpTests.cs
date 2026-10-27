using System.Net;
using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// GET /bff/iniciar-sesion/{area} y /bff/cambiar-clave: las comprobaciones previas al desafio a Keycloak, en este orden:
/// area valida, sesion de otra area, sesion de la misma area y disponibilidad de Keycloak. El desafio mismo (la redireccion a
/// Keycloak) necesita un Keycloak real y esta en Keycloak.Tests.
/// </summary>
public class IniciarSesionHttpTests
{
    [Fact]
    public async Task F004_RF14_IniciarSesion_ConAreaInvalida_Responde404()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.GetAsync("/bff/iniciar-sesion/otra", conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(AreaDeSesion.Clientes, "empleados", "/backoffice?aviso=otra-sesion-abierta")]
    [InlineData(AreaDeSesion.Empleados, "clientes", "/?aviso=otra-sesion-abierta")]
    public async Task F004_CL10_ConSesionDeOtraArea_IniciarSesion_RedirigeAOtraSesionAbierta(
        AreaDeSesion areaDeLaSesion, string areaPedida, string destinoEsperado)
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(areaDeLaSesion);

        // Act
        using var respuesta = await bff.GetAsync($"/bff/iniciar-sesion/{areaPedida}", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(destinoEsperado, Ayudas.Destino(respuesta));
    }

    [Fact]
    public async Task F004_CA13_ConSesionDeLaMismaArea_IniciarSesion_VaALaRutaDeRegreso()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes);

        // Act
        using var respuesta = await bff.GetAsync("/bff/iniciar-sesion/clientes?regreso=/mis-cuentas/abc", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/mis-cuentas/abc", Ayudas.Destino(respuesta));
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("/backoffice/clientes")]
    public async Task F004_CA13_ConSesionDeLaMismaArea_RegresoExternoODelBackoffice_VaAMisCuentas(string regreso)
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes);

        // Act
        using var respuesta = await bff.GetAsync(
            $"/bff/iniciar-sesion/clientes?regreso={Uri.EscapeDataString(regreso)}", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal("/mis-cuentas", Ayudas.Destino(respuesta));
    }

    [Theory]
    [InlineData("clientes", "/?aviso=identidad-no-disponible")]
    [InlineData("empleados", "/backoffice?aviso=identidad-no-disponible")]
    public async Task F004_CL05_KeycloakNoResponde_RedirigeConAvisoIdentidadNoDisponible(string area, string destinoEsperado)
    {
        // Arrange
        await using var bff = new BffFixture();
        bff.Identidad.Disponible = false;

        // Act
        using var respuesta = await bff.GetAsync($"/bff/iniciar-sesion/{area}", conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(destinoEsperado, Ayudas.Destino(respuesta));
    }

    [Fact]
    public async Task F004_CL05_ConOtraSesionAbiertaYKeycloakCaido_PrevaleceElAvisoDeOtraSesion()
    {
        // Arrange
        await using var bff = new BffFixture();
        bff.Identidad.Disponible = false;
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes);

        // Act
        using var respuesta = await bff.GetAsync("/bff/iniciar-sesion/empleados", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal("/backoffice?aviso=otra-sesion-abierta", Ayudas.Destino(respuesta));
    }

    [Fact]
    public async Task F004_CL05_ConSesionDeLaMismaAreaYKeycloakCaido_VaAlRegresoSinConsultarLaDisponibilidad()
    {
        // Arrange
        await using var bff = new BffFixture();
        bff.Identidad.Disponible = false;
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes);

        // Act
        using var respuesta = await bff.GetAsync("/bff/iniciar-sesion/clientes", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal("/mis-cuentas", Ayudas.Destino(respuesta));
    }

    [Fact]
    public async Task F004_RNF02_IniciarSesion_EsUnaNavegacion_NoExigeElEncabezadoAntiCsrf()
    {
        // Arrange
        await using var bff = new BffFixture();
        bff.Identidad.Disponible = false;

        // Act
        using var respuesta = await bff.EnviarAsync(HttpMethod.Get, "/bff/iniciar-sesion/clientes", conEncabezadoCsrf: false,
            secFetchSite: "none");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    [Fact]
    public async Task F004_CA21_CambiarClaveSinSesion_RedirigeAlInicioDelCliente()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.GetAsync("/bff/cambiar-clave", conEncabezadoCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", Ayudas.Destino(respuesta));
    }

    [Fact]
    public async Task F004_CA21_CambiarClaveConSesionDeEmpleados_RedirigeAlInicioDelCliente()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Empleados);

        // Act
        using var respuesta = await bff.GetAsync("/bff/cambiar-clave", sesion, conEncabezadoCsrf: false);

        // Assert
        Assert.Equal("/", Ayudas.Destino(respuesta));
    }
}
