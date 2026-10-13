// SDD: esqueleto creado por test-writer
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.OpenApi;

/// <summary>Operación: añade el requisito "Bearer" a toda operación con metadatos de autorización y sin AllowAnonymous.</summary>
public sealed class RequisitoDeSeguridadEnOpenApi : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
