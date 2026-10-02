using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-06: una cuenta solo se cierra con saldo cero.</summary>
public sealed class SaldoDistintoDeCeroException : ReglaDeNegocioException
{
    public SaldoDistintoDeCeroException(Dinero saldo)
        : base($"Una cuenta solo se cierra con saldo cero; saldo actual: {saldo}.") { }
}
