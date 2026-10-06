using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>Devuelve los numeros en orden; al agotarlos repite el ultimo.</summary>
public sealed class GeneradorDeSecuencia(params NumeroDeCuenta[] numeros) : IGeneradorDeNumeroDeCuenta
{
    public int Generaciones { get; private set; }

    public NumeroDeCuenta Generar()
    {
        var indice = Math.Min(Generaciones, numeros.Length - 1);
        Generaciones++;
        return numeros[indice];
    }
}
