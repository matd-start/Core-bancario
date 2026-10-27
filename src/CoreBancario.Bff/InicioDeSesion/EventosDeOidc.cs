using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>Eventos de los dos esquemas OIDC; uno por área.</summary>
public sealed class EventosDeOidc(AreaDeSesion area) : OpenIdConnectEvents
{
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
        var propiedades = context.Properties!;
        propiedades.Items[PropiedadesDeSesion.Area] = AreasDeSesion.Segmento(area);

        if (propiedades.Items.ContainsKey(PropiedadesDeSesion.Accion))
        {
            var exito = context.ProtocolMessage?.GetParameter("kc_action_status") == EstadoDeLaAccionExitosa;
            var aviso = exito ? Avisos.ClaveActualizada : Avisos.ClaveSinCambios;
            propiedades.RedirectUri = $"{AreasDeSesion.InicioDeLaSpa(area)}?aviso={aviso}";
        }

        return Task.CompletedTask;
    }

    /// <summary>RN-26: mismo sid conserva Inicio; otro sid cierra la sesión anterior.</summary>
    public override async Task TicketReceived(TicketReceivedContext context)
    {
        var cookie = await context.HttpContext.AuthenticateAsync(ConfiguracionDeSesion.EsquemaDeCookie);
        if (!cookie.Succeeded || cookie.Properties is null
            || !cookie.Properties.Items.TryGetValue(PropiedadesDeSesion.Clave, out var clave) || clave is null)
            return;

        var almacen = context.HttpContext.RequestServices.GetRequiredService<AlmacenDeSesiones>();
        var anterior = almacen.Obtener(clave);
        if (anterior is null)
            return;

        var sidNuevo = context.Principal?.FindFirst("sid")?.Value;
        if (anterior.Area == area && anterior.Sid == sidNuevo)
        {
            // Es la misma sesión de Keycloak: el inicio de la vida máxima no se reinicia (RN-22).
            context.Properties!.Items[PropiedadesDeSesion.Inicio] = anterior.Inicio.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        // Una sola sesión por navegador (RN-26): la anterior se cierra en Keycloak antes de guardar la nueva.
        var servicio = context.HttpContext.RequestServices.GetRequiredService<ServicioDeSesion>();
        await servicio.CerrarAsync(clave, context.HttpContext.RequestAborted);
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
