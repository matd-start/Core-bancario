using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Bff.DocumentacionOpenApi;

/// <summary>
/// Esquema ProblemDetails: declara "codigo" ($ref al enum CodigoDeErrorDelBff) y "traceId", que RespuestasDelBff pone
/// en todos los errores del BFF, y los marca requeridos (RNF-12 de la 004). Gemelo de ExtensionesDeErrorEnOpenApi de la
/// API, por la misma razón que CodigosDelBffEnOpenApi. Va después de CodigosDelBffEnOpenApi, que crea el esquema del $ref.
/// </summary>
public sealed class ExtensionesDeErrorDelBffEnOpenApi : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Components?.Schemas?.TryGetValue("ProblemDetails", out var encontrado) != true || encontrado is not OpenApiSchema esquema)
            return Task.CompletedTask;

        esquema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        esquema.Properties["codigo"] = new OpenApiSchemaReference(CodigosDelBffEnOpenApi.NombreDelEsquema, document);
        esquema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identificador de correlación de la petición: es el que se pide al usuario para buscar el error en los logs.",
        };
        esquema.Required ??= new HashSet<string>();
        esquema.Required.Add("codigo");
        esquema.Required.Add("traceId");

        return Task.CompletedTask;
    }
}
