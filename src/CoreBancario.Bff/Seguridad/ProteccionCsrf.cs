using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Errores;

namespace CoreBancario.Bff.Seguridad;

/// <summary>
/// RNF-02 (ADR-0020). Antes de autenticar. Protege /api/** y /bff/**, salvo GET /bff/iniciar-sesion/*, GET
/// /bff/cambiar-clave y los dos CallbackPath. Rechaza con 403 "peticion-no-confiable" si falta "X-CSRF: 1" o si hay
/// Sec-Fetch-Site y no es "same-origin". Una petición rechazada nunca llega a la API.
/// </summary>
public sealed class ProteccionCsrf(RequestDelegate siguiente)
{
    public const string Encabezado = "X-CSRF";
    public const string Valor = "1";

    private const string SecFetchSite = "Sec-Fetch-Site";
    private const string MismoOrigen = "same-origin";

    public Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (!EstaProtegida(contexto.Request) || EsConfiable(contexto.Request))
            return siguiente(contexto);

        return RespuestasDelBff.EscribirPeticionNoConfiableAsync(contexto);
    }

    private static bool EstaProtegida(HttpRequest peticion)
    {
        var ruta = peticion.Path;
        if (!ruta.StartsWithSegments("/api") && !ruta.StartsWithSegments("/bff"))
            return false;

        // Exenciones: son navegaciones del propio navegador (un enlace, o el regreso de Keycloak) y no pueden llevar encabezados propios.
        if (!HttpMethods.IsGet(peticion.Method))
            return true;

        return !ruta.StartsWithSegments("/bff/iniciar-sesion")
            && !ruta.StartsWithSegments("/bff/cambiar-clave")
            && !ruta.StartsWithSegments(AreasDeSesion.RutaDeRegresoOidc(AreaDeSesion.Clientes))
            && !ruta.StartsWithSegments(AreasDeSesion.RutaDeRegresoOidc(AreaDeSesion.Empleados));
    }

    private static bool EsConfiable(HttpRequest peticion)
    {
        // Un formulario o un fetch de otro origen no puede poner este encabezado sin un preflight de CORS, que aquí nunca se concede.
        if (peticion.Headers[Encabezado] != Valor)
            return false;

        // Sec-Fetch-Site lo pone el navegador y la página no puede cambiarlo. Si falta, no es un navegador moderno: basta el encabezado.
        return !peticion.Headers.TryGetValue(SecFetchSite, out var origen)
            || (origen.Count == 1 && origen[0] == MismoOrigen);
    }
}
