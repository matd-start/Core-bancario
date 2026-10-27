using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.InicioDeSesion;

public static class Avisos
{
    public const string InicioFallido = "inicio-fallido";
    public const string IdentidadNoDisponible = "identidad-no-disponible";
    public const string OtraSesionAbierta = "otra-sesion-abierta";
    public const string ClaveActualizada = "clave-actualizada";
    public const string ClaveSinCambios = "clave-sin-cambios";

    /// <summary>"{PrefijoDeLaSpa(area)}?aviso={aviso}" (Clientes: "/?aviso=…").</summary>
    // SDD: esqueleto creado por test-writer
    public static string UrlDe(AreaDeSesion area, string aviso) => throw new NotImplementedException();
}
