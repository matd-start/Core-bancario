using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>Eventos de los dos esquemas OIDC; uno por área.</summary>
public sealed class EventosDeOidc(AreaDeSesion area) : OpenIdConnectEvents
{
    /// <summary>
    /// Tipo del claim donde CookieAuthenticationHandler guarda la clave de sesión del SessionStore dentro de la cookie. Es la
    /// constante privada SessionIdClaim de ese manejador (ASP.NET Core): si cambiara, la prueba de fijación de sesión fallaría.
    /// </summary>
    private const string TipoDeClaimDeLaClaveDeSesion = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    private const string AccionCambiarClave = "UPDATE_PASSWORD";
    private const string EstadoDeLaAccionExitosa = "success";

    /// <summary>prompt=login (CL-09), salvo Items[Accion] = "UPDATE_PASSWORD": entonces kc_action=UPDATE_PASSWORD y sin prompt.</summary>
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        if (context.Properties.Items.TryGetValue(PropiedadesDeSesion.Accion, out var accion) && accion == AccionCambiarClave)
        {
            // El usuario ya tiene sesión en Keycloak: se reutiliza para pedirle solo el cambio de clave.
            context.ProtocolMessage.SetParameter("kc_action", AccionCambiarClave);
        }
        else
        {
            // Aunque Keycloak conserve una sesión viva, cada inicio de sesión del BFF pide credenciales (ADR-0021).
            context.ProtocolMessage.Prompt = "login";
        }

        return Task.CompletedTask;
    }

    /// <summary>RNF-05: ProtocolMessage.Iss distinto del Emisor del área, o ausente, hace context.Fail.</summary>
    public override Task MessageReceived(MessageReceivedContext context)
    {
        var esperado = OpcionesDelArea(context.HttpContext).Emisor.TrimEnd('/');
        var recibido = context.ProtocolMessage.Iss?.TrimEnd('/');

        if (string.IsNullOrEmpty(recibido) || !string.Equals(recibido, esperado, StringComparison.Ordinal))
            context.Fail("El emisor del regreso no es el del área.");

        return Task.CompletedTask;
    }

    /// <summary>Con Items[Accion]: RedirectUri según kc_action_status. Siempre: Items[Area].</summary>
    public override Task TokenValidated(TokenValidatedContext context)
    {
        var propiedades = context.Properties ??= new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Area] = AreasDeSesion.Segmento(area);

        if (propiedades.Items.ContainsKey(PropiedadesDeSesion.Accion))
        {
            var exito = context.ProtocolMessage?.GetParameter("kc_action_status") == EstadoDeLaAccionExitosa;
            var aviso = exito ? Avisos.ClaveActualizada : Avisos.ClaveSinCambios;
            propiedades.RedirectUri = $"{AreasDeSesion.InicioDeLaSpa(area)}?aviso={aviso}";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// RN-26 y rotación de la clave: cada regreso con éxito recibe una clave NUEVA. La sesión que el navegador traía se
    /// descarta antes de que el manejador de cookies guarde la nueva; si es de otro sid u otra área, además se cierra en Keycloak.
    /// Con el mismo sid (por ejemplo tras cambiar la clave) la sesión nueva conserva el Inicio de la anterior (RN-22).
    /// </summary>
    public override async Task TicketReceived(TicketReceivedContext context)
    {
        var almacen = context.HttpContext.RequestServices.GetRequiredService<AlmacenDeSesiones>();
        var claveAnterior = LeerClaveDeLaCookie(context.HttpContext);
        var anterior = claveAnterior is null ? null : almacen.ObtenerParaEvaluar(claveAnterior);
        if (claveAnterior is null || anterior is null)
            return;

        var sidNuevo = context.Principal?.FindFirst("sid")?.Value;
        if (anterior.Area == area && anterior.Sid == sidNuevo)
        {
            // Es la misma sesión de Keycloak: la vida máxima no se reinicia (RN-22) y Keycloak no se cierra (cerraría la nueva).
            if (context.Properties is { } propiedades)
                propiedades.Items[PropiedadesDeSesion.Inicio] = anterior.Inicio.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

            almacen.Eliminar(claveAnterior);
            return;
        }

        // Una sola sesión por navegador (RN-26): la anterior se cierra en Keycloak y se borra antes de guardar la nueva.
        var servicio = context.HttpContext.RequestServices.GetRequiredService<ServicioDeSesion>();
        await servicio.CerrarAsync(claveAnterior, context.HttpContext.RequestAborted);
    }

    /// <summary>
    /// La clave de la sesión que trae la cookie de ESTA petición, leída sin pasar por el manejador de cookies. Es a propósito:
    /// si el manejador autentica la cookie, recuerda la clave y, al iniciar sesión, llama a RenewAsync con esa misma clave en
    /// vez de a StoreAsync. Leyéndola aquí y borrando la sesión del almacén, el manejador no encuentra nada y pide una clave
    /// nueva, de modo que la cookie anterior deja de servir (fijación de sesión). El ticket de la cookie solo lleva esa clave.
    /// </summary>
    private static string? LeerClaveDeLaCookie(HttpContext contexto)
    {
        var cookie = contexto.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ConfiguracionDeSesion.EsquemaDeCookie);
        var valor = cookie.CookieManager.GetRequestCookie(contexto, cookie.Cookie.Name ?? "");
        if (string.IsNullOrEmpty(valor))
            return null;

        return cookie.TicketDataFormat.Unprotect(valor)?.Principal.FindFirst(TipoDeClaimDeLaClaveDeSesion)?.Value;
    }

    /// <summary>CL-01/CL-02: HandleResponse(); con sesión vigente a su inicio; si no, al aviso inicio-fallido.</summary>
    public override async Task RemoteFailure(RemoteFailureContext context)
    {
        context.HandleResponse();

        var log = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<EventosDeOidc>();
        // Solo el tipo del fallo: nunca el code, el state ni tokens (RNF-04).
        log.LogWarning("Falló el regreso de Keycloak del área {Area}: {Motivo}.",
            AreasDeSesion.Segmento(area), context.Failure?.GetType().Name ?? "desconocido");

        var destino = await TieneSesionVigenteAsync(context.HttpContext)
            ? AreasDeSesion.InicioDeLaSpa(area)
            : Avisos.UrlDe(area, Avisos.InicioFallido);
        context.Response.Redirect(destino);
    }

    private async Task<bool> TieneSesionVigenteAsync(HttpContext contexto)
    {
        var cookie = await contexto.AuthenticateAsync(ConfiguracionDeSesion.EsquemaDeCookie);
        if (!cookie.Succeeded || cookie.Properties is null
            || !cookie.Properties.Items.TryGetValue(PropiedadesDeSesion.Clave, out var clave))
            return false;

        try
        {
            var sesion = await contexto.RequestServices.GetRequiredService<ServicioDeSesion>()
                .ConsultarAsync(clave, contexto.RequestAborted);
            return sesion.Area == AreasDeSesion.Segmento(area);
        }
        catch (SesionTerminadaException)
        {
            return false;
        }
    }

    private OpcionesDeArea OpcionesDelArea(HttpContext contexto) =>
        contexto.RequestServices.GetRequiredService<IOptions<OpcionesDelBff>>().Value.De(area);
}
