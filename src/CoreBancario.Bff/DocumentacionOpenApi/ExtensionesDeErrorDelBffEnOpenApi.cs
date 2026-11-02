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
    private const string NombreDelEsquema = "ProblemDetails";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Falla al generar el contrato en vez de publicarlo sin las extensiones.
        if (document.Components?.Schemas?.TryGetValue(NombreDelEsquema, out var encontrado) != true || encontrado is not OpenApiSchema esquema)
            throw new InvalidOperationException($"El contrato del BFF no tiene el esquema {NombreDelEsquema}: no se le pueden declarar codigo y traceId.");

        esquema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        esquema.Properties["codigo"] = new OpenApiSchemaReference(CodigosDelBffEnOpenApi.NombreDelEsquema, document);
        esquema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identificador de correlación de la petición (W3C trace context): el que el usuario da a soporte al reportar un error.",
        };
        esquema.Required ??= new HashSet<string>();
        esquema.Required.Add("codigo");
        esquema.Required.Add("traceId");

        return Task.CompletedTask;
    }
}
