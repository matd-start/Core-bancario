using CoreBancario.Bff.Sesiones;

namespace CoreBancario.Bff.Errores;

/// <summary>Contrato estable con la SPA (como ADR-0011). Nunca se renombran.</summary>
public static class CodigosDelBff
{
    public const string SinSesion = "sin-sesion";
    public const string SesionVencidaPorInactividad = "sesion-vencida-por-inactividad";
    public const string SesionVencidaPorVidaMaxima = "sesion-vencida-por-vida-maxima";
    public const string SesionRevocada = "sesion-revocada";
    public const string PeticionNoConfiable = "peticion-no-confiable";
    public const string IdentidadSinRespuesta = "identidad-sin-respuesta";

    public static string De(MotivoDeCierre motivo) => motivo switch
    {
        MotivoDeCierre.SinSesion => SinSesion,
        MotivoDeCierre.Inactividad => SesionVencidaPorInactividad,
        MotivoDeCierre.VidaMaxima => SesionVencidaPorVidaMaxima,
        MotivoDeCierre.Revocada => SesionRevocada,
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };
}
