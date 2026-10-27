namespace CoreBancario.Bff.Identidad;

/// <summary>Keycloak no respondió (red, tiempo de espera) o respondió 5xx.</summary>
public sealed class IdentidadSinRespuestaException(Exception? causa = null) : Exception("El sistema de identidad no respondió.", causa);
