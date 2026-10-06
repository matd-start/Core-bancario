using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Monetario;

public class MonedaTests
{
    [Fact]
    public void RN04_MonedaCop_TienePrecisionCero()
    {
        // Arrange
        var moneda = Moneda.COP;

        // Act
        var precision = moneda.Precision;

        // Assert
        Assert.Equal(0, precision);
        Assert.Equal("COP", moneda.Codigo);
    }

    [Fact]
    public void RN04_MonedaUsd_TienePrecisionDos()
    {
        // Arrange
        var moneda = Moneda.USD;

        // Act
        var precision = moneda.Precision;

        // Assert
        Assert.Equal(2, precision);
        Assert.Equal("USD", moneda.Codigo);
    }

    [Fact]
    public void RN04_MonedaToString_DevuelveElCodigo()
    {
        // Arrange
        var moneda = Moneda.USD;

        // Act
        var texto = moneda.ToString();

        // Assert
        Assert.Equal("USD", texto);
    }

    // ---- CL-10 ----

    [Theory]
    [InlineData("EUR")]
    [InlineData("cop")]
    [InlineData("usd")]
    [InlineData("")]
    [InlineData(" COP")]
    [InlineData(null)]
    public void CL10_TryDesdeCodigoDesconocido_DevuelveFalse(string? codigo)
    {
        // Arrange / Act
        var encontrada = Moneda.TryDesdeCodigo(codigo, out var moneda);

        // Assert
        Assert.False(encontrada);
        Assert.Null(moneda);
    }

    [Theory]
    [InlineData("COP")]
    [InlineData("USD")]
    public void CL10_TryDesdeCodigoValido_DevuelveLaMoneda(string codigo)
    {
        // Arrange / Act
        var encontrada = Moneda.TryDesdeCodigo(codigo, out var moneda);

        // Assert
        Assert.True(encontrada);
        Assert.Equal(codigo, moneda!.Codigo);
    }

    [Fact]
    public void CL10_DesdeCodigoValido_DevuelveLaMismaInstancia()
    {
        // Arrange / Act
        var cop = Moneda.DesdeCodigo("COP");
        var usd = Moneda.DesdeCodigo("USD");

        // Assert
        Assert.Same(Moneda.COP, cop);
        Assert.Same(Moneda.USD, usd);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("cop")]
    [InlineData("")]
    [InlineData(null)]
    public void CL10_DesdeCodigoDesconocido_LanzaArgumentException(string? codigo)
    {
        // Arrange / Act
        var accion = () => Moneda.DesdeCodigo(codigo!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }
}
