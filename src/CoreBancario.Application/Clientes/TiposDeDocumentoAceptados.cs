using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

/// <summary>
/// Interpreta el tipo de documento que llega como texto. Se compara con un switch explícito y no con
/// Enum.TryParse, que aceptaría "1" (el valor numérico) o "CC,PA" (varios valores a la vez).
/// </summary>
internal static class TiposDeDocumentoAceptados
{
    public const string MensajeDeError = "El tipo de documento debe ser CC, CE o PA.";

    public static bool TryInterpretar(string? texto, out TipoDocumento tipo)
    {
        switch (texto)
        {
            case "CC":
                tipo = TipoDocumento.CC;
                return true;
            case "CE":
                tipo = TipoDocumento.CE;
                return true;
            case "PA":
                tipo = TipoDocumento.PA;
                return true;
            default:
                tipo = default;
                return false;
        }
    }

    /// <summary>true si no queda nada tras quitar lo que Documento descarta al normalizar (espacios, '.' y '-').</summary>
    public static bool EstaVacioAlNormalizar(string? numero) =>
        numero is null || numero.All(c => char.IsWhiteSpace(c) || c is '.' or '-');
}
