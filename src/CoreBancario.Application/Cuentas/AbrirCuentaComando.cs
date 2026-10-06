namespace CoreBancario.Application.Cuentas;

public sealed record AbrirCuentaComando(Guid ClienteId, string? Moneda);
