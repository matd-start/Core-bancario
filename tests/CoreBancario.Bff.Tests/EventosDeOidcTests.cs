using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// EventosDeOidc (T07) sobre contextos construidos a mano: lo que cada evento hace con el mensaje OIDC sin necesitar
/// un Keycloak. El recorrido completo contra Keycloak real esta en Keycloak.Tests.
/// </summary>
public class EventosDeOidcTests
{
    private static readonly AuthenticationScheme EsquemaDeClientes =
        new("OidcClientes", "OidcClientes", typeof(OpenIdConnectHandler));

    private static DefaultHttpContext Contexto()
    {
        var servicios = new ServiceCollection()
            .AddSingleton(Construir.OpcionesEnvueltas())
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = servicios };
    }

    private static RedirectContext ContextoDeRedireccion(AuthenticationProperties propiedades) =>
        new(Contexto(), EsquemaDeClientes, new OpenIdConnectOptions(), propiedades) { ProtocolMessage = new OpenIdConnectMessage() };

    private static MessageReceivedContext ContextoDeRegreso(string? iss)
    {
        var mensaje = new OpenIdConnectMessage { Code = "codigo", State = "estado" };
        if (iss is not null)
            mensaje.Iss = iss;

        return new MessageReceivedContext(Contexto(), EsquemaDeClientes, new OpenIdConnectOptions(), new AuthenticationProperties())
        {
            ProtocolMessage = mensaje,
        };
    }

    private static TokenValidatedContext ContextoDeToken(AuthenticationProperties propiedades, string? estadoDeLaAccion)
    {
        var mensaje = new OpenIdConnectMessage { Code = "codigo", State = "estado" };
        if (estadoDeLaAccion is not null)
            mensaje.SetParameter("kc_action_status", estadoDeLaAccion);

        return new TokenValidatedContext(Contexto(), EsquemaDeClientes, new OpenIdConnectOptions(),
            new ClaimsPrincipal(new ClaimsIdentity("pruebas")), propiedades)
        {
            ProtocolMessage = mensaje,
        };
    }

    // ---- CL-09: prompt=login siempre ----

    [Fact]
    public async Task F004_CL09_RedirectToIdentityProvider_PideCredencialesConPromptLogin()
    {
        // Arrange
        var contexto = ContextoDeRedireccion(new AuthenticationProperties());

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).RedirectToIdentityProvider(contexto);

        // Assert
        Assert.Equal("login", contexto.ProtocolMessage.Prompt);
    }

    // ---- RF-15 / CA-21: cambiar clave usa kc_action ----

    [Fact]
    public async Task F004_CA21_RedirectToIdentityProvider_CambiarClave_UsaKcActionYNoPrompt()
    {
        // Arrange
        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Accion] = "UPDATE_PASSWORD";
        var contexto = ContextoDeRedireccion(propiedades);

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).RedirectToIdentityProvider(contexto);

        // Assert
        Assert.Equal("UPDATE_PASSWORD", contexto.ProtocolMessage.GetParameter("kc_action"));
        Assert.Null(contexto.ProtocolMessage.Prompt);
    }

    [Fact]
    public async Task F004_CA21_TokenValidated_CambioDeClaveExitoso_VuelveConAvisoClaveActualizada()
    {
        // Arrange
        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Accion] = "UPDATE_PASSWORD";
        var contexto = ContextoDeToken(propiedades, "success");

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).TokenValidated(contexto);

        // Assert
        Assert.Equal("/mis-cuentas?aviso=clave-actualizada", contexto.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("error")]
    [InlineData(null)]
    public async Task F004_CA21_TokenValidated_CambioDeClaveSinExito_VuelveConAvisoClaveSinCambios(string? estado)
    {
        // Arrange
        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Accion] = "UPDATE_PASSWORD";
        var contexto = ContextoDeToken(propiedades, estado);

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).TokenValidated(contexto);

        // Assert
        Assert.Equal("/mis-cuentas?aviso=clave-sin-cambios", contexto.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData(AreaDeSesion.Clientes, "clientes")]
    [InlineData(AreaDeSesion.Empleados, "empleados")]
    public async Task F004_RF14_TokenValidated_GuardaElAreaEnLasPropiedades(AreaDeSesion area, string segmento)
    {
        // Arrange
        var contexto = ContextoDeToken(new AuthenticationProperties(), null);

        // Act
        await new EventosDeOidc(area).TokenValidated(contexto);

        // Assert
        Assert.Equal(segmento, contexto.Properties!.Items[PropiedadesDeSesion.Area]);
    }

    // ---- RNF-05 / CL-01: el iss del regreso ----

    [Theory]
    [InlineData("https://evil.example/realms/clientes")]
    [InlineData("http://localhost:8080/realms/empleados")]
    [InlineData("")]
    [InlineData(null)]
    public async Task F004_RNF05_MessageReceived_IssDeOtroEmisorOAusente_FallaElRegreso(string? iss)
    {
        // Arrange
        var contexto = ContextoDeRegreso(iss);

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).MessageReceived(contexto);

        // Assert
        Assert.NotNull(contexto.Result?.Failure);
    }

    [Fact]
    public async Task F004_RNF05_MessageReceived_IssDelEmisorDelArea_NoRechaza()
    {
        // Arrange
        var contexto = ContextoDeRegreso(BffFixture.EmisorDeClientes);

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).MessageReceived(contexto);

        // Assert
        Assert.Null(contexto.Result);
    }
}
