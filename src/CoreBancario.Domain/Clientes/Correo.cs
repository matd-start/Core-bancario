using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

public sealed record Correo
{
    public string Valor { get; }

    // SDD: esqueleto creado por test-writer
    private Correo(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Correo? correo) => throw new NotImplementedException();

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    // SDD: esqueleto creado por test-writer
    public static Correo Crear(string valor) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
