using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// Pruebas de regresión pedidas por el reviewer de 004a (nacen en verde: el código ya existe). Cubren el regreso de Keycloak
/// cuando el navegador ya trae una cookie de sesión: la vida máxima no se reinicia con el mismo sid (RN-22), y la sesión
/// guardada bajo una clave nunca pasa a otra identidad por RenewAsync (fijación de sesión, ADR-0020).
/// </summary>
public class InicioDeSesionConSesionPreviaTests
{
    private const string TipoDeClaimDeLaClaveDeSesion = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    private static readonly AuthenticationScheme EsquemaOidcDeClientes =
        new("OidcClientes", "OidcClientes", typeof(OpenIdConnectHandler));

    /// <summary>
    /// Un TicketReceivedContext como el del regreso de Keycloak, con la cookie de sesión previa (la que protege la clave
    /// recibida) en la petición. Los servicios son los del host real del BFF (el ámbito de la petición).
    /// </summary>
    private static TicketReceivedContext ContextoDeRegresoConCookie(BffFixture bff, IServiceProvider servicios, string clavePrevia, AuthenticationTicket ticketNuevo)
    {
        var cookie = bff.Servicios.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ConfiguracionDeSesion.EsquemaDeCookie);
        var ticketDeLaCookie = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(TipoDeClaimDeLaClaveDeSesion, clavePrevia)], "pruebas")),
            new AuthenticationProperties(),
            ConfiguracionDeSesion.EsquemaDeCookie);

        var http = new DefaultHttpContext { RequestServices = servicios };
        http.Request.Headers.Cookie = $"{cookie.Cookie.Name}={cookie.TicketDataFormat.Protect(ticketDeLaCookie)}";

        return new TicketReceivedContext(http, EsquemaOidcDeClientes, new OpenIdConnectOptions(), ticketNuevo);
    }

    // ---- RN-22: el mismo sid no reinicia el Inicio ----

    [Fact]
    public async Task F004_RN22_NuevoInicioDeSesionConElMismoSid_NoReiniciaElInicio()
    {
        // Arrange: una sesión abierta hace 10 minutos y un regreso de Keycloak con el MISMO sid (por ejemplo tras cambiar la clave).
        await using var bff = new BffFixture();
        var almacen = bff.Servicios.GetRequiredService<AlmacenDeSesiones>();
        var ahora = bff.Reloj.GetUtcNow();
        var inicioOriginal = ahora.AddMinutes(-10);
        almacen.Agregar(Construir.Sesion(inicio: inicioOriginal, clave: "clave-previa", sid: "sid-1"));
        var ticketNuevo = Construir.Ticket("sub-1", "sid-1", "access-nuevo", "refresh-nuevo", "id-nuevo", ahora, ahora.AddSeconds(270));
        using var ambito = bff.Servicios.CreateScope();
        var contexto = ContextoDeRegresoConCookie(bff, ambito.ServiceProvider, "clave-previa", ticketNuevo);

        // Act: el evento prepara el regreso y después el manejador de cookies guarda el ticket con una clave nueva.
        await new EventosDeOidc(AreaDeSesion.Clientes).TicketReceived(contexto);
        var claveNueva = await almacen.StoreAsync(ticketNuevo);

        // Assert
        var sesionNueva = almacen.Obtener(claveNueva);
        Assert.NotNull(sesionNueva);
        Assert.Equal(inicioOriginal.ToUnixTimeSeconds(), sesionNueva.Inicio.ToUnixTimeSeconds());
        Assert.NotEqual("clave-previa", claveNueva);
        Assert.Null(almacen.Obtener("clave-previa"));
    }

    [Fact]
    public async Task F004_RN22_NuevoInicioDeSesionConOtroSid_EmpiezaUnaVidaMaximaNueva()
    {
        // Arrange: contraste del caso anterior; otra identidad no hereda el Inicio de la sesión previa.
        await using var bff = new BffFixture();
        var almacen = bff.Servicios.GetRequiredService<AlmacenDeSesiones>();
        var ahora = bff.Reloj.GetUtcNow();
        almacen.Agregar(Construir.Sesion(inicio: ahora.AddMinutes(-10), clave: "clave-previa", sid: "sid-1"));
        var ticketNuevo = Construir.Ticket("sub-2", "sid-2", "access-nuevo", "refresh-nuevo", "id-nuevo", ahora, ahora.AddSeconds(270));
        using var ambito = bff.Servicios.CreateScope();
        var contexto = ContextoDeRegresoConCookie(bff, ambito.ServiceProvider, "clave-previa", ticketNuevo);

        // Act
        await new EventosDeOidc(AreaDeSesion.Clientes).TicketReceived(contexto);
        var claveNueva = await almacen.StoreAsync(ticketNuevo);

        // Assert
        var sesionNueva = almacen.Obtener(claveNueva);
        Assert.NotNull(sesionNueva);
        Assert.Equal(ahora.ToUnixTimeSeconds(), sesionNueva.Inicio.ToUnixTimeSeconds());
        Assert.False(contexto.Properties!.Items.ContainsKey(PropiedadesDeSesion.Inicio));
    }

    // ---- ADR-0020: la barrera de RenewAsync ----

    [Theory]
    [InlineData("sid-otro", "clientes")]
    [InlineData("sid-1", "empleados")]
    public async Task F004_RN26_RenewAsync_TicketDeOtroSidOOtraArea_NoReemplazaLaSesionGuardada(string sidDelTicket, string areaDelTicket)
    {
        // Arrange: bajo la clave "k" hay una sesión de clientes con sid-1.
        var reloj = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Construir.Origen);
        var almacen = new AlmacenDeSesiones(reloj, Construir.OpcionesEnvueltas());
        almacen.Agregar(Construir.Sesion(clave: "k", sid: "sid-1", tokens: Construir.Tokens(Construir.Origen.AddSeconds(270), "original")));
        var ticketAjeno = Construir.Ticket("sub-ajeno", sidDelTicket, "access-ajeno", "refresh-ajeno", "id-ajeno",
            Construir.Origen, Construir.Origen.AddSeconds(270), areaDelTicket);

        // Act
        await almacen.RenewAsync("k", ticketAjeno);

        // Assert: sigue la sesión original, con sus tokens, su sid y su área.
        var sesion = almacen.Obtener("k");
        Assert.NotNull(sesion);
        Assert.Equal("sid-1", sesion.Sid);
        Assert.Equal(AreaDeSesion.Clientes, sesion.Area);
        Assert.Equal("access-original", sesion.Tokens.AccessToken);
        Assert.Equal("refresh-original", sesion.Tokens.RefreshToken);
    }

    [Fact]
    public async Task F004_RN26_RenewAsync_TicketDeLaMismaIdentidad_SiReemplazaLaSesion()
    {
        // Arrange: contraste de la barrera; mismo sid y misma área (caso de "Cambiar clave") sí actualiza.
        var reloj = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Construir.Origen);
        var almacen = new AlmacenDeSesiones(reloj, Construir.OpcionesEnvueltas());
        almacen.Agregar(Construir.Sesion(clave: "k", sid: "sid-1", tokens: Construir.Tokens(Construir.Origen.AddSeconds(270), "original")));
        var ticketPropio = Construir.Ticket("sub-1", "sid-1", "access-renovado", "refresh-renovado", "id-renovado",
            Construir.Origen, Construir.Origen.AddSeconds(270));

        // Act
        await almacen.RenewAsync("k", ticketPropio);

        // Assert
        Assert.Equal("access-renovado", almacen.Obtener("k")?.Tokens.AccessToken);
    }
}
