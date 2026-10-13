// SDD: esqueleto creado por test-writer
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

    public static string Desde(Exception? falla) => throw new NotImplementedException();
}
