using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Sesiones;

/// <summary>Regla pura de RN-22 con RN-25. "≥": en el instante exacto del límite, ya venció. Si vencen las dos, gana VidaMaxima.</summary>
public static class PoliticaDeSesion
{
    public static EstadoDeSesion Evaluar(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(opciones);

        // Primero la vida máxima: si las dos vencieron a la vez, el motivo que se informa es el que no se puede alargar.
        if (ahora >= sesion.Inicio + opciones.VidaMaxima)
            return EstadoDeSesion.VencidaPorVidaMaxima;

        if (ahora >= sesion.UltimaActividad + opciones.Inactividad)
            return EstadoDeSesion.VencidaPorInactividad;

        return EstadoDeSesion.Vigente;
    }

    public static PlazosDeSesion Plazos(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(opciones);

        return new PlazosDeSesion(
            SinNegativos(sesion.UltimaActividad + opciones.Inactividad - ahora),
            SinNegativos(sesion.Inicio + opciones.VidaMaxima - ahora));
    }

    private static TimeSpan SinNegativos(TimeSpan duracion) => duracion < TimeSpan.Zero ? TimeSpan.Zero : duracion;
}
