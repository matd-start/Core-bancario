namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>Cómo terminó una solicitud (ADR-0017). Una solicitud sin resultado está pendiente.</summary>
public enum ResultadoDeAcceso
{
    Completado,
    Fallido,
}
