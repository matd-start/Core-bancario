using System.Globalization;

namespace CoreBancario.Domain.Monetario;

/// <summary>
/// Par monto + moneda. Invariantes: Monto >= 0 (RN-15); Monto sin más decimales que la precisión de su
/// moneda (RN-16); forma canónica: Monto.Scale == Moneda.Precision.
/// </summary>
public sealed record Dinero
{
    public decimal Monto { get; }
    public Moneda Moneda { get; }

    /// <summary>true si Monto == 0. Propiedad calculada: no forma parte de la igualdad del record.</summary>
    public bool EsCero => Monto == 0m;

    private Dinero(decimal monto, Moneda moneda)
    {
        Monto = monto;
        Moneda = moneda;
    }

    /// <summary>Crea un Dinero a partir de un monto pedido por alguien. Nunca redondea.</summary>
    public static Dinero Crear(decimal monto, Moneda moneda)
    {
        ArgumentNullException.ThrowIfNull(moneda);

        if (monto < 0m)
            throw new MontoNegativoException(monto, moneda);

        // La precisión se evalúa sobre el valor: 1000.00m COP es válido porque su valor no tiene decimales.
        if (decimal.Round(monto, moneda.Precision, MidpointRounding.ToEven) != monto)
            throw new PrecisionExcedidaException(monto, moneda);

        return new Dinero(AFormaCanonica(monto, moneda), moneda);
    }

    /// <summary>
    /// Crea un Dinero a partir del resultado de un cálculo, ajustándolo a la precisión de la moneda;
    /// en el punto medio exacto redondea al par (ADR-0007).
    /// </summary>
    public static Dinero DesdeCalculo(decimal resultado, Moneda moneda)
    {
        ArgumentNullException.ThrowIfNull(moneda);

        // Se comprueba antes de redondear, para que un negativo diminuto no se convierta en un cero válido.
        if (resultado < 0m)
            throw new MontoNegativoException(resultado, moneda);

        return new Dinero(AFormaCanonica(resultado, moneda), moneda);
    }

    public Dinero Sumar(Dinero otro)
    {
        ArgumentNullException.ThrowIfNull(otro);

        if (otro.Moneda != Moneda)
            throw new MonedasDistintasException(Moneda, otro.Moneda);

        return new Dinero(AFormaCanonica(Monto + otro.Monto, Moneda), Moneda);
    }

    public Dinero Restar(Dinero otro)
    {
        ArgumentNullException.ThrowIfNull(otro);

        if (otro.Moneda != Moneda)
            throw new MonedasDistintasException(Moneda, otro.Moneda);

        var resultado = Monto - otro.Monto;
        if (resultado < 0m)
            throw new MontoNegativoException(resultado, Moneda);

        return new Dinero(AFormaCanonica(resultado, Moneda), Moneda);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Monto} {Moneda}");

    // Fija la escala del monto a la precisión de la moneda. Round solo puede reducir la escala y
    // sumar un cero con la escala deseada la amplía (la suma conserva la mayor).
    private static decimal AFormaCanonica(decimal monto, Moneda moneda) =>
        decimal.Round(monto, moneda.Precision, MidpointRounding.ToEven)
        + new decimal(0, 0, 0, false, (byte)moneda.Precision);
}
