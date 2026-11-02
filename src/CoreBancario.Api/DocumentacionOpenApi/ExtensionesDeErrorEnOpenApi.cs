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
    private const string DescripcionDelTraceId =
        "Identificador de correlación de la petición (W3C trace context): el que el usuario da a soporte al reportar un error.";

    private static readonly string[] EsquemasDeError = ["ProblemDetails", "HttpValidationProblemDetails"];

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var nombre in EsquemasDeError)
        {
            // Falla al generar el contrato en vez de publicarlo sin las extensiones (por ejemplo, si una versión nueva
            // de Microsoft.AspNetCore.OpenApi cambia el nombre del esquema).
            if (document.Components?.Schemas?.TryGetValue(nombre, out var encontrado) != true || encontrado is not OpenApiSchema esquema)
                throw new InvalidOperationException($"El contrato no tiene el esquema {nombre}: no se le pueden declarar codigo y traceId.");

            esquema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            esquema.Properties["codigo"] = new OpenApiSchemaReference(CodigosDeErrorEnOpenApi.NombreDelEsquema, document);
            esquema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = DescripcionDelTraceId };
            esquema.Required ??= new HashSet<string>();
            esquema.Required.Add("codigo");
            esquema.Required.Add("traceId");
        }

        return Task.CompletedTask;
    }
}
