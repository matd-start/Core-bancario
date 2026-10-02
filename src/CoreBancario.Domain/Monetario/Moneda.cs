namespace CoreBancario.Domain.Monetario;

// SDD: esqueleto creado por test-writer
/// <summary>COP o USD. Conjunto cerrado: el constructor es privado y solo existen estas dos instancias.</summary>
public sealed record Moneda
{
    public static Moneda COP => throw new NotImplementedException();
    public static Moneda USD => throw new NotImplementedException();

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
    public override string ToString() => throw new NotImplementedException();
}
