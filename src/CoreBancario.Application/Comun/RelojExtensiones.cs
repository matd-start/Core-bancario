namespace CoreBancario.Application.Comun;

internal static class RelojExtensiones
{
    private const long TicksPorMicrosegundo = TimeSpan.TicksPerMicrosecond;

    /// <summary>
    /// La hora actual truncada a microsegundos. .NET mide en ticks de 100 ns pero PostgreSQL (timestamptz) guarda
    /// microsegundos: si no se trunca, la respuesta de crear algo (con ticks) difiere de lo que se lee después
    /// (microsegundos). Truncando aquí, lo que se devuelve es exactamente lo que queda guardado.
    /// </summary>
    public static DateTimeOffset AhoraEnMicrosegundos(this TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        return ahora.AddTicks(-(ahora.Ticks % TicksPorMicrosegundo));
    }
}
