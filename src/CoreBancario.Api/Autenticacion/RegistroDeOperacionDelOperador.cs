namespace CoreBancario.Api.Autenticacion;

/// <summary>
/// RNF-05. Filtro de los endpoints del operador que cambian datos. Tras ejecutar, registra (Information)
/// "Operación {Operacion} del operador {OperadorId}: {Resultado}" ("completada" o "no completada").
/// </summary>
public sealed class RegistroDeOperacionDelOperador(ILogger<RegistroDeOperacionDelOperador> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var operacion = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? context.HttpContext.Request.Path.Value;
        var operador = context.HttpContext.User.Sub();

        try
        {
            var resultado = await next(context);
            // Una respuesta de error (400, 404...) no lanza excepción, pero la operación tampoco se completó.
            var fallida = Desenvolver(resultado) is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest };
            Registrar(operacion, operador, fallida ? "no completada" : "completada");
            return resultado;
        }
        catch
        {
            Registrar(operacion, operador, "no completada");
            throw;
        }
    }

    // Results<A, B> envuelve el resultado real (INestedHttpResult): hay que mirar el de adentro.
    private static object? Desenvolver(object? resultado) => resultado is INestedHttpResult anidado ? anidado.Result : resultado;

    private void Registrar(string? operacion, string operador, string resultado) =>
        logger.LogInformation("Operación {Operacion} del operador {OperadorId}: {Resultado}", operacion, operador, resultado);
}
