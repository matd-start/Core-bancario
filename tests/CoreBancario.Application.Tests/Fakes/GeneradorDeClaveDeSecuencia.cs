using CoreBancario.Application.AccesosDigitales;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>Devuelve las claves en orden; al agotarlas repite la última.</summary>
public sealed class GeneradorDeClaveDeSecuencia(params ClaveTemporal[] claves) : IGeneradorDeClaveTemporal
{
    public int Generaciones { get; private set; }

    public ClaveTemporal Generar()
    {
        var indice = Math.Min(Generaciones, claves.Length - 1);
        Generaciones++;
        return claves[indice];
    }
}
