using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>
/// Registro de accesos (RN-20): solo inserciones. Distingue lo agregado de lo GUARDADO: Agregar* deja la fila "sin
/// guardar" y solo cuando la unidad de trabajo confirma pasa a Solicitudes / Resultados. Así una prueba puede
/// comprobar que la intención ya estaba guardada cuando se llamó al proveedor (ADR-0017), y que si la base falla la
/// fila no queda. Una solicitud está pendiente si está guardada y no tiene resultado guardado.
/// </summary>
public sealed class RegistroDeAccesosEnMemoria : IRegistroDeAccesos
{
    private readonly List<SolicitudDeAcceso> _solicitudesSinGuardar = [];
    private readonly List<ResultadoDeSolicitud> _resultadosSinGuardar = [];
    private readonly List<SolicitudDeAcceso> _solicitudes = [];
    private readonly List<ResultadoDeSolicitud> _resultados = [];

    public RegistroDeAccesosEnMemoria(UnidadDeTrabajoEspia unidadDeTrabajo) =>
        unidadDeTrabajo.Confirmado += Confirmar;

    /// <summary>Solicitudes ya guardadas.</summary>
    public IReadOnlyList<SolicitudDeAcceso> Solicitudes => _solicitudes;

    /// <summary>Resultados ya guardados.</summary>
    public IReadOnlyList<ResultadoDeSolicitud> Resultados => _resultados;

    /// <summary>true si no hay NADA, ni guardado ni agregado a la espera de guardar.</summary>
    public bool EstaVacio =>
        _solicitudes.Count + _resultados.Count + _solicitudesSinGuardar.Count + _resultadosSinGuardar.Count == 0;

    /// <summary>Prepara una solicitud que ya estaba guardada antes de la operación (con su resultado, si lo tiene).</summary>
    public void Sembrar(SolicitudDeAcceso solicitud, ResultadoDeSolicitud? resultado = null)
    {
        _solicitudes.Add(solicitud);
        if (resultado is not null)
            _resultados.Add(resultado);
    }

    public void AgregarSolicitud(SolicitudDeAcceso solicitud) => _solicitudesSinGuardar.Add(solicitud);

    public void AgregarResultado(ResultadoDeSolicitud resultado) => _resultadosSinGuardar.Add(resultado);

    public Task<IReadOnlyList<SolicitudDeAcceso>> ListarPendientesAsync(Guid clienteId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SolicitudDeAcceso>>(
            _solicitudes
                .Where(s => s.ClienteId == clienteId && _resultados.All(r => r.SolicitudId != s.Id))
                .OrderBy(s => s.Fecha)
                .ToList());

    private void Confirmar()
    {
        _solicitudes.AddRange(_solicitudesSinGuardar);
        _resultados.AddRange(_resultadosSinGuardar);
        _solicitudesSinGuardar.Clear();
        _resultadosSinGuardar.Clear();
    }
}
