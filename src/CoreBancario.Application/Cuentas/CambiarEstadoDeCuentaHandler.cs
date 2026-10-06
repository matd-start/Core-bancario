using CoreBancario.Application.Comun;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

public sealed class CambiarEstadoDeCuentaHandler(ICuentaRepositorio cuentas, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>ObtenerAsync → Bloquear() | Desbloquear() | Cerrar(reloj.AhoraEnMicrosegundos()) → GuardarCambiosAsync. Sin reintento automático (ADR-0012).</summary>
    /// <exception cref="RecursoNoEncontradoException">CL-16.</exception>
    /// <exception cref="TransicionNoPermitidaException">RN-14, CL-13.</exception>
    /// <exception cref="SaldoDistintoDeCeroException">RN-06, CL-14.</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">CL-15.</exception>
    public async Task<CuentaDto> EjecutarAsync(CambiarEstadoDeCuentaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var cuenta = await cuentas.ObtenerAsync(comando.CuentaId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe una cuenta con ese identificador.");

        switch (comando.Accion)
        {
            case AccionDeEstado.Bloquear:
                cuenta.Bloquear();
                break;
            case AccionDeEstado.Desbloquear:
                cuenta.Desbloquear();
                break;
            case AccionDeEstado.Cerrar:
                cuenta.Cerrar(reloj.AhoraEnMicrosegundos());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(comando), comando.Accion, "Acción de estado desconocida.");
        }

        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);

        return CuentaDto.Desde(cuenta);
    }
}
