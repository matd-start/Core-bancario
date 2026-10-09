using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Domain.Tests.Cuentas;

public class NumeroDeCuentaTests
{
    [Theory]
    [InlineData("123456789", "1234567897")]
    [InlineData("100000000", "1000000008")]
    [InlineData("987654321", "9876543217")]
    public void F002_RN18_DesdeCuerpo_AnadeElDigitoVerificadorLuhn(string cuerpo, string esperado)
    {
        // Arrange / Act
        var numero = NumeroDeCuenta.DesdeCuerpo(cuerpo);

        // Assert
        Assert.Equal(esperado, numero.Valor);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("")]
    [InlineData("12345678a")]
    [InlineData("012345678")]
    public void F002_RN18_DesdeCuerpoInvalido_LanzaArgumentException(string cuerpo)
    {
        // Arrange / Act
        var accion = () => NumeroDeCuenta.DesdeCuerpo(cuerpo);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Theory]
    [InlineData("1234567897")]
    [InlineData("1000000008")]
    [InlineData("9876543217")]
    public void F002_RN18_CrearConNumeroValido_ConservaElValor(string valor)
    {
        // Arrange / Act
        var numero = NumeroDeCuenta.Crear(valor);

        // Assert
        Assert.Equal(valor, numero.Valor);
    }

    [Theory]
    [InlineData("1234567890")]   // verificador incorrecto
    [InlineData("2134567897")]   // transposición de los dos primeros dígitos
    [InlineData("0234567899")]   // Luhn correcto pero empieza por 0
    [InlineData("123456789")]    // 9 dígitos
    [InlineData("12345678a7")]   // no es dígito
    [InlineData("12345678971")]  // 11 dígitos
    [InlineData("")]
    public void F002_RN18_CrearConNumeroInvalido_LanzaArgumentException(string valor)
    {
        // Arrange / Act
        var accion = () => NumeroDeCuenta.Crear(valor);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F002_RN18_CrearConNull_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => NumeroDeCuenta.Crear(null!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F002_RN18_DosNumerosConElMismoValor_SonIguales()
    {
        // Arrange
        var primero = NumeroDeCuenta.Crear("1234567897");
        var segundo = NumeroDeCuenta.DesdeCuerpo("123456789");

        // Act
        var iguales = primero == segundo;

        // Assert
        Assert.True(iguales);
    }

    [Fact]
    public void F002_RN18_ToString_DevuelveElValor()
    {
        // Arrange
        var numero = NumeroDeCuenta.Crear("1234567897");

        // Act
        var texto = numero.ToString();

        // Assert
        Assert.Equal("1234567897", texto);
    }
}
