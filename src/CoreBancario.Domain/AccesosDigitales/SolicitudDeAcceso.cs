namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>
/// RN-20. Fila inmutable del registro de accesos: la intención del operador, escrita ANTES de llamar al sistema de
/// identidad (ADR-0017). Su resultado es otra fila (ResultadoDeSolicitud). Id = Guid.CreateVersion7(Fecha).
/// </summary>
// SDD: esqueleto creado por test-writer
public sealed class SolicitudDeAcceso
{
    public const int LongitudMaximaDelOperador = 255;

    public Guid Id { get; }

    public Guid ClienteId { get; }

    /// <summary>Sub del operador, tomado de su token (RN-19).</summary>
    public string Operador { get; }

    public AccionDeAcceso Accion { get; }

    public DateTimeOffset Fecha { get; }

    private SolicitudDeAcceso(Guid id, Guid clienteId, string operador, AccionDeAcceso accion, DateTimeOffset fecha) =>
        throw new NotImplementedException();

    /// <summary>SinAcceso o Deshabilitado → Habilitar (CL-16). Habilitado → Restablecer (CL-13).</summary>
    /// <exception cref="ArgumentException">clienteId vacío u operador null, vacío, solo espacios o de más de 255 caracteres.</exception>
    public static SolicitudDeAcceso ParaHabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha) =>
        throw new NotImplementedException();

    /// <summary>Habilitado → Deshabilitar. Valida antes de crear nada: si lanza, no hay solicitud (CA-04).</summary>
    /// <exception cref="AccesoNoHabilitadoException">estadoActual es SinAcceso o Deshabilitado (CL-14).</exception>
    /// <exception cref="ArgumentException">Igual que ParaHabilitar.</exception>
    public static SolicitudDeAcceso ParaDeshabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha) =>
        throw new NotImplementedException();

    /// <summary>Resultado Completado de ESTA solicitud. No modifica la solicitud: devuelve una fila nueva.</summary>
    public ResultadoDeSolicitud Completar(DateTimeOffset fecha, string? motivo = null) =>
        throw new NotImplementedException();

    /// <exception cref="ArgumentException">motivo null, vacío o de más de 200 caracteres.</exception>
    public ResultadoDeSolicitud Fallar(DateTimeOffset fecha, string motivo) =>
        throw new NotImplementedException();
}
