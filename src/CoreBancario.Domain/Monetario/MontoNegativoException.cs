namespace CoreBancario.Domain.Monetario;

/// <summary>RN-15: el monto de un Dinero nunca es negativo.</summary>
public sealed class MontoNegativoException : ReglaDeNegocioException
{
    public MontoNegativoException(decimal monto, Moneda moneda)
        : base($"El monto no puede ser negativo: {monto} {moneda}.") { }
}
