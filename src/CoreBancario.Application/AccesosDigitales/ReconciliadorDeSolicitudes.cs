using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class ReconciliadorDeSolicitudes(IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>
    /// Reconciliación (a) de ADR-0017. Cada solicitud pendiente del cliente se cierra: Completado (motivo "Confirmada al
    /// reintentar") si usuario?.UltimaSolicitud == solicitud.Id; si no, Fallido (motivo "No se aplicó; cerrada al reintentar").
    /// Un solo GuardarCambiosAsync si cerró alguna; ninguno si no había pendientes. Devuelve los resultados escritos.
    /// </summary>
    public Task<IReadOnlyList<ResultadoDeSolicitud>> ReconciliarAsync(Guid clienteId, UsuarioDeIdentidad? usuario, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
