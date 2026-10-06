using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Comun;

/// <summary>Monto en texto, cultura invariante y forma canónica: "0" COP, "0.00" USD.</summary>
public sealed record DineroDto(string Monto, string Moneda)
{
    // SDD: esqueleto creado por test-writer
    public static DineroDto Desde(Dinero dinero) => throw new NotImplementedException();
}
