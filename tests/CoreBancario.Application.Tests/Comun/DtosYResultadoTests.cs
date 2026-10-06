using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Tests.Comun;

public class DtosYResultadoTests
{
    // ---- DineroDto: el monto viaja como texto en forma canonica ----

    [Theory]
    [InlineData(0, "0")]
    [InlineData(50000, "50000")]
    [InlineData(1234567, "1234567")]
    public void CA06_DineroDtoEnCop_NoLlevaDecimales(int pesos, string esperado)
    {
        // Arrange
        var dinero = Dinero.Crear(pesos, Moneda.COP);

        // Act
        var dto = DineroDto.Desde(dinero);

        // Assert
        Assert.Equal(new DineroDto(esperado, "COP"), dto);
    }

    [Theory]
    [InlineData("0", "0.00")]
    [InlineData("10.5", "10.50")]
    [InlineData("1234.56", "1234.56")]
    public void CA07_DineroDtoEnUsd_LlevaSiempreDosDecimalesConPunto(string monto, string esperado)
    {
        // Arrange
        var dinero = Dinero.Crear(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture), Moneda.USD);

        // Act
        var dto = DineroDto.Desde(dinero);

        // Assert
        Assert.Equal(new DineroDto(esperado, "USD"), dto);
    }

    // ---- ClienteDto y CuentaDto ----

    [Fact]
    public void CA01_ClienteDto_CopiaLosDatosNormalizadosDelCliente()
    {
        // Arrange
        var cliente = Datos.Cliente("1234567");

        // Act
        var dto = ClienteDto.Desde(cliente);

        // Assert
        Assert.Equal(cliente.Id, dto.Id);
        Assert.Equal("CC", dto.TipoDocumento);
        Assert.Equal("1234567", dto.NumeroDocumento);
        Assert.Equal("María José", dto.Nombres);
        Assert.Equal("Núñez", dto.Apellidos);
        Assert.Equal("maria@example.com", dto.Correo);
        Assert.Equal("+573001234567", dto.Telefono);
        Assert.Equal(cliente.FechaRegistro, dto.FechaRegistro);
    }

    [Fact]
    public void CA17_CuentaDto_CopiaLosDatosDeLaCuentaConElSaldoComoTexto()
    {
        // Arrange
        var cuenta = Datos.Cuenta(Guid.NewGuid(), moneda: Moneda.USD);

        // Act
        var dto = CuentaDto.Desde(cuenta);

        // Assert
        Assert.Equal(cuenta.Id, dto.Id);
        Assert.Equal("1234567897", dto.Numero);
        Assert.Equal(cuenta.ClienteId, dto.ClienteId);
        Assert.Equal("USD", dto.Moneda);
        Assert.Equal("Activa", dto.Estado);
        Assert.Equal(new DineroDto("0.00", "USD"), dto.Saldo);
        Assert.Equal(cuenta.FechaApertura, dto.FechaApertura);
        Assert.Null(dto.FechaCierre);
    }

    // ---- Resultado<T> ----

    [Fact]
    public void RNF03_ResultadoExito_ExponeElValorYNoTieneErrores()
    {
        // Arrange / Act
        var resultado = Resultado<int>.Exito(42);

        // Assert
        Assert.True(resultado.EsExito);
        Assert.Equal(42, resultado.Valor);
        Assert.Empty(resultado.Errores);
    }

    [Fact]
    public void RNF03_ResultadoInvalido_ExponeTodosLosErroresYNoEsExito()
    {
        // Arrange
        var errores = new Dictionary<string, string[]>
        {
            ["correo"] = ["inválido"],
            ["telefono"] = ["inválido"],
        };

        // Act
        var resultado = Resultado<int>.Invalido(errores);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["correo", "telefono"], resultado.Errores.Keys.Order());
    }

    [Fact]
    public void RNF03_LeerElValorDeUnResultadoInvalido_LanzaInvalidOperationException()
    {
        // Arrange
        var resultado = Resultado<int>.Invalido(new Dictionary<string, string[]> { ["correo"] = ["inválido"] });

        // Act
        Action accion = () => _ = resultado.Valor;

        // Assert
        Assert.Throws<InvalidOperationException>(accion);
    }

    [Fact]
    public void RNF03_ResultadoInvalidoSinErrores_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => Resultado<int>.Invalido(new Dictionary<string, string[]>());

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }
}
