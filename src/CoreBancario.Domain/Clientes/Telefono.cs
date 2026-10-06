using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

public sealed record Telefono
{
    public string Valor { get; }

    // SDD: esqueleto creado por test-writer
    private Telefono(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Telefono? telefono) => throw new NotImplementedException();

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    // SDD: esqueleto creado por test-writer
    public static Telefono Crear(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
