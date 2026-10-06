using Microsoft.AspNetCore.Diagnostics;

namespace CoreBancario.Api.Errores;

public sealed class ManejadorDeErrores(IProblemDetailsService problemDetailsService, ILogger<ManejadorDeErrores> logger) : IExceptionHandler
{
    // SDD: esqueleto creado por test-writer
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
