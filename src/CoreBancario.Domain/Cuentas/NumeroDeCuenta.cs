namespace CoreBancario.Domain.Cuentas;

/// <summary>
/// RN-18. Valor: 10 dígitos ASCII; el primero no es 0; el último es el dígito verificador Luhn de los 9 primeros.
/// </summary>
public sealed record NumeroDeCuenta
{
    public string Valor { get; }

    // SDD: esqueleto creado por test-writer
    private NumeroDeCuenta(string valor) => throw new NotImplementedException();

    /// <summary>Añade a cuerpo (los 9 primeros dígitos) su dígito verificador Luhn.</summary>
    /// <exception cref="ArgumentException">cuerpo no tiene exactamente 9 dígitos ASCII o empieza por 0.</exception>
    // SDD: esqueleto creado por test-writer
    public static NumeroDeCuenta DesdeCuerpo(string cuerpo) => throw new NotImplementedException();

    /// <summary>Reconstruye un número ya asignado comprobando todas sus reglas.</summary>
    /// <exception cref="ArgumentException">valor no tiene 10 dígitos ASCII, empieza por 0 o su verificador no es correcto.</exception>
    // SDD: esqueleto creado por test-writer
    public static NumeroDeCuenta Crear(string valor) => throw new NotImplementedException();

    /// <returns>Valor.</returns>
    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
