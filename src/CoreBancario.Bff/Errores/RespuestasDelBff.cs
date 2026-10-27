using System.Diagnostics;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Mvc;

namespace CoreBancario.Bff.Errores;

/// <summary>ProblemDetails {title, status, codigo, traceId}, sin detail. 401 lleva Cache-Control: no-store.</summary>
public static class RespuestasDelBff
{
    /// <summary>401: la sesión no existe o terminó. El código dice por qué (CodigosDelBff.De).</summary>
    public static IResult SesionTerminada(MotivoDeCierre motivo) =>
        new RespuestaDeProblema(StatusCodes.Status401Unauthorized, "Sesión terminada", CodigosDelBff.De(motivo));

    /// <summary>403 desde el middleware anti-CSRF: la petición está bien formada, pero no es de confianza.</summary>
    public static Task EscribirPeticionNoConfiableAsync(HttpContext contexto) =>
        new RespuestaDeProblema(StatusCodes.Status403Forbidden, "Petición no confiable", CodigosDelBff.PeticionNoConfiable)
            .ExecuteAsync(contexto);

    /// <summary>503: hacía falta hablar con Keycloak y no respondió. La sesión sigue abierta.</summary>
    public static IResult IdentidadSinRespuesta() =>
        new RespuestaDeProblema(StatusCodes.Status503ServiceUnavailable, "El sistema de identidad no respondió",
            CodigosDelBff.IdentidadSinRespuesta);

    /// <summary>Escribe el problema con el trace id de la petición. Nunca lleva "detail": no hay nada técnico que contar al navegador.</summary>
    private sealed class RespuestaDeProblema(int estado, string titulo, string codigo) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var problema = new ProblemDetails { Status = estado, Title = titulo };
            problema.Extensions["codigo"] = codigo;
            problema.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            httpContext.Response.Headers.CacheControl = "no-store";
            return Results.Problem(problema).ExecuteAsync(httpContext);
        }
    }
}
