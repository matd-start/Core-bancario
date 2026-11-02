using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.DocumentacionOpenApi;

/// <summary>
/// Esquemas ProblemDetails y HttpValidationProblemDetails: declara las extensiones que llevan TODOS los errores de la
/// API (ADR-0011), "codigo" ($ref al enum CodigoDeError) y "traceId", y las marca requeridas (RNF-12 de la 004). Sin
/// esto el contrato solo describe los campos de RFC 9457 y la SPA no recibe tipados los que de verdad usa. Va después
/// de CodigosDeErrorEnOpenApi, que crea el esquema al que apunta el $ref.
/// </summary>
public sealed class ExtensionesDeErrorEnOpenApi : IOpenApiDocumentTransformer
{
    private static readonly string[] EsquemasDeError = ["ProblemDetails", "HttpValidationProblemDetails"];

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var nombre in EsquemasDeError)
        {
            if (document.Components?.Schemas?.TryGetValue(nombre, out var encontrado) != true || encontrado is not OpenApiSchema esquema)
                continue;

            esquema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            esquema.Properties["codigo"] = new OpenApiSchemaReference(CodigosDeErrorEnOpenApi.NombreDelEsquema, document);
            esquema.Properties["traceId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Identificador de correlación de la petición: es el que se pide al usuario para buscar el error en los logs.",
            };
            esquema.Required ??= new HashSet<string>();
            esquema.Required.Add("codigo");
            esquema.Required.Add("traceId");
        }

        return Task.CompletedTask;
    }
}
