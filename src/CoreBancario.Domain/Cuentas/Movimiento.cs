using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Cuentas;

// SDD: esqueleto creado por test-writer
/// <summary>Registro inmutable de un débito o crédito (RN-10). Solo lo crea Cuenta.</summary>
public sealed class Movimiento
{
    public Guid Id { get; }
    public Guid CuentaId { get; }
    public TipoMovimiento Tipo { get; }
    public Dinero Monto { get; }
    public Dinero SaldoResultante { get; }
    public DateTimeOffset FechaHora { get; }

    internal Movimiento(Guid cuentaId, TipoMovimiento tipo, Dinero monto, Dinero saldoResultante, DateTimeOffset fechaHora)
    {
        CuentaId = cuentaId;
        Tipo = tipo;
        Monto = monto;
        SaldoResultante = saldoResultante;
        FechaHora = fechaHora;
    }
}
