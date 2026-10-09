using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.Clientes;

public class TelefonoTests
{
    [Theory]
    [InlineData("+57 (300) 123-4567", "+573001234567")]
    [InlineData("+57 300 123 4567", "+573001234567")]
    [InlineData("+573001234567", "+573001234567")]
    [InlineData(" +1 (415) 555-2671 ", "+14155552671")]
    public void F002_CA01_TelefonoConEspaciosGuionesYParentesis_SeNormaliza(string escrito, string esperado)
    {
        // Arrange / Act
        var creado = Telefono.TryCrear(escrito, out var telefono);

        // Assert
        Assert.True(creado);
        Assert.Equal(esperado, telefono!.Valor);
    }

    [Theory]
    [InlineData("3001234567")]
    [InlineData("573001234567")]
    [InlineData("300 123 4567")]
    public void F002_CL07_SinCodigoDePais_NoEsValido(string valor)
    {
        // Arrange / Act
        var creado = Telefono.TryCrear(valor, out var telefono);

        // Assert
        Assert.False(creado);
        Assert.Null(telefono);
    }

    [Theory]
    [InlineData("+1234567")]
    [InlineData("+")]
    [InlineData("+1234567890123456")]
    public void F002_CL07_FueraDeRango_NoEsValido(string valor)
    {
        // Arrange / Act
        var creado = Telefono.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData("+12345678")]
    [InlineData("+123456789012345")]
    public void F002_CL07_EnLosLimitesDeRango_EsValido(string valor)
    {
        // Arrange / Act
        var creado = Telefono.TryCrear(valor, out _);

        // Assert
        Assert.True(creado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+57300abc4567")]
    [InlineData("+57.300.123.4567")]
    [InlineData("++573001234567")]
    public void F002_CL07_AusenteOConCaracteresInvalidos_NoEsValido(string? valor)
    {
        // Arrange / Act
        var creado = Telefono.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void F002_CL07_CrearConValorInvalido_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => Telefono.Crear("3001234567");

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F002_CA01_ToString_DevuelveElValorNormalizado()
    {
        // Arrange
        var telefono = Telefono.Crear("+57 (300) 123-4567");

        // Act
        var texto = telefono.ToString();

        // Assert
        Assert.Equal("+573001234567", texto);
    }
}
