namespace CoreBancario.Domain.Cuentas;

/// <summary>
/// RN-18. Valor: 10 dígitos ASCII; el primero no es 0; el último es el dígito verificador Luhn de los 9 primeros.
/// La generación aleatoria no vive aquí: la hace IGeneradorDeNumeroDeCuenta (Application) con DesdeCuerpo (ADR-0013).
/// </summary>
public sealed record NumeroDeCuenta
{
    private const int LongitudDelCuerpo = 9;
    private const int LongitudTotal = LongitudDelCuerpo + 1;

    public string Valor { get; }

    private NumeroDeCuenta(string valor) => Valor = valor;

    /// <summary>Añade a cuerpo (los 9 primeros dígitos) su dígito verificador Luhn. "123456789" → "1234567897".</summary>
    /// <exception cref="ArgumentException">cuerpo no tiene exactamente 9 dígitos ASCII o empieza por 0.</exception>
    public static NumeroDeCuenta DesdeCuerpo(string cuerpo)
    {
        if (!SonDigitos(cuerpo, LongitudDelCuerpo) || cuerpo[0] == '0')
            throw new ArgumentException("El cuerpo debe tener exactamente 9 dígitos y no empezar por 0.", nameof(cuerpo));

        return new NumeroDeCuenta(cuerpo + CalcularVerificador(cuerpo));
    }

    /// <summary>Reconstruye un número ya asignado (persistencia, pruebas) comprobando todas sus reglas.</summary>
    /// <exception cref="ArgumentException">valor no tiene 10 dígitos ASCII, empieza por 0 o su verificador no es correcto.</exception>
    public static NumeroDeCuenta Crear(string valor)
    {
        if (!SonDigitos(valor, LongitudTotal) || valor[0] == '0')
            throw new ArgumentException("El número debe tener exactamente 10 dígitos y no empezar por 0.", nameof(valor));

        var cuerpo = valor[..LongitudDelCuerpo];
        if (valor[LongitudDelCuerpo] != CalcularVerificador(cuerpo))
            throw new ArgumentException("El dígito verificador del número no es correcto.", nameof(valor));

        return new NumeroDeCuenta(valor);
    }

    /// <returns>Valor.</returns>
    public override string ToString() => Valor;

    private static bool SonDigitos(string? texto, int longitud) =>
        texto is not null && texto.Length == longitud && texto.All(char.IsAsciiDigit);

    // Luhn: recorriendo el cuerpo de derecha a izquierda se duplica uno de cada dos dígitos empezando por el
    // último (porque el verificador, que irá a su derecha, ocupará la posición 1 sin duplicar).
    private static char CalcularVerificador(string cuerpo)
    {
        var suma = 0;
        var duplicar = true;

        for (var i = cuerpo.Length - 1; i >= 0; i--)
        {
            var digito = cuerpo[i] - '0';
            if (duplicar)
            {
                digito *= 2;
                if (digito > 9)
                    digito -= 9;
            }

            suma += digito;
            duplicar = !duplicar;
        }

        return (char)('0' + (10 - suma % 10) % 10);
    }
}
