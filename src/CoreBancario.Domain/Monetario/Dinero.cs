namespace CoreBancario.Domain.Monetario;

// SDD: esqueleto creado por test-writer
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

    public static Dinero Crear(decimal monto, Moneda moneda) => throw new NotImplementedException();

    public static Dinero DesdeCalculo(decimal resultado, Moneda moneda) => throw new NotImplementedException();

    public Dinero Sumar(Dinero otro) => throw new NotImplementedException();

    public Dinero Restar(Dinero otro) => throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();
}
