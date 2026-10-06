using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

/// <summary>RN-17. Tipo + número normalizado.</summary>
public sealed record Documento
{
    public TipoDocumento Tipo { get; }
    public string Numero { get; }

    // SDD: esqueleto creado por test-writer
    private Documento(TipoDocumento tipo, string numero) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static bool TryCrear(TipoDocumento tipo, string? numero, [NotNullWhen(true)] out Documento? documento)
        => throw new NotImplementedException();

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    // SDD: esqueleto creado por test-writer
    public static Documento Crear(TipoDocumento tipo, string numero) => throw new NotImplementedException();

    /// <returns>"CC 1234567".</returns>
    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
