namespace CoreBancario.Application.AccesosDigitales;

/// <summary>503 identidad-sin-respuesta. La solicitud queda SIN resultado (pendiente) y el reintento la reconcilia (ADR-0017).</summary>
public sealed class ProveedorDeIdentidadSinRespuestaException : Exception
{
    public ProveedorDeIdentidadSinRespuestaException(Exception? causa = null)
        : base("El sistema de identidad no respondió. La operación quedó pendiente de confirmar; vuelve a intentarla.", causa)
    {
    }
}
