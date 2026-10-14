using System.Security.Claims;
using System.Text.Json;
using CoreBancario.Api.Errores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace CoreBancario.Api.Autenticacion;

/// <summary>
/// Eventos compartidos por los dos esquemas: TokenValidated (sub obligatorio y rol según el esquema),
/// Challenge (401 no-autenticado, registra el motivo sin el token) y Forbidden (403 prohibido).
/// </summary>
/// <param name="rolEnKeycloak">Rol de realm que se busca en realm_access.roles ("cliente" u "operador").</param>
/// <param name="rolDeLaApi">Rol que la API concede si lo encuentra (Roles.Cliente o Roles.Operador).</param>
public sealed class EventosDeJwt(string rolEnKeycloak, string rolDeLaApi) : JwtBearerEvents
{
    /// <summary>
    /// El rol de la API depende del ESQUEMA que validó el token, no solo de lo que el token diga: un token del realm
    /// clientes nunca obtiene Operador aunque alguien cree ese rol en su realm (RN-19, plan decisión 4).
    /// </summary>
    public override Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;

        if (string.IsNullOrWhiteSpace(principal?.FindFirst("sub")?.Value))
        {
            context.Fail(new TokenSinSujetoException());
            return Task.CompletedTask;
        }

        if (principal.Identity is not ClaimsIdentity identidad)
            return Task.CompletedTask;

        // El token puede traer un claim de rol con el tipo de rol de la API: se descarta antes de decidir, para que el
        // rol salga solo del esquema que validó el token (ADR-0015).
        foreach (var rolDelToken in identidad.FindAll(identidad.RoleClaimType).ToList())
            identidad.RemoveClaim(rolDelToken);
        foreach (var rolDelToken in identidad.FindAll(ClaimTypes.Role).ToList())
            identidad.RemoveClaim(rolDelToken);

        // Se concede con el mismo tipo que consultan las políticas (RoleClaimType), el que se acaba de vaciar.
        if (RolesDeRealm(principal).Contains(rolEnKeycloak, StringComparer.Ordinal))
            identidad.AddClaim(new Claim(identidad.RoleClaimType, rolDeLaApi));

        return Task.CompletedTask;
    }

    /// <summary>La respuesta es idéntica para todos los motivos (RNF-04); el motivo solo va al log.</summary>
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        // Sin HandleResponse, JwtBearer escribiría su propia respuesta, con error y error_description.
        context.HandleResponse();

        var http = context.HttpContext;
        var motivo = MotivoDeRechazo.Desde(context.AuthenticateFailure);
        http.RequestServices.GetRequiredService<ILogger<EventosDeJwt>>()
            .LogInformation("Petición no autenticada en {Metodo} {Ruta}: {Motivo}", http.Request.Method, http.Request.Path, motivo);

        http.Response.Headers.WWWAuthenticate = "Bearer";
        return EscribirProblemaAsync(http, StatusCodes.Status401Unauthorized, "No autenticado", CodigosDeError.NoAutenticado);
    }

    public override Task Forbidden(ForbiddenContext context)
    {
        var http = context.HttpContext;
        http.RequestServices.GetRequiredService<ILogger<EventosDeJwt>>()
            .LogInformation("Petición prohibida en {Metodo} {Ruta}: el rol del usuario no sirve a esta operación", http.Request.Method, http.Request.Path);

        return EscribirProblemaAsync(http, StatusCodes.Status403Forbidden, "Prohibido", CodigosDeError.Prohibido);
    }

    private static async Task EscribirProblemaAsync(HttpContext http, int estado, string titulo, string codigo)
    {
        var problema = new ProblemDetails { Status = estado, Title = titulo, Instance = http.Request.Path };
        problema.Extensions["codigo"] = codigo;

        http.Response.StatusCode = estado;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problema,
        });
    }

    // Keycloak pone los roles de realm en el claim "realm_access": {"roles": ["cliente", ...]}. Con
    // MapInboundClaims = false ese claim llega tal cual, como texto JSON.
    private static IEnumerable<string> RolesDeRealm(ClaimsPrincipal principal)
    {
        var acceso = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(acceso))
            return [];

        try
        {
            using var documento = JsonDocument.Parse(acceso);
            if (documento.RootElement.ValueKind != JsonValueKind.Object
                || !documento.RootElement.TryGetProperty("roles", out var roles)
                || roles.ValueKind != JsonValueKind.Array)
                return [];

            return [.. roles.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()).OfType<string>()];
        }
        catch (JsonException)
        {
            // Un claim ilegible equivale a "sin roles": el usuario queda autenticado pero sin permisos (403).
            return [];
        }
    }
}
