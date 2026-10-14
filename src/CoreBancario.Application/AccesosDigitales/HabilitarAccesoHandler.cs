using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;

namespace CoreBancario.Application.AccesosDigitales;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class HabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IGeneradorDeClaveTemporal generador, IAccesoDigitalRepositorio accesos,
    IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>Secuencia exacta en la sección 5 del plan ("Intención primero").</summary>
    /// <exception cref="RecursoNoEncontradoException">CL-12 (sin escribir nada ni llamar al proveedor).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException">Solicitud pendiente.</exception>
    /// <exception cref="ErrorDelProveedorDeIdentidadException">Solicitud con resultado Fallido.</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">Carrera con otro operador (índice único).</exception>
    public Task<AccesoHabilitadoDto> EjecutarAsync(HabilitarAccesoComando comando, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
