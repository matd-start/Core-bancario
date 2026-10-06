using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.Errores;

public static class RespuestasDeError
{
    // SDD: esqueleto creado por test-writer
    public static ValidationProblem DatosInvalidos(IReadOnlyDictionary<string, string[]> errores)
        => throw new NotImplementedException();
}
