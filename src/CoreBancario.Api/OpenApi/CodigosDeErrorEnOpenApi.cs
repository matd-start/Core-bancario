using System.Reflection;
using System.Text.Json.Nodes;
using CoreBancario.Api.Errores;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.OpenApi;

/// <summary>
/// components.schemas["CodigoDeError"] = string con enum de TODAS las constantes public const string de CodigosDeError
/// (por reflexión, en orden alfabético). Fuente del tipo CodigoDeError de la SPA (RNF-12).
/// </summary>
public sealed class CodigosDeErrorEnOpenApi : IOpenApiDocumentTransformer
{
    public const string NombreDelEsquema = "CodigoDeError";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var codigos = typeof(CodigosDeError)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[NombreDelEsquema] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identificadores estables del contrato de errores (ADR-0011). Nunca se renombran.",
            Enum = [.. codigos.Select(codigo => (JsonNode)JsonValue.Create(codigo)!)],
        };

        return Task.CompletedTask;
    }
}
