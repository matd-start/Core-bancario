namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>CL-14: deshabilitar un acceso que no existe o que ya está deshabilitado. 422 acceso-no-habilitado.</summary>
public sealed class AccesoNoHabilitadoException : ReglaDeNegocioException
{
    public AccesoNoHabilitadoException(EstadoDeAcceso estadoActual)
        : base("El cliente no tiene un acceso digital habilitado.") =>
        EstadoActual = estadoActual;

    public EstadoDeAcceso EstadoActual { get; }
}
