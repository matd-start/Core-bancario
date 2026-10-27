using System.Diagnostics.CodeAnalysis;
using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>CL-08: solo rutas internas de la SPA del mismo área. Nunca devuelve una URL absoluta (sin redirección abierta).</summary>
public static class RutaDeRegreso
{
    public const int LongitudMaxima = 2048;

    private const string Backoffice = "/backoffice";

    public static string Normalizar(AreaDeSesion area, string? regreso)
    {
        if (!EsRutaInterna(regreso) || EsDelBffOApi(regreso) || EsDelBackoffice(regreso) != (area == AreaDeSesion.Empleados))
            return AreasDeSesion.InicioDeLaSpa(area);

        return regreso;
    }

    private static bool EsRutaInterna([NotNullWhen(true)] string? regreso)
    {
        if (string.IsNullOrEmpty(regreso) || regreso.Length > LongitudMaxima)
            return false;

        // Debe empezar por "/" y no por "//" ni "/\": el navegador los leería como otro sitio (redirección abierta).
        if (regreso[0] != '/' || (regreso.Length > 1 && (regreso[1] == '/' || regreso[1] == '\\')))
            return false;

        return !regreso.Contains('\\')
            && !regreso.Contains("://", StringComparison.Ordinal)
            && !regreso.Any(char.IsControl);
    }

    private static bool EsDelBffOApi(string ruta) =>
        ruta.StartsWith("/bff", StringComparison.OrdinalIgnoreCase) || ruta.StartsWith("/api", StringComparison.OrdinalIgnoreCase);

    private static bool EsDelBackoffice(string ruta) =>
        ruta.Equals(Backoffice, StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWith(Backoffice + "/", StringComparison.OrdinalIgnoreCase);
}
