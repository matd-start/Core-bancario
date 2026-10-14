namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>RN-20 / ADR-0017. Fila inmutable con el desenlace de una solicitud; como máximo una por solicitud (la base lo garantiza).</summary>
// SDD: esqueleto creado por test-writer
public sealed class ResultadoDeSolicitud
{
    public const int LongitudMaximaDelMotivo = 200;

    public Guid SolicitudId { get; }

    public ResultadoDeAcceso Resultado { get; }

    public DateTimeOffset Fecha { get; }

    /// <summary>Texto técnico corto sin secretos. null si no hace falta.</summary>
    public string? Motivo { get; }

    // Solo la crean SolicitudDeAcceso.Completar/Fallar (constructor internal) y EF Core (constructor privado, lo añade la implementación).
    internal ResultadoDeSolicitud(Guid solicitudId, ResultadoDeAcceso resultado, DateTimeOffset fecha, string? motivo) =>
        throw new NotImplementedException();
}
