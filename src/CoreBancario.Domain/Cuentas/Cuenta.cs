using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Cuentas;

public sealed class Cuenta
{
    public Guid Id { get; }
    public NumeroDeCuenta Numero { get; }
    public Guid ClienteId { get; }
    public Moneda Moneda { get; }
    public EstadoCuenta Estado { get; private set; }
    public Dinero Saldo { get; private set; }

    // SDD: esqueleto creado por test-writer (propiedades nuevas; el coder las asigna en T03)
    public DateTimeOffset FechaApertura { get; }
    public DateTimeOffset? FechaCierre { get; private set; }

    private Cuenta(Guid id, NumeroDeCuenta numero, Guid clienteId, Moneda moneda)
    {
        Id = id;
        Numero = numero;
        ClienteId = clienteId;
        Moneda = moneda;
        Estado = EstadoCuenta.Activa;
        Saldo = Dinero.Crear(0m, moneda);
    }

    /// <summary>Abre una cuenta Activa con saldo 0 en la moneda indicada (RN-02).</summary>
    // SDD: esqueleto creado por test-writer (firma nueva del plan; aún no usa fechaApertura)
    public static Cuenta Abrir(NumeroDeCuenta numero, Guid clienteId, Moneda moneda, DateTimeOffset fechaApertura)
    {
        ArgumentNullException.ThrowIfNull(numero);
        ArgumentNullException.ThrowIfNull(moneda);

        return new Cuenta(Guid.CreateVersion7(), numero, clienteId, moneda);
    }

    /// <summary>Suma monto al saldo. Orden de validación (CL-12): estado, moneda, monto > 0.</summary>
    public Movimiento Acreditar(Dinero monto, DateTimeOffset fechaHora)
    {
        ArgumentNullException.ThrowIfNull(monto);

        // Lista blanca: solo Activa y Bloqueada admiten créditos (RN-05).
        if (Estado is not (EstadoCuenta.Activa or EstadoCuenta.Bloqueada))
            throw new OperacionNoPermitidaException(Estado, TipoMovimiento.Credito);

        if (monto.Moneda != Moneda)
            throw new MonedasDistintasException(Moneda, monto.Moneda);

        if (monto.EsCero)
            throw new MontoNoPositivoException();

        var nuevoSaldo = Saldo.Sumar(monto);
        Saldo = nuevoSaldo;
        return new Movimiento(Id, TipoMovimiento.Credito, monto, nuevoSaldo, fechaHora);
    }

    /// <summary>Resta monto del saldo. Orden de validación (CL-12): estado, moneda, monto > 0, saldo.</summary>
    public Movimiento Debitar(Dinero monto, DateTimeOffset fechaHora)
    {
        ArgumentNullException.ThrowIfNull(monto);

        // Lista blanca: solo Activa admite débitos (RN-05).
        if (Estado is not EstadoCuenta.Activa)
            throw new OperacionNoPermitidaException(Estado, TipoMovimiento.Debito);

        if (monto.Moneda != Moneda)
            throw new MonedasDistintasException(Moneda, monto.Moneda);

        if (monto.EsCero)
            throw new MontoNoPositivoException();

        // Se comprueba antes de Restar: la excepción de Restar es solo una red de seguridad (RN-01).
        if (monto.Monto > Saldo.Monto)
            throw new SaldoInsuficienteException(Saldo, monto);

        var nuevoSaldo = Saldo.Restar(monto);
        Saldo = nuevoSaldo;
        return new Movimiento(Id, TipoMovimiento.Debito, monto, nuevoSaldo, fechaHora);
    }

    /// <summary>Activa → Bloqueada.</summary>
    public void Bloquear() => CambiarEstado(EstadoCuenta.Bloqueada);

    /// <summary>Bloqueada → Activa.</summary>
    public void Desbloquear() => CambiarEstado(EstadoCuenta.Activa);

    /// <summary>Activa → Cerrada, solo con saldo cero. La transición se comprueba primero (CL-12).</summary>
    // SDD: esqueleto creado por test-writer (firma nueva del plan; aún no fija FechaCierre)
    public void Cerrar(DateTimeOffset fechaCierre)
    {
        if (!EsTransicionPermitida(Estado, EstadoCuenta.Cerrada))
            throw new TransicionNoPermitidaException(Estado, EstadoCuenta.Cerrada);

        if (!Saldo.EsCero)
            throw new SaldoDistintoDeCeroException(Saldo);

        Estado = EstadoCuenta.Cerrada;
    }

    private void CambiarEstado(EstadoCuenta destino)
    {
        if (!EsTransicionPermitida(Estado, destino))
            throw new TransicionNoPermitidaException(Estado, destino);

        Estado = destino;
    }

    // Lista blanca (RN-14): solo estas tres transiciones; todo lo demás, incluido repetir estado, se rechaza.
    private static bool EsTransicionPermitida(EstadoCuenta origen, EstadoCuenta destino) => (origen, destino) switch
    {
        (EstadoCuenta.Activa, EstadoCuenta.Bloqueada) => true,
        (EstadoCuenta.Bloqueada, EstadoCuenta.Activa) => true,
        (EstadoCuenta.Activa, EstadoCuenta.Cerrada) => true,
        _ => false,
    };
}
