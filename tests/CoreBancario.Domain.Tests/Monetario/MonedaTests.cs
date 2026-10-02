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
}
