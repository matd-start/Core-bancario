using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Cuentas;

public class CuentaEstadosTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static NumeroDeCuenta Numero() => NumeroDeCuenta.Crear("1234567897");

    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    private static Cuenta CuentaActivaSinSaldo() => Cuenta.Abrir(Numero(), Guid.NewGuid(), Moneda.COP, Instante);

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
        cuenta.Cerrar(Instante);
        return cuenta;
    }

    // ---- CA-20 ----

    [Fact]
    public void F001_CA20_BloquearCuentaActiva_QuedaBloqueada()
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
    public void F001_CA20_DesbloquearCuentaBloqueada_QuedaActiva()
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
    public void F001_CA21_CerrarCuentaActivaConSaldoCero_QuedaCerrada()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();

        // Act
        cuenta.Cerrar(Instante);

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    // ---- CA-22 ----

    [Fact]
    public void F001_CA22_CerrarCuentaActivaConUnPeso_LanzaSaldoDistintoDeCeroYSigueActiva()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(1m);

        // Act
        var accion = () => cuenta.Cerrar(Instante);

        // Assert
        Assert.Throws<SaldoDistintoDeCeroException>(accion);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(Cop(1m), cuenta.Saldo);
    }

    // ---- CA-23 / CL-11 ----

    [Fact]
    public void F001_CA23_CerrarCuentaBloqueada_LanzaTransicionNoPermitidaYSigueBloqueada()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();

        // Act
        var accion = () => cuenta.Cerrar(Instante);

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    // ---- CA-24 / CL-11 ----

    [Fact]
    public void F001_CA24_BloquearBloqueada_LanzaTransicionNoPermitidaSinCambios()
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
    public void F001_CA24_DesbloquearActiva_LanzaTransicionNoPermitidaSinCambios()
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
    public void F001_CA25_BloquearCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
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
    public void F001_CA25_DesbloquearCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
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
    public void F001_CA25_CerrarCuentaCerrada_LanzaTransicionNoPermitidaYSigueCerrada()
    {
        // Arrange
        var cuenta = CuentaCerrada();

        // Act
        var accion = () => cuenta.Cerrar(Instante);

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
        Assert.Equal(Cop(0m), cuenta.Saldo);
    }

    // ---- CA-26 / CL-12 ----

    [Fact]
    public void F001_CA26_CerrarBloqueadaConSaldo_LanzaTransicionNoPermitida()
    {
        // Arrange
        var cuenta = CuentaBloqueadaConSaldo(10_000m);

        // Act
        var accion = () => cuenta.Cerrar(Instante);

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(EstadoCuenta.Bloqueada, cuenta.Estado);
        Assert.Equal(Cop(10_000m), cuenta.Saldo);
    }

    // ---- RN-14: la cuenta desbloqueada puede cerrarse (camino completo) ----

    [Fact]
    public void F001_RN14_BloquearDesbloquearYCerrarConSaldoCero_QuedaCerrada()
    {
        // Arrange
        var cuenta = CuentaBloqueadaSinSaldo();
        cuenta.Desbloquear();

        // Act
        cuenta.Cerrar(Instante);

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
    }

    // ---- Prueba explícita por regla de negocio ----

    [Fact]
    public void F001_RN06_CerrarConSaldoDistintoDeCero_LanzaSaldoDistintoDeCeroSinCambios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(1m);

        // Act
        var accion = () => cuenta.Cerrar(Instante);

        // Assert
        Assert.Throws<SaldoDistintoDeCeroException>(accion);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
        Assert.Equal(Cop(1m), cuenta.Saldo);
    }
}
