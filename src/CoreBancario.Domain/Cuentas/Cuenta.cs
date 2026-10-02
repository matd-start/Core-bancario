using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Cuentas;

// SDD: esqueleto creado por test-writer
public sealed class Cuenta
{
    public Guid Id { get; }
    public string Numero { get; }
    public Guid ClienteId { get; }
    public Moneda Moneda { get; }
    public EstadoCuenta Estado { get; private set; }
    public Dinero Saldo { get; private set; }

    private Cuenta(Guid id, string numero, Guid clienteId, Moneda moneda)
    {
        Id = id;
        Numero = numero;
        ClienteId = clienteId;
        Moneda = moneda;
        Saldo = null!;
    }

    public static Cuenta Abrir(string numero, Guid clienteId, Moneda moneda) => throw new NotImplementedException();

    public Movimiento Acreditar(Dinero monto, DateTimeOffset fechaHora) => throw new NotImplementedException();

    public Movimiento Debitar(Dinero monto, DateTimeOffset fechaHora) => throw new NotImplementedException();

    public void Bloquear() => throw new NotImplementedException();

    public void Desbloquear() => throw new NotImplementedException();

    public void Cerrar() => throw new NotImplementedException();
}
