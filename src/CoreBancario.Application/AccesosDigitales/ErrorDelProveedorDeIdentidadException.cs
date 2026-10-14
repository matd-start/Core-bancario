namespace CoreBancario.Application.AccesosDigitales;

/// <summary>502 identidad-error. Motivo: texto técnico corto, SIN el cuerpo de la respuesta (puede traer datos internos).</summary>
public sealed class ErrorDelProveedorDeIdentidadException : Exception
{
    public ErrorDelProveedorDeIdentidadException(string motivo, Exception? causa = null)
        : base("El sistema de identidad rechazó la operación y no se completó. Vuelve a intentarla.", causa) =>
        Motivo = motivo;

    public string Motivo { get; }
}
