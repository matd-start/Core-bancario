// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

/// <summary>
/// RNF-05. Filtro de los endpoints del operador que cambian datos. Tras ejecutar, registra (Information)
/// "Operación {Operacion} del operador {OperadorId}: {Resultado}" ("completada" o "no completada").
/// </summary>
public sealed class RegistroDeOperacionDelOperador : IEndpointFilter
{
    private readonly ILogger<RegistroDeOperacionDelOperador> _logger;

    public RegistroDeOperacionDelOperador(ILogger<RegistroDeOperacionDelOperador> logger) => _logger = logger;

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        throw new NotImplementedException();
}
