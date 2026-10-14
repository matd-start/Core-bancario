using System.Security.Cryptography;
using CoreBancario.Application.AccesosDigitales;

namespace CoreBancario.Infrastructure.AccesosDigitales;

/// <summary>RandomNumberGenerator.GetString(Alfabeto, 16). Alfabeto sin caracteres ambiguos (sin 0, O, 1, I, l): ~93 bits.</summary>
public sealed class GeneradorAleatorioDeClaveTemporal : IGeneradorDeClaveTemporal
{
    public const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    private const int Longitud = 16;

    public ClaveTemporal Generar() => new(RandomNumberGenerator.GetString(Alfabeto, Longitud));
}
