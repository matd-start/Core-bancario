namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>
/// RN-20. Fila inmutable del registro de accesos: la intención del operador, escrita ANTES de llamar al sistema de
/// identidad (ADR-0017). Su resultado es otra fila (ResultadoDeSolicitud). Id = Guid.CreateVersion7(Fecha).
/// </summary>
public sealed class SolicitudDeAcceso
{
    public const int LongitudMaximaDelOperador = 255;

    public Guid Id { get; }

    public Guid ClienteId { get; }

    /// <summary>Sub del operador, tomado de su token (RN-19).</summary>
    public string Operador { get; }

    public AccionDeAcceso Accion { get; }

    public DateTimeOffset Fecha { get; }

    private SolicitudDeAcceso(Guid id, Guid clienteId, string operador, AccionDeAcceso accion, DateTimeOffset fecha)
    {
        Id = id;
        ClienteId = clienteId;
        Operador = operador;
        Accion = accion;
        Fecha = fecha;
    }

    /// <summary>SinAcceso o Deshabilitado → Habilitar (CL-16). Habilitado → Restablecer (CL-13).</summary>
    /// <exception cref="ArgumentException">clienteId vacío u operador null, vacío, solo espacios o de más de 255 caracteres.</exception>
    public static SolicitudDeAcceso ParaHabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha)
    {
        var accion = estadoActual == EstadoDeAcceso.Habilitado ? AccionDeAcceso.Restablecer : AccionDeAcceso.Habilitar;
        return Crear(clienteId, operador, accion, fecha);
    }

    /// <summary>Habilitado → Deshabilitar. Valida antes de crear nada: si lanza, no hay solicitud (CA-04).</summary>
    /// <exception cref="AccesoNoHabilitadoException">estadoActual es SinAcceso o Deshabilitado (CL-14).</exception>
    /// <exception cref="ArgumentException">Igual que ParaHabilitar.</exception>
    public static SolicitudDeAcceso ParaDeshabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha)
    {
        if (estadoActual != EstadoDeAcceso.Habilitado)
            throw new AccesoNoHabilitadoException(estadoActual);

        return Crear(clienteId, operador, AccionDeAcceso.Deshabilitar, fecha);
    }

    /// <summary>Resultado Completado de ESTA solicitud. No modifica la solicitud: devuelve una fila nueva.</summary>
    public ResultadoDeSolicitud Completar(DateTimeOffset fecha, string? motivo = null) =>
        new(Id, ResultadoDeAcceso.Completado, fecha, motivo);

    /// <exception cref="ArgumentException">motivo null, vacío o de más de 200 caracteres.</exception>
    public ResultadoDeSolicitud Fallar(DateTimeOffset fecha, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (motivo.Length > ResultadoDeSolicitud.LongitudMaximaDelMotivo)
            throw new ArgumentException(
                $"El motivo no puede tener más de {ResultadoDeSolicitud.LongitudMaximaDelMotivo} caracteres.", nameof(motivo));

        return new ResultadoDeSolicitud(Id, ResultadoDeAcceso.Fallido, fecha, motivo);
    }

    private static SolicitudDeAcceso Crear(Guid clienteId, string operador, AccionDeAcceso accion, DateTimeOffset fecha)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("El identificador del cliente no puede estar vacío.", nameof(clienteId));

        ArgumentException.ThrowIfNullOrWhiteSpace(operador);
        if (operador.Length > LongitudMaximaDelOperador)
            throw new ArgumentException($"El operador no puede tener más de {LongitudMaximaDelOperador} caracteres.", nameof(operador));

        return new SolicitudDeAcceso(Guid.CreateVersion7(fecha), clienteId, operador, accion, fecha);
    }
}
