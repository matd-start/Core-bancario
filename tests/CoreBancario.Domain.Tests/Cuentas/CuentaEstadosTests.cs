using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Cuentas;

public class CuentaEstadosTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    private static Cuenta CuentaActivaSinSaldo() => Cuenta.Abrir("001-0001", Guid.NewGuid(), Moneda.COP);

    private static Cuenta CuentaActivaConSaldo(decimal pesos)
    {
        var cuenta = CuentaActivaSinSaldo();
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
        var cuenta = CuentaActivaSinSaldo();
        cuenta.Bloquear();
        return cuenta;
    }

    private static Cuenta CuentaCerrada()
    {
        var cuenta = CuentaActivaSinSaldo();
        cuenta.Cerrar();
        return cuenta;
    }

    // ---- CA-20 ----

    [Fact]
    public void CA20_BloquearCuentaActiva_QuedaBloqueada()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        cuenta.Bloquear();

        // Assert
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
    }

    [Fact]
    public void CA20_DesbloquearCuentaBloqueada_QuedaActiva()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        cuenta.Desbloquear();

        // Assert
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
    }

    // ---- CA-21 ----

    [Fact]
    public void CA21_CerrarCuentaActivaConSaldoCero_QuedaCerrada()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();

        // Act
        cuenta.Cerrar();

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    // ---- CA-22 ----

    [Fact]
    public void CA22_CerrarCuentaActivaConUnPeso_LanzaSaldoDistintoDeCeroYSigueActiva()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(1m);

        // Act
        var accion = () => cuenta.Cerrar();

        // Assert
        Assert.Throws<SaldoDistintoDeCeroException>(accion);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(Cop(1m), cuenta.Saldo);
    }

    // ---- CA-23 / CL-11 ----

    [Fact]
    public void CA23_CerrarCuentaBloqueada_LanzaTransicionNoPermitidaYSigueBloqueada()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();

        // Act
        var accion = () => cuenta.Cerrar();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    // ---- CA-24 / CL-11 ----

    [Fact]
    public void CA24_BloquearBloqueada_LanzaTransicionNoPermitidaSinCambios()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Bloquear();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
    }

    [Fact]
    public void CA24_DesbloquearActiva_LanzaTransicionNoPermitidaSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var accion = () => cuenta.Desbloquear();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(Cop(50_000m), cuenta.Saldo);
    }

    // ---- CA-25 / CL-10 ----

    [Fact]
    public void CA25_BloquearCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Bloquear();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    [Fact]
    public void CA25_DesbloquearCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Desbloquear();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    [Fact]
    public void CA25_CerrarCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Cerrar();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    // ---- CA-26 / CL-12 ----

    [Fact]
    public void CA26_CerrarBloqueadaConSaldo_LanzaTransicionNoPermitida()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(10_000m);

        // Act
        var accion = () => cuenta.Cerrar();

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(10_000m), cuenta.Saldo);
    }

    // ---- RN-14: la cuenta desbloqueada puede cerrarse (camino completo) ----

    [Fact]
    public void RN14_BloquearDesbloquearYCerrarConSaldoCero_QuedaCerrada()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();
        cuenta.Desbloquear();

        // Act
        cuenta.Cerrar();

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }
}
