namespace CoreBancario.Bff.Sesiones;

/// <summary>Duraciones (nunca negativas) hasta cada cierre; la SPA las convierte con su hora local (ADR-0021).</summary>
public sealed record PlazosDeSesion(TimeSpan HastaInactividad, TimeSpan HastaVidaMaxima);
