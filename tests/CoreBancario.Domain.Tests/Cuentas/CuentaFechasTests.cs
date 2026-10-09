using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using CoreBancario.Domain.Tests.Clientes;

namespace CoreBancario.Domain.Tests.Cuentas;

public class CuentaFechasTests
{
    private static readonly DateTimeOffset Apertura = new(2026, 10, 5, 14, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cierre = new(2026, 10, 6, 9, 30, 0, TimeSpan.Zero);

    private static Cuenta CuentaActivaSinSaldo() =>
        Cuenta.Abrir(NumeroDeCuenta.Crear("1234567897"), Guid.NewGuid(), Moneda.COP, Apertura);

    [Fact]
    public void F002_CA06_Abrir_FijaFechaDeAperturaYSinFechaDeCierre()
    {
        // Arrange / Act
        var cuenta = CuentaActivaSinSaldo();

        // Assert
        Assert.Equal(Apertura, cuenta.FechaApertura);
        Assert.Null(cuenta.FechaCierre);
    }

    [Fact]
    public void F002_CA06_Abrir_IdEsVersion7DeLaFechaDeApertura()
    {
        // Arrange / Act
        var cuenta = CuentaActivaSinSaldo();

        // Assert
        Assert.Equal(7, cuenta.Id.Version);
        Assert.Equal(Apertura.ToUnixTimeMilliseconds(), ClienteTests.MilisegundosDelGuidV7(cuenta.Id));
    }

    [Fact]
    public void F002_CA12_Cerrar_FijaLaFechaDeCierre()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();

        // Act
        cuenta.Cerrar(Cierre);

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, cuenta.Estado);
        Assert.Equal(Cierre, cuenta.FechaCierre);
    }

    [Fact]
    public void F002_CA11_CerrarBloqueada_NoFijaFechaDeCierre()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();
        cuenta.Bloquear();

        // Act
        var accion = () => cuenta.Cerrar(Cierre);

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Null(cuenta.FechaCierre);
    }

    [Fact]
    public void F002_CA13_CerrarConSaldo_NoFijaFechaDeCierre()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();
        cuenta.Acreditar(Dinero.Crear(1_000m, Moneda.COP), Apertura);

        // Act
        var accion = () => cuenta.Cerrar(Cierre);

        // Assert
        Assert.Throws<SaldoDistintoDeCeroException>(accion);
        Assert.Null(cuenta.FechaCierre);
        Assert.Equal(EstadoCuenta.Activa, cuenta.Estado);
    }

    [Fact]
    public void F002_CA12_CerrarCuentaYaCerrada_NoCambiaLaFechaDeCierre()
    {
        // Arrange
        var cuenta = CuentaActivaSinSaldo();
        cuenta.Cerrar(Cierre);

        // Act
        var accion = () => cuenta.Cerrar(Cierre.AddDays(1));

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
        Assert.Equal(Cierre, cuenta.FechaCierre);
    }
}
