using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Tests.Cuentas;

public class CambiarEstadoDeCuentaHandlerTests
{
    private readonly CuentaRepositorioEnMemoria _cuentas = new();
    private readonly UnidadDeTrabajoEspia _unidad = new();

    private CambiarEstadoDeCuentaHandler CrearHandler(UnidadDeTrabajoEspia? unidad = null) =>
        new(_cuentas, unidad ?? _unidad, Datos.Reloj);

    private Cuenta CuentaActiva()
    {
        var cuenta = Datos.Cuenta(Guid.NewGuid());
        _cuentas.Agregar(cuenta);
        return cuenta;
    }

    private Cuenta CuentaBloqueada()
    {
        var cuenta = CuentaActiva();
        cuenta.Bloquear();
        return cuenta;
    }

    [Fact]
    public async Task CA10_BloquearCuentaActiva_QuedaBloqueadaYGuarda()
    {
        // Arrange
        var cuenta = CuentaActiva();
        var handler = CrearHandler();

        // Act
        var dto = await handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, AccionDeEstado.Bloquear), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Bloqueada", dto.Estado);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(1, _unidad.Llamadas);
    }

    [Fact]
    public async Task CA10_DesbloquearCuentaBloqueada_VuelveAActiva()
    {
        // Arrange
        var cuenta = CuentaBloqueada();
        var handler = CrearHandler();

        // Act
        var dto = await handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, AccionDeEstado.Desbloquear), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Activa", dto.Estado);
        Assert.Equal(1, _unidad.Llamadas);
    }

    [Theory]
    [InlineData(AccionDeEstado.Cerrar)]
    [InlineData(AccionDeEstado.Bloquear)]
    public async Task CA11_CerrarOBloquearCuentaBloqueada_LanzaTransicionNoPermitidaSinGuardar(AccionDeEstado accion)
    {
        // Arrange
        var cuenta = CuentaBloqueada();
        var handler = CrearHandler();

        // Act
        var ejecutar = () => handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, accion), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<TransicionNoPermitidaException>(ejecutar);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CA12_CerrarCuentaActivaSinSaldo_QuedaCerradaConLaFechaDelReloj()
    {
        // Arrange
        var cuenta = CuentaActiva();
        var handler = CrearHandler();

        // Act
        var dto = await handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, AccionDeEstado.Cerrar), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Cerrada", dto.Estado);
        Assert.Equal(Datos.Ahora, dto.FechaCierre);
        Assert.Equal(1, _unidad.Llamadas);
    }

    [Theory]
    [InlineData(AccionDeEstado.Bloquear)]
    [InlineData(AccionDeEstado.Desbloquear)]
    [InlineData(AccionDeEstado.Cerrar)]
    public async Task CA12_CambiarEstadoDeCuentaCerrada_LanzaTransicionNoPermitidaSinGuardar(AccionDeEstado accion)
    {
        // Arrange
        var cuenta = CuentaActiva();
        cuenta.Cerrar(Datos.Ahora);
        var handler = CrearHandler();

        // Act
        var ejecutar = () => handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, accion), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<TransicionNoPermitidaException>(ejecutar);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CA13_CerrarCuentaConSaldo_LanzaSaldoDistintoDeCeroSinGuardar()
    {
        // Arrange
        var cuenta = CuentaActiva();
        cuenta.Acreditar(Dinero.Crear(1_000m, Moneda.COP), Datos.Ahora);
        var handler = CrearHandler();

        // Act
        var ejecutar = () => handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, AccionDeEstado.Cerrar), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<SaldoDistintoDeCeroException>(ejecutar);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Null(cuenta.FechaCierre);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Theory]
    [InlineData(AccionDeEstado.Bloquear)]
    [InlineData(AccionDeEstado.Desbloquear)]
    [InlineData(AccionDeEstado.Cerrar)]
    public async Task CL16_CuentaInexistente_LanzaRecursoNoEncontrado(AccionDeEstado accion)
    {
        // Arrange
        var handler = CrearHandler();

        // Act
        var ejecutar = () => handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(Guid.NewGuid(), accion), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(ejecutar);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CL15_ConflictoAlGuardar_SePropagaYNoSeReintenta()
    {
        // Arrange
        var cuenta = CuentaActiva();
        var unidad = new UnidadDeTrabajoEspia { ExcepcionAlGuardar = new ConflictoDeConcurrenciaException() };
        var handler = CrearHandler(unidad);

        // Act
        var ejecutar = () => handler.EjecutarAsync(
            new CambiarEstadoDeCuentaComando(cuenta.Id, AccionDeEstado.Bloquear), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(ejecutar);
        Assert.Equal(1, unidad.Llamadas);
    }
}
