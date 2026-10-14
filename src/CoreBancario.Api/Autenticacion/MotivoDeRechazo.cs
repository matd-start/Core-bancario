using Microsoft.IdentityModel.Tokens;

namespace CoreBancario.Api.Autenticacion;

/// <summary>RNF-04. Motivo para el log; jamás se envía al cliente.</summary>
public static class MotivoDeRechazo
{
    public const string SinCredencial = "sin-credencial";             // falla == null
    public const string Vencida = "vencida";                          // SecurityTokenExpiredException
    public const string AunNoValida = "aun-no-valida";                // SecurityTokenNotYetValidException
    public const string FirmaInvalida = "firma-invalida";             // SecurityTokenInvalidSignatureException, SecurityTokenSignatureKeyNotFoundException
    public const string EmisorIncorrecto = "emisor-incorrecto";       // SecurityTokenInvalidIssuerException
    public const string AudienciaIncorrecta = "audiencia-incorrecta"; // SecurityTokenInvalidAudienceException
    public const string AlgoritmoNoPermitido = "algoritmo-no-permitido"; // SecurityTokenInvalidAlgorithmException
    public const string SinIdentificador = "sin-identificador";       // TokenSinSujetoException
    public const string MalFormada = "mal-formada";                   // cualquier otra

    /// <summary>
    /// Devuelve una de las constantes de arriba y NUNCA el mensaje de la excepción: el de IdentityModel puede traer
    /// partes del token.
    /// </summary>
    public static string Desde(Exception? falla) => falla switch
    {
        null => SinCredencial,
        SecurityTokenExpiredException => Vencida,
        SecurityTokenNotYetValidException => AunNoValida,
        // SecurityTokenSignatureKeyNotFoundException hereda de la de firma inválida.
        SecurityTokenInvalidSignatureException => FirmaInvalida,
        SecurityTokenInvalidIssuerException => EmisorIncorrecto,
        SecurityTokenInvalidAudienceException => AudienciaIncorrecta,
        SecurityTokenInvalidAlgorithmException => AlgoritmoNoPermitido,
        TokenSinSujetoException => SinIdentificador,
        _ => MalFormada,
    };
}
