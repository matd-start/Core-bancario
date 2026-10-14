namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>Estado del acceso de un cliente tal como lo informa el sistema de identidad (única fuente de verdad, ADR-0017).</summary>
public enum EstadoDeAcceso
{
    SinAcceso,
    Habilitado,
    Deshabilitado,
}
