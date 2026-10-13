// SDD: esqueleto creado por test-writer
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.OpenApi;

/// <summary>Documento: components.securitySchemes["Bearer"] = http/bearer/JWT con una descripción de los dos realms y la audiencia.</summary>
public sealed class EsquemaDeSeguridadEnOpenApi : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
