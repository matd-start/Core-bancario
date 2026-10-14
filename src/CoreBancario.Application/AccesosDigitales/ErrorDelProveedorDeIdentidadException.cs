namespace CoreBancario.Application.AccesosDigitales;

/// <summary>502 identidad-error. Motivo: texto técnico corto, SIN el cuerpo de la respuesta (puede traer datos internos).</summary>
public sealed class ErrorDelProveedorDeIdentidadException : Exception
{
    // SDD: esqueleto creado por test-writer
    // Mensaje de la implementación: "El sistema de identidad rechazó la operación y no se completó. Vuelve a intentarla."
    public ErrorDelProveedorDeIdentidadException(string motivo, Exception? causa = null) : base(string.Empty, causa) =>
        throw new NotImplementedException();

    public string Motivo { get; }
}
