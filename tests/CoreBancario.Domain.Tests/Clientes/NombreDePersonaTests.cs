using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.Clientes;

public class NombreDePersonaTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CL06_VacioOSoloEspacios_NoEsValido(string? valor)
    {
        // Arrange / Act
        var creado = NombreDePersona.TryCrear(valor, out var nombre);

        // Assert
        Assert.False(creado);
        Assert.Null(nombre);
    }

    [Theory]
    [InlineData("María José")]
    [InlineData("Núñez")]
    [InlineData("O'Neil")]
    [InlineData("Pérez-Gómez")]
    public void CL06_ConTildesEnieApostrofoYGuion_EsValido(string valor)
    {
        // Arrange / Act
        var creado = NombreDePersona.TryCrear(valor, out var nombre);

        // Assert
        Assert.True(creado);
        Assert.Equal(valor, nombre!.Valor);
    }

    [Theory]
    [InlineData("Maria2")]
    [InlineData("Juan_Perez")]
    [InlineData("Ana@Casa")]
    [InlineData("Luis.")]
    public void CL06_ConDigitosOSimbolos_NoEsValido(string valor)
    {
        // Arrange / Act
        var creado = NombreDePersona.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void CL06_CienCaracteres_EsValido()
    {
        // Arrange
        var valor = new string('a', 100);

        // Act
        var creado = NombreDePersona.TryCrear(valor, out _);

        // Assert
        Assert.True(creado);
    }

    [Fact]
    public void CL06_MasDeCienCaracteres_NoEsValido()
    {
        // Arrange
        var valor = new string('a', 101);

        // Act
        var creado = NombreDePersona.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void CL06_QuitaEspaciosAlInicioYAlFinal()
    {
        // Arrange / Act
        var creado = NombreDePersona.TryCrear("  Ana María  ", out var nombre);

        // Assert
        Assert.True(creado);
        Assert.Equal("Ana María", nombre!.Valor);
    }

    [Theory]
    [InlineData("'")]
    [InlineData("-")]
    [InlineData("' - '")]
    public void CL06_SoloApostrofosYGuiones_NoEsValido(string valor)
    {
        // Arrange / Act
        var creado = NombreDePersona.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void CL06_TextoDescompuesto_SeNormalizaANfc()
    {
        // Arrange: "e" + acento combinado (NFD)
        var descompuesto = "José";

        // Act
        var creado = NombreDePersona.TryCrear(descompuesto, out var nombre);

        // Assert
        Assert.True(creado);
        Assert.Equal("José", nombre!.Valor);
    }

    [Fact]
    public void CL06_CrearConValorInvalido_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => NombreDePersona.Crear("  ");

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void CA01_ToString_DevuelveElValor()
    {
        // Arrange
        var nombre = NombreDePersona.Crear(" Ana ");

        // Act
        var texto = nombre.ToString();

        // Assert
        Assert.Equal("Ana", texto);
    }
}
