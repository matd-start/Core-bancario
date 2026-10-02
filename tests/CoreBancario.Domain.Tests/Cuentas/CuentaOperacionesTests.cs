using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Cuentas;

public class CuentaOperacionesTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    private static Dinero Usd(decimal monto) => Dinero.Crear(monto, Moneda.USD);

    private static Cuenta CuentaActivaConSaldo(decimal pesos)
    {
        var cuenta = Cuenta.Abrir("001-0001", Guid.NewGuid(), Moneda.COP);
        cuenta.Acreditar(Cop(pesos), Instante);
        return cuenta;
    }

    private static Cuenta CuentaBloqueadaConSaldo(decimal pesos)
    {
        var cuenta = CuentaActivaConSaldo(pesos);
        cuenta.Bloquear();
        return cuenta;
    }

    private static Cuenta CuentaBloqueadaSinSaldo()
    {
        var cuenta = Cuenta.Abrir("001-0003", Guid.NewGuid(), Moneda.COP);
        cuenta.Bloquear();
        return cuenta;
    }

    private static Cuenta CuentaCerrada()
    {
        var cuenta = Cuenta.Abrir("001-0002", Guid.NewGuid(), Moneda.COP);
        cuenta.Cerrar();
        return cuenta;
    }

    // ---- CA-10 ----

    [Fact]
    public void CA10_AbrirCuentaCop_NaceActiva()
    {
        // Arrange / Act
        var cuenta = Cuenta.Abrir("001-0001", Guid.NewGuid(), Moneda.COP);

        // Assert
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void CA10_AbrirCuentaCop_NaceConSaldoCeroEnSuMoneda()
    {
        // Arrange / Act
        var cuenta = Cuenta.Abrir("001-0001", Guid.NewGuid(), Moneda.COP);

        // Assert
        Assert.Equal(Cop(0m), cuenta.Saldo);
        Assert.Equal(Moneda.COP, cuenta.Moneda);
    }

    [Fact]
    public void CA10_AbrirCuentaUsd_NaceConSaldoCeroEnDolares()
    {
        // Arrange / Act
        var cuenta = Cuenta.Abrir("001-0003", Guid.NewGuid(), Moneda.USD);

        // Assert
        Assert.Equal(Usd(0m), cuenta.Saldo);
        Assert.Equal(Moneda.USD, cuenta.Moneda);
    }

    [Fact]
    public void CA10_AbrirCuenta_ConservaNumeroYCliente()
    {
        // Arrange
        var clienteId = Guid.NewGuid();

        // Act
        var cuenta = Cuenta.Abrir("001-0001", clienteId, Moneda.COP);

        // Assert
        Assert.Equal("001-0001", cuenta.Numero);
        Assert.Equal(clienteId, cuenta.ClienteId);
        Assert.NotEqual(Guid.Empty, cuenta.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ADR0008_AbrirSinNumero_LanzaArgumentException(string? numero)
    {
        // Arrange / Act
        var accion = () => Cuenta.Abrir(numero!, Guid.NewGuid(), Moneda.COP);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void ADR0008_AbrirSinMoneda_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cuenta.Abrir("001-0001", Guid.NewGuid(), null!);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // ---- CA-11 ----

    [Fact]
    public void CA11_AcreditarCuentaActiva_SumaAlSaldo()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        cuenta.Acreditar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(Cop(70_000m), cuenta.Saldo);
    }

    [Fact]
    public void ADR0008_AcreditarConMontoNulo_LanzaArgumentNullException()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Acreditar(null!, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // ---- CA-12 ----

    [Fact]
    public void CA12_DebitarCuentaActiva_RestaDelSaldo()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        cuenta.Debitar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(Cop(30_000m), cuenta.Saldo);
    }

    [Fact]
    public void ADR0008_DebitarConMontoNulo_LanzaArgumentNullException()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(null!, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // ---- CA-13 / CL-07 ----

    [Fact]
    public void CA13_DebitarTodoElSaldo_DejaSaldoCero()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        cuenta.Debitar(Cop(50_000m), Instante);

        // Assert
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    // ---- CA-14 / CL-08 ----

    [Fact]
    public void CA14_DebitarMasQueElSaldo_LanzaSaldoInsuficienteSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(80_000m), Instante);

        // Assert
        Assert.Throws<SaldoInsuficienteException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void RN01_DebitarMasQueElSaldo_LanzaSaldoInsuficienteException()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(1m);

        // Act
        var accion = () => cuenta.Debitar(Cop(2m), Instante);

        // Assert
        Assert.Throws<SaldoInsuficienteException>(accion);
    }

    // ---- CA-15 / CL-06 ----

    [Fact]
    public void CA15_AcreditarCero_LanzaMontoNoPositivoSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Acreditar(Cop(0m), Instante);

        // Assert
        Assert.Throws<MontoNoPositivoException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void CA15_DebitarCero_LanzaMontoNoPositivoSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(0m), Instante);

        // Assert
        Assert.Throws<MontoNoPositivoException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    // ---- CA-16 / CL-04 ----

    [Fact]
    public void CA16_AcreditarEnOtraMoneda_LanzaMonedasDistintasSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Acreditar(Usd(10.00m), Instante);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void CA16_DebitarEnOtraMoneda_LanzaMonedasDistintasSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Usd(10.00m), Instante);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    // ---- CA-17 / CL-09 ----

    [Fact]
    public void CA17_AcreditarCuentaBloqueada_AumentaElSaldo()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();

        // Act
        cuenta.Acreditar(Cop(10_000m), Instante);

        // Assert
        Assert.Equal(Cop(10_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
    }

    [Fact]
    public void CA17_AcreditarCuentaBloqueada_DevuelveMovimientoDeCredito()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();

        // Act
        var movimiento = cuenta.Acreditar(Cop(10_000m), Instante);

        // Assert
        Assert.Equal(TipoMovimiento.Credito, movimiento.Tipo);
        Assert.Equal(Cop(10_000m), movimiento.Monto);
    }

    // ---- CA-18 / CL-09 ----

    [Fact]
    public void CA18_DebitarCuentaBloqueada_LanzaOperacionNoPermitidaSinCambios()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(10_000m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
    }

    // ---- CA-19 / CL-10 ----

    [Fact]
    public void CA19_AcreditarCuentaCerrada_LanzaOperacionNoPermitidaSinCambios()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Acreditar(Cop(10_000m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(0m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    [Fact]
    public void CA19_DebitarCuentaCerrada_LanzaOperacionNoPermitidaSinCambios()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Debitar(Cop(10_000m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(0m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    // ---- CA-27 / CL-12 ----

    [Fact]
    public void CA27_DebitarBloqueadaMasQueElSaldo_LanzaOperacionNoPermitida()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(80_000m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
    }

    [Fact]
    public void CL12_AcreditarCerradaEnOtraMoneda_LanzaOperacionNoPermitida()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Acreditar(Usd(10.00m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(0m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    [Fact]
    public void CL12_DebitarCeroEnOtraMoneda_LanzaMonedasDistintas()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Usd(0m), Instante);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void CL12_DebitarEnOtraMonedaMasQueElSaldo_LanzaMonedasDistintas()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Usd(80_000.00m), Instante);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void CL12_DebitarCeroConCuentaBloqueada_LanzaOperacionNoPermitida()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(0m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
    }

    [Fact]
    public void CL12_DebitarCeroMasQueNingunSaldo_LanzaMontoNoPositivo()
    {
        // Arrange: el monto cero se informa antes que el saldo (cuenta en cero).
        var cuenta = Cuenta.Abrir("001-0004", Guid.NewGuid(), Moneda.COP);

        // Act
        var accion = () => cuenta.Debitar(Cop(0m), Instante);

        // Assert
        Assert.Throws<MontoNoPositivoException>(accion);
    }

    // ---- Pruebas explícitas por regla de negocio ----

    [Fact]
    public void RN02_OperarLaCuenta_NoCambiaSuMoneda()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        cuenta.Acreditar(Cop(10_000m), Instante);
        cuenta.Debitar(Cop(5_000m), Instante);

        // Assert
        Assert.Equal(Moneda.COP, cuenta.Moneda);
        Assert.Equal(Moneda.COP, cuenta.Saldo.Moneda);
    }

    [Fact]
    public void RN03_SumarCopYUsd_LanzaMonedasDistintas()
    {
        // Arrange
        var pesos = Cop(1_000m);
        var dolares = Usd(1m);

        // Act
        var accion = () => pesos.Sumar(dolares);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
    }

    [Fact]
    public void RN05_CuentaBloqueada_AceptaCredito()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        cuenta.Acreditar(Cop(10_000m), Instante);

        // Assert
        Assert.Equal(Cop(60_000m), cuenta.Saldo);
    }

    [Fact]
    public void RN05_CuentaBloqueada_RechazaDebitoSinCambios()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Debitar(Cop(10_000m), Instante);

        // Assert
        Assert.Throws<OperacionNoPermitidaException>(accion);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
    }

    [Fact]
    public void RN10_MovimientoEmitido_NoCambiaTrasOperacionesPosteriores()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);
        var movimiento = cuenta.Acreditar(Cop(20_000m), Instante);

        // Act
        cuenta.Debitar(Cop(30_000m), Instante);
        cuenta.Acreditar(Cop(1_000m), Instante);

        // Assert
        Assert.Equal(TipoMovimiento.Credito, movimiento.Tipo);
        Assert.Equal(Cop(20_000m), movimiento.Monto);
        Assert.Equal(Cop(70_000m), movimiento.SaldoResultante);
        Assert.Equal(Instante, movimiento.FechaHora);
    }
}
