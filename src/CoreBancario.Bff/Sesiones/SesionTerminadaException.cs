namespace CoreBancario.Bff.Sesiones;

/// <summary>La sesión no existe o terminó. Se traduce a 401 con el código de CodigosDelBff.De(motivo).</summary>
public sealed class SesionTerminadaException(MotivoDeCierre motivo) : Exception
{
    public MotivoDeCierre Motivo { get; } = motivo;
}
