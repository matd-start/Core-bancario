using System.Globalization;
using System.Security.Cryptography;
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Infrastructure.Cuentas;

/// <summary>
/// RN-18. Un número aleatorio de 9 dígitos (el primero no es 0) al que el dominio añade el dígito verificador.
/// Se usa un generador criptográfico para que el siguiente número no se pueda predecir a partir de los anteriores.
/// </summary>
public sealed class GeneradorAleatorioDeNumeroDeCuenta : IGeneradorDeNumeroDeCuenta
{
    public NumeroDeCuenta Generar()
    {
        var cuerpo = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
        return NumeroDeCuenta.DesdeCuerpo(cuerpo.ToString(CultureInfo.InvariantCulture));
    }
}
