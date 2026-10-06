using System.Globalization;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Comun;

/// <summary>Monto en texto, cultura invariante y forma canónica: "0" COP, "0.00" USD.</summary>
public sealed record DineroDto(string Monto, string Moneda)
{
    public static DineroDto Desde(Dinero dinero)
    {
        ArgumentNullException.ThrowIfNull(dinero);

        // El monto ya está en forma canónica (escala = precisión de la moneda), así que ToString la conserva.
        return new DineroDto(dinero.Monto.ToString(CultureInfo.InvariantCulture), dinero.Moneda.Codigo);
    }
}
