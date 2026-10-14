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
            Registrar(operacion, operador, "completada");
            return resultado;
        }
        catch
        {
            Registrar(operacion, operador, "no completada");
            throw;
        }
    }

    private void Registrar(string? operacion, string operador, string resultado) =>
        logger.LogInformation("Operación {Operacion} del operador {OperadorId}: {Resultado}", operacion, operador, resultado);
}
