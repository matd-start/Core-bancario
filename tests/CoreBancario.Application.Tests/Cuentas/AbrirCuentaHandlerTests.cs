using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Tests.Cuentas;

public class AbrirCuentaHandlerTests
{
    private readonly ClienteRepositorioEnMemoria _clientes = new();
    private readonly CuentaRepositorioEnMemoria _cuentas = new();
    private readonly UnidadDeTrabajoEspia _unidad = new();
    private readonly Cliente _cliente = Datos.Cliente();

    public AbrirCuentaHandlerTests() => _clientes.Agregar(_cliente);

    private AbrirCuentaHandler CrearHandler(GeneradorDeSecuencia generador) =>
        new(_clientes, _cuentas, generador, _unidad, Datos.Reloj);

    [Fact]
    public async Task CA06_ClienteRegistrado_AbreCuentaCopActivaConSaldoCeroYFechaDelReloj()
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA));

        // Act
        var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(_cliente.Id, "COP"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resultado.EsExito);
        var dto = resultado.Valor;
        Assert.Equal(_cliente.Id, dto.ClienteId);
        Assert.Equal("1234567897", dto.Numero);
        Assert.Equal("COP", dto.Moneda);
        Assert.Equal("Activa", dto.Estado);
        Assert.Equal(new DineroDto("0", "COP"), dto.Saldo);
        Assert.Equal(Datos.Ahora, dto.FechaApertura);
        Assert.Null(dto.FechaCierre);
        Assert.Single(_cuentas.Cuentas);
        Assert.Equal(1, _unidad.Llamadas);
    }

    [Fact]
    public async Task CA07_ClienteRegistrado_AbreCuentaUsdConSaldoCeroUsd()
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA));

        // Act
        var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(_cliente.Id, "USD"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resultado.EsExito);
        Assert.Equal("USD", resultado.Valor.Moneda);
        Assert.Equal(new DineroDto("0.00", "USD"), resultado.Valor.Saldo);
    }

    [Fact]
    public async Task CL09_ClienteInexistente_LanzaNoEncontradoSinGuardar()
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA));

        // Act
        var accion = () => handler.EjecutarAsync(new AbrirCuentaComando(Guid.NewGuid(), "COP"), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
        Assert.Empty(_cuentas.Cuentas);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("cop")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CL10_MonedaDesconocida_DevuelveErrorDeMonedaSinGuardar(string? moneda)
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA));

        // Act
        var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(_cliente.Id, moneda), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["moneda"], resultado.Errores.Keys);
        Assert.Empty(_cuentas.Cuentas);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CL10_MonedaInvalidaYClienteInexistente_ResponderPrimeroElErrorDeMoneda()
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA));

        // Act
        var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(Guid.NewGuid(), "EUR"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Contains("moneda", resultado.Errores.Keys);
    }

    [Fact]
    public async Task CL11_NumeroRepetido_GeneraOtroSinError()
    {
        // Arrange: el numero A ya existe; el generador devuelve A y luego B.
        _cuentas.Agregar(Datos.Cuenta(_cliente.Id, Datos.NumeroA));
        var generador = new GeneradorDeSecuencia(Datos.NumeroA, Datos.NumeroB);
        var handler = CrearHandler(generador);

        // Act
        var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(_cliente.Id, "COP"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resultado.EsExito);
        Assert.Equal(Datos.NumeroB.Valor, resultado.Valor.Numero);
        Assert.Equal(2, generador.Generaciones);
        Assert.Equal(2, _cuentas.Cuentas.Count);
    }

    [Fact]
    public async Task CL11_CincoNumerosRepetidos_LanzaInvalidOperationSinGuardar()
    {
        // Arrange
        _cuentas.Agregar(Datos.Cuenta(_cliente.Id, Datos.NumeroA));
        var generador = new GeneradorDeSecuencia(Datos.NumeroA);
        var handler = CrearHandler(generador);

        // Act
        var accion = () => handler.EjecutarAsync(new AbrirCuentaComando(_cliente.Id, "COP"), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Equal(AbrirCuentaHandler.MaximoDeIntentos, generador.Generaciones);
        Assert.Single(_cuentas.Cuentas);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CL12_ReintentarApertura_AbreUnaSegundaCuenta()
    {
        // Arrange
        var handler = CrearHandler(new GeneradorDeSecuencia(Datos.NumeroA, Datos.NumeroB));
        var comando = new AbrirCuentaComando(_cliente.Id, "COP");

        // Act
        await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);
        await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert: limite conocido y aceptado (sin idempotencia hasta el S4).
        Assert.Equal(2, _cuentas.Cuentas.Count);
        Assert.Equal(2, _unidad.Llamadas);
    }
}
