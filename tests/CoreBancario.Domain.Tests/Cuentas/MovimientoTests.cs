using System.Reflection;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Cuentas;

public class MovimientoTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static NumeroDeCuenta Numero() => NumeroDeCuenta.Crear("1234567897");

    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    private static Cuenta CuentaActivaConSaldo(decimal pesos)
    {
        var cuenta = Cuenta.Abrir(Numero(), Guid.NewGuid(), Moneda.COP, Instante);
        cuenta.Acreditar(Cop(pesos), Instante);
        return cuenta;
    }

    private static bool TieneSetterPublico(PropertyInfo propiedad) =>
        propiedad.SetMethod is not null && propiedad.SetMethod.IsPublic;

    // ---- CA-11 ----

    [Fact]
    public void F001_CA11_AcreditarCuentaActiva_ProduceMovimientoDeCredito()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var movimiento = cuenta.Acreditar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(TipoMovimiento.Credito, movimiento.Tipo);
        Assert.Equal(Cop(20_000m), movimiento.Monto);
        Assert.Equal(Cop(70_000m), movimiento.SaldoResultante);
    }

    // ---- CA-12 ----

    [Fact]
    public void F001_CA12_DebitarCuentaActiva_ProduceMovimientoDeDebito()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var movimiento = cuenta.Debitar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(TipoMovimiento.Debito, movimiento.Tipo);
        Assert.Equal(Cop(20_000m), movimiento.Monto);
        Assert.Equal(Cop(30_000m), movimiento.SaldoResultante);
    }

    // ---- Datos del Movimiento ----

    [Fact]
    public void F001_CA11_MovimientoDeCredito_GuardaCuentaIdYFechaHora()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var movimiento = cuenta.Acreditar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(cuenta.Id, movimiento.CuentaId);
        Assert.Equal(Instante, movimiento.FechaHora);
    }

    [Fact]
    public void F001_CA12_MovimientoDeDebito_GuardaCuentaIdYFechaHora()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var movimiento = cuenta.Debitar(Cop(20_000m), Instante);

        // Assert
        Assert.Equal(cuenta.Id, movimiento.CuentaId);
        Assert.Equal(Instante, movimiento.FechaHora);
    }

    [Fact]
    public void F001_CA11_DosMovimientos_TienenIdsDistintosYNoVacios()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var primero = cuenta.Acreditar(Cop(1_000m), Instante);
        var segundo = cuenta.Acreditar(Cop(1_000m), Instante);

        // Assert
        Assert.NotEqual(Guid.Empty, primero.Id);
        Assert.NotEqual(primero.Id, segundo.Id);
    }

    [Fact]
    public void F001_CA11_SaldoResultante_ReflejaElSaldoDeLaCuentaTrasCadaOperacion()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);

        // Act
        var credito = cuenta.Acreditar(Cop(10_000m), Instante);
        var debito = cuenta.Debitar(Cop(5_000m), Instante);

        // Assert
        Assert.Equal(Cop(60_000m), credito.SaldoResultante);
        Assert.Equal(Cop(55_000m), debito.SaldoResultante);
        Assert.Equal(debito.SaldoResultante, cuenta.Saldo);
    }

    // ---- CA-28 / RN-10 ----

    [Fact]
    public void F001_CA28_PropiedadesPublicasDeMovimiento_NoTienenSetterPublico()
    {
        // Arrange
        var propiedades = typeof(Movimiento).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act
        var conSetterPublico = propiedades.Where(TieneSetterPublico).Select(p => p.Name).ToList();

        // Assert
        Assert.NotEmpty(propiedades);
        Assert.Empty(conSetterPublico);
    }

    [Fact]
    public void F001_CA28_PropiedadesPublicasDeDinero_NoTienenSetterPublico()
    {
        // Arrange
        var propiedades = typeof(Dinero).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act
        var conSetterPublico = propiedades.Where(TieneSetterPublico).Select(p => p.Name).ToList();

        // Assert
        Assert.NotEmpty(propiedades);
        Assert.Empty(conSetterPublico);
    }

    [Fact]
    public void F001_CA28_MovimientoNoTieneConstructorPublico_SoloLoCreaCuenta()
    {
        // Arrange / Act
        var constructoresPublicos = typeof(Movimiento).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        // Assert
        Assert.Empty(constructoresPublicos);
    }

    [Fact]
    public void F001_CA28_OperarDespuesDeUnMovimiento_NoCambiaElMovimientoAnterior()
    {
        // Arrange
        var cuenta = CuentaActivaConSaldo(50_000m);
        var primero = cuenta.Acreditar(Cop(20_000m), Instante);

        // Act
        cuenta.Acreditar(Cop(5_000m), Instante);

        // Assert
        Assert.Equal(Cop(70_000m), primero.SaldoResultante);
        Assert.Equal(Cop(20_000m), primero.Monto);
        Assert.Equal(TipoMovimiento.Credito, primero.Tipo);
    }
}
