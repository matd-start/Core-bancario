using System.Diagnostics.CodeAnalysis;

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

    /// <summary>Devuelve COP o USD según el código exacto ("COP", "USD"; sensible a mayúsculas). Lo usa la persistencia.</summary>
    /// <exception cref="ArgumentException">codigo es null, vacío o no es "COP" ni "USD".</exception>
    // SDD: esqueleto creado por test-writer
    public static Moneda DesdeCodigo(string codigo) => throw new NotImplementedException();

    /// <summary>Versión sin excepciones para validar la entrada (CL-10).</summary>
    // SDD: esqueleto creado por test-writer
    public static bool TryDesdeCodigo(string? codigo, [NotNullWhen(true)] out Moneda? moneda) => throw new NotImplementedException();

    /// <returns>El código, por ejemplo "COP".</returns>
    public override string ToString() => Codigo;
}
