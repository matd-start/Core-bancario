using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Sesiones;

/// <summary>Regla pura de RN-22 con RN-25. "≥": en el instante exacto del límite, ya venció. Si vencen las dos, gana VidaMaxima.</summary>
public static class PoliticaDeSesion
{
    // SDD: esqueleto creado por test-writer
    public static EstadoDeSesion Evaluar(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora) =>
        throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static PlazosDeSesion Plazos(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora) =>
        throw new NotImplementedException();
}
