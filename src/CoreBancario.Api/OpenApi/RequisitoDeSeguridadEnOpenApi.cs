using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CoreBancario.Api.OpenApi;

/// <summary>Operación: añade el requisito "Bearer" a toda operación con metadatos de autorización y sin AllowAnonymous.</summary>
public sealed class RequisitoDeSeguridadEnOpenApi : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadatos = context.Description.ActionDescriptor.EndpointMetadata;
        var exigeSesion = metadatos.OfType<IAuthorizeData>().Any() && !metadatos.OfType<IAllowAnonymous>().Any();
        if (!exigeSesion)
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(EsquemaDeSeguridadEnOpenApi.Nombre, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
