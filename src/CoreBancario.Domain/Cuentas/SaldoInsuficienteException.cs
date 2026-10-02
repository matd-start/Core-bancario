using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-01: el saldo nunca queda negativo.</summary>
public sealed class SaldoInsuficienteException : ReglaDeNegocioException
{
    public SaldoInsuficienteException(Dinero saldo, Dinero monto)
        : base($"Saldo insuficiente: saldo {saldo}, monto {monto}.") { }
}
