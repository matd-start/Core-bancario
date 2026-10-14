using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.OpenApi;

/// <summary>Documento: components.securitySchemes["Bearer"] = http/bearer/JWT con una descripción de los dos realms y la audiencia.</summary>
public sealed class EsquemaDeSeguridadEnOpenApi : IOpenApiDocumentTransformer
{
    public const string Nombre = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Nombre] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token JWT emitido por Keycloak: el realm 'clientes' para los clientes y el realm 'empleados' " +
                          "para los operadores. Debe llevar la audiencia 'core-bancario-api'. Vive 5 minutos como máximo.",
        };

        return Task.CompletedTask;
    }
}
