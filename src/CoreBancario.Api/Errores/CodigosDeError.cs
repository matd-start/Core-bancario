namespace CoreBancario.Api.Errores;

/// <summary>Identificadores estables del contrato con core-bancario-web (RNF-03, ADR-0011). Nunca se renombran.</summary>
public static class CodigosDeError
{
    public const string DatosInvalidos = "datos-invalidos";
    public const string NoEncontrado = "no-encontrado";
    public const string DocumentoDuplicado = "documento-duplicado";
    public const string ConflictoDeConcurrencia = "conflicto-de-concurrencia";
    public const string TransicionNoPermitida = "transicion-no-permitida";
    public const string SaldoDistintoDeCero = "saldo-distinto-de-cero";
    public const string OperacionNoPermitida = "operacion-no-permitida";
    public const string SaldoInsuficiente = "saldo-insuficiente";
    public const string MontoNoPositivo = "monto-no-positivo";
    public const string MonedasDistintas = "monedas-distintas";
    public const string MontoNegativo = "monto-negativo";
    public const string PrecisionExcedida = "precision-excedida";
    public const string ReglaDeNegocio = "regla-de-negocio";
    public const string ErrorInesperado = "error-inesperado";
}
