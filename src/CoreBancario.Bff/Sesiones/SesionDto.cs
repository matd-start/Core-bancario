namespace CoreBancario.Bff.Sesiones;

/// <summary>Respuesta de /bff/sesion y /bff/actividad. Sin tokens (RNF-01). Segundos truncados hacia abajo.</summary>
public sealed record SesionDto(string Area, string Usuario, string? Rol, int SegundosHastaInactividad, int SegundosHastaVidaMaxima);
