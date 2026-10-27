using System.Net.Http.Headers;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Errores;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Seguridad;
using CoreBancario.Bff.Sesiones;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace CoreBancario.Bff.Proxy;

/// <summary>
/// /api/{**resto} → OpcionesDeLaApi.Url (YARP 2.3.0, AddHttpForwarder + MapForwarder). Antes de reenviar, un paso
/// previo obtiene el token con ServicioDeSesion.ObtenerAccessTokenAsync: SesionTerminadaException → 401 del BFF;
/// IdentidadSinRespuestaException → 503, en los dos casos SIN llamar a la API. Transformaciones: quita el prefijo /api;
/// quita Cookie, X-CSRF y Authorization entrantes; pone Authorization: Bearer; quita Set-Cookie de la respuesta.
/// </summary>
public static class ProxyDeLaApi
{
    public const string Prefijo = "/api";

    /// <summary>El token que resolvió el paso previo viaja hasta la transformación en HttpContext.Items.</summary>
    private const string ClaveDelToken = "cb:access-token";

    public static IEndpointRouteBuilder MapProxyDeLaApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var destino = app.ServiceProvider.GetRequiredService<IOptions<OpcionesDelBff>>().Value.Api.Url.ToString();

        var reenvio = app.MapForwarder($"{Prefijo}/{{**resto}}", destino, ConfigurarTransformaciones);
        reenvio.Add(constructor =>
        {
            var reenviar = constructor.RequestDelegate!;
            constructor.RequestDelegate = contexto => PasoPrevioAsync(contexto, reenviar);
        });
        reenvio.ExcludeFromDescription();

        return app;
    }

    private static void ConfigurarTransformaciones(TransformBuilderContext transformaciones)
    {
        transformaciones.AddPathRemovePrefix(Prefijo);

        // Nada de lo que identifica al navegador ante el BFF llega a la API (RNF-01).
        transformaciones.AddRequestHeaderRemove("Cookie");
        transformaciones.AddRequestHeaderRemove(ProteccionCsrf.Encabezado);
        transformaciones.AddRequestHeaderRemove("Authorization");
        transformaciones.AddRequestTransform(contexto =>
        {
            if (contexto.HttpContext.Items[ClaveDelToken] is string token)
                contexto.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return ValueTask.CompletedTask;
        });

        // La API no pone cookies en el navegador: la única es la del BFF.
        transformaciones.AddResponseHeaderRemove("Set-Cookie", ResponseCondition.Always);
    }

    private static async Task PasoPrevioAsync(HttpContext contexto, RequestDelegate reenviar)
    {
        var servicio = contexto.RequestServices.GetRequiredService<ServicioDeSesion>();
        try
        {
            var clave = await PropiedadesDeSesion.LeerClaveAsync(contexto);
            contexto.Items[ClaveDelToken] = await servicio.ObtenerAccessTokenAsync(clave, contexto.RequestAborted);
        }
        catch (SesionTerminadaException ex)
        {
            await RespuestasDelBff.SesionTerminada(ex.Motivo).ExecuteAsync(contexto);
            return;
        }
        catch (IdentidadSinRespuestaException)
        {
            await RespuestasDelBff.IdentidadSinRespuesta().ExecuteAsync(contexto);
            return;
        }

        await reenviar(contexto);
    }
}
