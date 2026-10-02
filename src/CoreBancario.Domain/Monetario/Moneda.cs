namespace CoreBancario.Domain.Monetario;

/// <summary>COP o USD. Conjunto cerrado: el constructor es privado y solo existen estas dos instancias.</summary>
public sealed record Moneda
{
    public static Moneda COP { get; } = new("COP", 0);
    public static Moneda USD { get; } = new("USD", 2);

    /// <summary>Código ISO 4217.</summary>
    public string Codigo { get; }

    /// <summary>Número de decimales que admite la moneda. COP: 0. USD: 2.</summary>
    public int Precision { get; }

    private Moneda(string codigo, int precision)
    {
        Codigo = codigo;
        Precision = precision;
    }

    /// <returns>El código, por ejemplo "COP".</returns>
    public override string ToString() => Codigo;
}
