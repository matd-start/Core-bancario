namespace CoreBancario.Domain.Monetario;

/// <summary>RN-16: un monto con más decimales de los que admite su moneda se rechaza.</summary>
public sealed class PrecisionExcedidaException : ReglaDeNegocioException
{
    public PrecisionExcedidaException(decimal monto, Moneda moneda)
        : base($"El monto {monto} tiene más decimales de los que admite {moneda}.") { }
}
