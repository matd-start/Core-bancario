using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests.Monetario;

public class DineroTests
{
    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    private static Dinero Usd(decimal monto) => Dinero.Crear(monto, Moneda.USD);

    // Puntos medios exactos (ADR-0007, al par): entrada, esperado.
    public static TheoryData<decimal, decimal> PuntosMediosCop => new()
    {
        { 41234.5m, 41234m },
        { 123703.5m, 123704m },
        { 206172.5m, 206172m },
    };

    public static TheoryData<decimal, decimal> PuntosMediosUsd => new()
    {
        { 10.125m, 10.12m },
        { 10.135m, 10.14m },
    };

    public static TheoryData<decimal, decimal> SobreLaMitadCop => new()
    {
        { 42595.6m, 42596m },
    };

    public static TheoryData<decimal, decimal> SobreLaMitadUsd => new()
    {
        { 10.126m, 10.13m },
    };

    // ---- CA-01 / CL-01 ----

    [Fact]
    public void F001_CA01_CrearConMontoNegativo_LanzaMontoNegativoException()
    {
        // Arrange
        var monto = -1m;

        // Act
        var accion = () => Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.Throws<MontoNegativoException>(accion);
    }

    [Fact]
    public void F001_CA01_CrearUsdConMontoNegativo_LanzaMontoNegativoException()
    {
        // Arrange
        var monto = -0.01m;

        // Act
        var accion = () => Dinero.Crear(monto, Moneda.USD);

        // Assert
        Assert.Throws<MontoNegativoException>(accion);
    }

    // ---- CA-02 ----

    [Fact]
    public void F001_CA02_CrearConMontoCero_SeCreaConMontoCero()
    {
        // Arrange
        var monto = 0m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.Equal(0m, dinero.Monto);
        Assert.True(dinero.EsCero);
    }

    [Fact]
    public void F001_CA02_CrearConMontoPositivo_NoEsCero()
    {
        // Arrange
        var monto = 1m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.False(dinero.EsCero);
    }

    // ---- CA-03 / CL-02 ----

    [Fact]
    public void F001_CA03_CrearUsdConMasDecimalesQueLaMoneda_LanzaPrecisionExcedidaException()
    {
        // Arrange
        var monto = 10.555m;

        // Act
        var accion = () => Dinero.Crear(monto, Moneda.USD);

        // Assert
        Assert.Throws<PrecisionExcedidaException>(accion);
    }

    [Fact]
    public void F001_CA03_CrearCopConDecimales_LanzaPrecisionExcedidaException()
    {
        // Arrange
        var monto = 1000.50m;

        // Act
        var accion = () => Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.Throws<PrecisionExcedidaException>(accion);
    }

    // ---- CA-04 / CL-03 ----

    [Fact]
    public void F001_CA04_CrearCopConCerosSobrantes_SeCrea()
    {
        // Arrange
        var monto = 1000.00m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.Equal(1000m, dinero.Monto);
        Assert.Equal(Moneda.COP, dinero.Moneda);
    }

    [Fact]
    public void F001_CA04_CrearUsdConDosDecimales_SeCrea()
    {
        // Arrange
        var monto = 10.50m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.USD);

        // Assert
        Assert.Equal(10.50m, dinero.Monto);
        Assert.Equal(Moneda.USD, dinero.Moneda);
    }

    [Fact]
    public void F001_CL03_CrearConCerosSobrantes_GuardaLaEscalaDeSuMoneda()
    {
        // Arrange
        var monto = 1000.00m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.COP);

        // Assert
        Assert.Equal(0, dinero.Monto.Scale);
        Assert.Equal("1000 COP", dinero.ToString());
    }

    [Fact]
    public void F001_CL03_CrearUsdConUnDecimal_GuardaDosDecimales()
    {
        // Arrange
        var monto = 10.5m;

        // Act
        var dinero = Dinero.Crear(monto, Moneda.USD);

        // Assert
        Assert.Equal(2, dinero.Monto.Scale);
        Assert.Equal("10.50 USD", dinero.ToString());
    }

    // ---- Argumentos nulos ----

    [Fact]
    public void F001_ADR0008_CrearConMonedaNula_LanzaArgumentNullException()
    {
        // Arrange
        Moneda moneda = null!;

        // Act
        var accion = () => Dinero.Crear(1m, moneda);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void F001_ADR0008_DesdeCalculoConMonedaNula_LanzaArgumentNullException()
    {
        // Arrange
        Moneda moneda = null!;

        // Act
        var accion = () => Dinero.DesdeCalculo(1m, moneda);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void F001_ADR0008_SumarConNulo_LanzaArgumentNullException()
    {
        // Arrange
        var dinero = Cop(100m);

        // Act
        var accion = () => dinero.Sumar(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void F001_ADR0008_RestarConNulo_LanzaArgumentNullException()
    {
        // Arrange
        var dinero = Cop(100m);

        // Act
        var accion = () => dinero.Restar(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // ---- CA-05 / CL-04 ----

    [Fact]
    public void F001_CA05_SumarMonedasDistintas_LanzaMonedasDistintasException()
    {
        // Arrange
        var pesos = Cop(100m);
        var dolares = Usd(1.00m);

        // Act
        var accion = () => pesos.Sumar(dolares);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
    }

    [Fact]
    public void F001_CA05_RestarMonedasDistintas_LanzaMonedasDistintasException()
    {
        // Arrange
        var pesos = Cop(100m);
        var dolares = Usd(1.00m);

        // Act
        var accion = () => pesos.Restar(dolares);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
    }

    [Fact]
    public void F001_CA05_RestarMonedasDistintasQueTambienDaNegativo_LanzaMonedasDistintasException()
    {
        // Arrange: la moneda se comprueba antes que el signo del resultado.
        var pesos = Cop(50m);
        var dolares = Usd(80.00m);

        // Act
        var accion = () => pesos.Restar(dolares);

        // Assert
        Assert.Throws<MonedasDistintasException>(accion);
    }

    // ---- CA-06 ----

    [Fact]
    public void F001_CA06_SumarMismaMoneda_DevuelveLaSuma()
    {
        // Arrange
        var cien = Cop(100m);
        var cincuenta = Cop(50m);

        // Act
        var resultado = cien.Sumar(cincuenta);

        // Assert
        Assert.Equal(Cop(150m), resultado);
    }

    [Fact]
    public void F001_CA06_RestarMismaMoneda_DevuelveLaDiferencia()
    {
        // Arrange
        var cien = Cop(100m);
        var cincuenta = Cop(50m);

        // Act
        var resultado = cien.Restar(cincuenta);

        // Assert
        Assert.Equal(Cop(50m), resultado);
    }

    [Fact]
    public void F001_CA06_SumarUsd_ConservaDosDecimales()
    {
        // Arrange
        var a = Usd(10.50m);
        var b = Usd(0.25m);

        // Act
        var resultado = a.Sumar(b);

        // Assert
        Assert.Equal(Usd(10.75m), resultado);
        Assert.Equal(2, resultado.Monto.Scale);
    }

    // ---- CA-07 / CL-05 ----

    [Fact]
    public void F001_CA07_RestarMasDeLoQueHay_LanzaMontoNegativoException()
    {
        // Arrange
        var cincuenta = Cop(50m);
        var ochenta = Cop(80m);

        // Act
        var accion = () => cincuenta.Restar(ochenta);

        // Assert
        Assert.Throws<MontoNegativoException>(accion);
    }

    [Fact]
    public void F001_CA07_RestarElMismoMonto_DaCero()
    {
        // Arrange
        var cincuenta = Cop(50m);

        // Act
        var resultado = cincuenta.Restar(Cop(50m));

        // Assert
        Assert.True(resultado.EsCero);
    }

    // ---- CA-08 ----

    [Fact]
    public void F001_CA08_MismoMontoYMismaMoneda_SonIguales()
    {
        // Arrange
        var a = Cop(1000m);
        var b = Cop(1000m);

        // Act
        var iguales = a == b;

        // Assert
        Assert.True(iguales);
    }

    [Fact]
    public void F001_CA08_MismoMontoYDistintaMoneda_NoSonIguales()
    {
        // Arrange
        var pesos = Cop(100m);
        var dolares = Usd(100m);

        // Act
        var iguales = pesos == dolares;

        // Assert
        Assert.False(iguales);
    }

    [Fact]
    public void F001_CA08_MismoValorEscritoDistinto_SonIgualesConElMismoHash()
    {
        // Arrange
        var a = Cop(1000m);
        var b = Cop(1000.00m);

        // Act
        var hashA = a.GetHashCode();
        var hashB = b.GetHashCode();

        // Assert
        Assert.Equal(a, b);
        Assert.Equal(hashA, hashB);
    }

    // ---- CA-09 / CL-14 ----

    [Fact]
    public void F001_CA09_DesdeCalculoUsd_AjustaAlValorMasCercano()
    {
        // Arrange
        var resultado = 24.2515m;

        // Act
        var dinero = Dinero.DesdeCalculo(resultado, Moneda.USD);

        // Assert
        Assert.Equal(24.25m, dinero.Monto);
        Assert.Equal(Moneda.USD, dinero.Moneda);
    }

    [Fact]
    public void F001_CA09_DesdeCalculoCop_AjustaAlValorMasCercano()
    {
        // Arrange
        var resultado = 42595.2385m;

        // Act
        var dinero = Dinero.DesdeCalculo(resultado, Moneda.COP);

        // Assert
        Assert.Equal(42595m, dinero.Monto);
        Assert.Equal(Moneda.COP, dinero.Moneda);
    }

    [Theory]
    [MemberData(nameof(SobreLaMitadUsd))]
    public void F001_CL14_DesdeCalculoUsdPorEncimaDeLaMitad_SubeAlSiguiente(decimal entrada, decimal esperado)
    {
        // Arrange / Act
        var dinero = Dinero.DesdeCalculo(entrada, Moneda.USD);

        // Assert
        Assert.Equal(esperado, dinero.Monto);
    }

    [Theory]
    [MemberData(nameof(SobreLaMitadCop))]
    public void F001_CL14_DesdeCalculoCopPorEncimaDeLaMitad_SubeAlSiguiente(decimal entrada, decimal esperado)
    {
        // Arrange / Act
        var dinero = Dinero.DesdeCalculo(entrada, Moneda.COP);

        // Assert
        Assert.Equal(esperado, dinero.Monto);
    }

    [Theory]
    [MemberData(nameof(PuntosMediosCop))]
    public void F001_RN16_DesdeCalculoCopEnPuntoMedioExacto_RedondeaAlPar(decimal entrada, decimal esperado)
    {
        // Arrange / Act
        var dinero = Dinero.DesdeCalculo(entrada, Moneda.COP);

        // Assert
        Assert.Equal(esperado, dinero.Monto);
    }

    [Theory]
    [MemberData(nameof(PuntosMediosUsd))]
    public void F001_RN16_DesdeCalculoUsdEnPuntoMedioExacto_RedondeaAlPar(decimal entrada, decimal esperado)
    {
        // Arrange / Act
        var dinero = Dinero.DesdeCalculo(entrada, Moneda.USD);

        // Assert
        Assert.Equal(esperado, dinero.Monto);
    }

    [Fact]
    public void F001_RN15_DesdeCalculoCopConResultadoNegativo_LanzaMontoNegativoException()
    {
        // Arrange
        var resultado = -1m;

        // Act
        var accion = () => Dinero.DesdeCalculo(resultado, Moneda.COP);

        // Assert
        Assert.Throws<MontoNegativoException>(accion);
    }

    [Fact]
    public void F001_RN15_DesdeCalculoNegativoQueRedondeaACero_LanzaMontoNegativoException()
    {
        // Arrange: el signo se comprueba antes de redondear (-0,004 USD redondearía a 0,00).
        var resultado = -0.004m;

        // Act
        var accion = () => Dinero.DesdeCalculo(resultado, Moneda.USD);

        // Assert
        Assert.Throws<MontoNegativoException>(accion);
    }

    [Fact]
    public void F001_CL14_DesdeCalculoCop_GuardaLaEscalaDeSuMoneda()
    {
        // Arrange
        var resultado = 1000.4m;

        // Act
        var dinero = Dinero.DesdeCalculo(resultado, Moneda.COP);

        // Assert
        Assert.Equal(0, dinero.Monto.Scale);
        Assert.Equal("1000 COP", dinero.ToString());
    }

    [Fact]
    public void F001_CL14_DesdeCalculoUsd_GuardaLaEscalaDeSuMoneda()
    {
        // Arrange
        var resultado = 5.1m;

        // Act
        var dinero = Dinero.DesdeCalculo(resultado, Moneda.USD);

        // Assert
        Assert.Equal(2, dinero.Monto.Scale);
        Assert.Equal("5.10 USD", dinero.ToString());
    }
}
