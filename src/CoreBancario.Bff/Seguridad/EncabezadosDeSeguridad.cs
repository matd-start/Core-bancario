namespace CoreBancario.Bff.Seguridad;

/// <summary>En /bff/** y /api/**: Cache-Control: no-store y X-Content-Type-Options: nosniff (CL-11).</summary>
public sealed class EncabezadosDeSeguridad(RequestDelegate siguiente)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var ruta = contexto.Request.Path;
        if (ruta.StartsWithSegments("/bff") || ruta.StartsWithSegments("/api"))
        {
            // OnStarting y no asignar ahora: el proxy copia los encabezados de la API después y podría cambiarlos.
            contexto.Response.OnStarting(() =>
            {
                contexto.Response.Headers.CacheControl = "no-store";
                contexto.Response.Headers.XContentTypeOptions = "nosniff";
                return Task.CompletedTask;
            });
        }

        return siguiente(contexto);
    }
}
