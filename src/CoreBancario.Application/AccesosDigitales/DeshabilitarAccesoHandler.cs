using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class DeshabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IAccesoDigitalRepositorio accesos, IRegistroDeAccesos registro,
    IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <exception cref="RecursoNoEncontradoException">Cliente inexistente.</exception>
    /// <exception cref="AccesoNoHabilitadoException">CL-14 (sin escribir la solicitud).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException"/>
    /// <exception cref="ErrorDelProveedorDeIdentidadException"/>
    /// <exception cref="ConflictoDeConcurrenciaException"/>
    public Task<RegistroDeAccesoDto> EjecutarAsync(DeshabilitarAccesoComando comando, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
