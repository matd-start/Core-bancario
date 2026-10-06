namespace CoreBancario.Application.Cuentas;

public sealed record CambiarEstadoDeCuentaComando(Guid CuentaId, AccionDeEstado Accion);
