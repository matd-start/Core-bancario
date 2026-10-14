namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>CL-14: deshabilitar un acceso que no existe o que ya está deshabilitado. 422 acceso-no-habilitado.</summary>
public sealed class AccesoNoHabilitadoException : ReglaDeNegocioException
{
    // SDD: esqueleto creado por test-writer
    // Mensaje de la implementación: "El cliente no tiene un acceso digital habilitado."
    public AccesoNoHabilitadoException(EstadoDeAcceso estadoActual) : base(string.Empty) =>
        throw new NotImplementedException();

    public EstadoDeAcceso EstadoActual { get; }
}
