namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>RN-20 / ADR-0017. Fila inmutable con el desenlace de una solicitud; como máximo una por solicitud (la base lo garantiza).</summary>
public sealed class ResultadoDeSolicitud
{
    public const int LongitudMaximaDelMotivo = 200;

    public Guid SolicitudId { get; }

    public ResultadoDeAcceso Resultado { get; }

    public DateTimeOffset Fecha { get; }

    /// <summary>Texto técnico corto sin secretos. null si no hace falta.</summary>
    public string? Motivo { get; }

    // Solo para EF Core.
#pragma warning disable CS8618
    private ResultadoDeSolicitud() { }
#pragma warning restore CS8618

    // Solo la crean SolicitudDeAcceso.Completar y SolicitudDeAcceso.Fallar, que validan antes.
    internal ResultadoDeSolicitud(Guid solicitudId, ResultadoDeAcceso resultado, DateTimeOffset fecha, string? motivo)
    {
        SolicitudId = solicitudId;
        Resultado = resultado;
        Fecha = fecha;
        Motivo = motivo;
    }
}
