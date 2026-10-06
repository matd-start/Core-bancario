using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class CambiarEstadoDeCuentaHandler(ICuentaRepositorio cuentas, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    // SDD: esqueleto creado por test-writer
    public Task<CuentaDto> EjecutarAsync(CambiarEstadoDeCuentaComando comando, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
