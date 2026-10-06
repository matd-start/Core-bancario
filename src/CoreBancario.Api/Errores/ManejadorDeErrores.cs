using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoreBancario.Api.Errores;

/// <summary>
/// Escribe ProblemDetails con IProblemDetailsService: status, title (del catálogo), detail (el mensaje de la
/// excepción SOLO si no es 500), instance (ruta) y la extensión "codigo". Siempre devuelve true.
/// Registra en log con nivel Error las de 500 (desde .NET 10 el middleware ya no registra en log las excepciones
/// que un IExceptionHandler maneja) y con nivel Information las demás.
/// </summary>
public sealed class ManejadorDeErrores(IProblemDetailsService problemDetailsService, ILogger<ManejadorDeErrores> logger) : IExceptionHandler
{
    private const string DetalleDeCuerpoInvalido = "La solicitud no tiene un formato válido.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var error = CatalogoDeErrores.Clasificar(exception);

        if (error.Estado >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error inesperado al atender {Metodo} {Ruta}.", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogInformation("Solicitud rechazada ({Codigo}) en {Metodo} {Ruta}.", error.Codigo, httpContext.Request.Method, httpContext.Request.Path);

        var problema = new ProblemDetails
        {
            Status = error.Estado,
            Title = error.Titulo,
            Detail = DetalleParaElCliente(error, exception),
            Instance = httpContext.Request.Path,
        };
        problema.Extensions["codigo"] = error.Codigo;

        httpContext.Response.StatusCode = error.Estado;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });

        return true;
    }

    // En el 500 nunca se envía el mensaje: puede contener SQL, rutas o datos internos (RNF-04).
    // El mensaje del framework para un JSON mal formado tampoco se envía: es técnico y está en inglés.
    private static string? DetalleParaElCliente(ErrorHttp error, Exception excepcion) => excepcion switch
    {
        _ when error.Estado >= StatusCodes.Status500InternalServerError => null,
        BadHttpRequestException => DetalleDeCuerpoInvalido,
        _ => excepcion.Message,
    };
}
