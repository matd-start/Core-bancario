using CoreBancario.Infrastructure.AccesosDigitales;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// RNF-03 y CA-02: el generador de claves temporales. El sistema elige claves aleatorias y uniformes para todos
/// (decisión del autor en la spec): el operador no inventa claves.
/// </summary>
public class GeneradorDeClaveTemporalTests
{
    private const string CaracteresAmbiguos = "0O1Il";

    private readonly GeneradorAleatorioDeClaveTemporal _generador = new();

    [Fact]
    public void F003_RNF03_ClaveTemporalGenerada_Tiene16CaracteresAlfanumericos()
    {
        // Arrange / Act
        var clave = _generador.Generar().Valor;

        // Assert
        Assert.Equal(16, clave.Length);
        Assert.All(clave, caracter => Assert.True(char.IsAsciiLetterOrDigit(caracter), $"Carácter inesperado: {caracter}"));
    }

    [Fact]
    public void F003_RNF03_ClaveTemporalGenerada_PerteneceAlAlfabetoSinCaracteresAmbiguos()
    {
        // Arrange: el operador la dicta o la lee en voz alta; 0/O y 1/I/l no se distinguen.
        var claves = Enumerable.Range(0, 200).Select(_ => _generador.Generar().Valor).ToList();

        // Act
        var conAmbiguos = claves.Where(clave => clave.IndexOfAny(CaracteresAmbiguos.ToCharArray()) >= 0).ToList();

        // Assert
        Assert.Empty(conAmbiguos);
        Assert.All(claves, clave => Assert.All(clave, c => Assert.True(GeneradorAleatorioDeClaveTemporal.Alfabeto.Contains(c))));
    }

    [Fact]
    public void F003_CA02_ClavesGeneradas_SonDistintas()
    {
        // Arrange / Act
        var claves = Enumerable.Range(0, 100).Select(_ => _generador.Generar().Valor).ToList();

        // Assert
        Assert.Equal(claves.Count, claves.Distinct().Count());
    }
}
