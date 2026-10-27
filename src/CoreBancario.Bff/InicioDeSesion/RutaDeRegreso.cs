using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>CL-08: solo rutas internas de la SPA del mismo área. Nunca devuelve una URL absoluta (sin redirección abierta).</summary>
public static class RutaDeRegreso
{
    public const int LongitudMaxima = 2048;

    // SDD: esqueleto creado por test-writer
    public static string Normalizar(AreaDeSesion area, string? regreso) => throw new NotImplementedException();
}
