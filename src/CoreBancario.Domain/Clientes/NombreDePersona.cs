using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

public sealed record NombreDePersona
{
    public string Valor { get; }

    // SDD: esqueleto creado por test-writer
    private NombreDePersona(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out NombreDePersona? nombre) => throw new NotImplementedException();

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    // SDD: esqueleto creado por test-writer
    public static NombreDePersona Crear(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
