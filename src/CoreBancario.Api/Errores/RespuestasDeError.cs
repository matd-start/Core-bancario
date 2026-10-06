using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.Errores;

public static class RespuestasDeError
{
    /// <summary>400 con todos los errores por campo y el código estable "datos-invalidos".</summary>
    public static ValidationProblem DatosInvalidos(IReadOnlyDictionary<string, string[]> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        return TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(errores),
            title: "Datos inválidos",
            extensions: new Dictionary<string, object?> { ["codigo"] = CodigosDeError.DatosInvalidos });
    }
}
