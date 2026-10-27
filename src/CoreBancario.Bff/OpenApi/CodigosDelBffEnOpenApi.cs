using System.Reflection;
using System.Text.Json.Nodes;
using CoreBancario.Bff.Errores;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Bff.OpenApi;

/// <summary>
/// components.schemas["CodigoDeErrorDelBff"] = string con enum de TODAS las constantes public const string de CodigosDelBff
/// (por reflexión, en orden alfabético). Fuente del tipo de los códigos del BFF en la SPA (RNF-12). Gemelo de
/// CodigosDeErrorEnOpenApi de la API: son dos clases pequeñas en dos hosts que no se referencian (DRY a la tercera).
/// </summary>
public sealed class CodigosDelBffEnOpenApi : IOpenApiDocumentTransformer
{
    public const string NombreDelEsquema = "CodigoDeErrorDelBff";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var codigos = typeof(CodigosDelBff)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[NombreDelEsquema] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identificadores estables de los errores del BFF (campo codigo de sus respuestas). Nunca se renombran.",
            Enum = [.. codigos.Select(codigo => (JsonNode)JsonValue.Create(codigo)!)],
        };

        return Task.CompletedTask;
    }
}
