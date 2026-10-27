using CoreBancario.Bff.Sesiones;

namespace CoreBancario.Bff.Errores;

/// <summary>ProblemDetails {title, status, codigo, traceId}, sin detail. 401 lleva Cache-Control: no-store.</summary>
public static class RespuestasDelBff
{
    // SDD: esqueleto creado por test-writer
    public static IResult SesionTerminada(MotivoDeCierre motivo) => throw new NotImplementedException();

    public static Task EscribirPeticionNoConfiableAsync(HttpContext contexto) => throw new NotImplementedException();

    public static IResult IdentidadSinRespuesta() => throw new NotImplementedException();
}
