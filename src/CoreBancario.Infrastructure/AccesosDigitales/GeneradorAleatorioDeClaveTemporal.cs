using CoreBancario.Application.AccesosDigitales;

namespace CoreBancario.Infrastructure.AccesosDigitales;

/// <summary>RandomNumberGenerator.GetString(Alfabeto, 16). Alfabeto sin caracteres ambiguos (sin 0, O, 1, I, l): ~93 bits.</summary>
// SDD: esqueleto creado por test-writer
public sealed class GeneradorAleatorioDeClaveTemporal : IGeneradorDeClaveTemporal
{
    public const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public ClaveTemporal Generar() => throw new NotImplementedException();
}
