using System.Security.Claims;
using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Sesiones;

/// <summary>
/// Sesión de un navegador (ADR-0020, ADR-0021). Inicio = auth_time del token de identidad. UltimaActividad solo la
/// cambian el regreso del login y el latido (RN-25). Mutable solo con sus dos métodos.
/// </summary>
public sealed class SesionDelNavegador
{
    /// <exception cref="ArgumentException">clave, sub, usuario o sid vacíos, o ultimaActividad &lt; inicio.</exception>
    public SesionDelNavegador(string clave, AreaDeSesion area, string sub, string usuario, string sid, RolDeSesion? rol,
        DateTimeOffset inicio, DateTimeOffset ultimaActividad, TokensDeLaSesion tokens, ClaimsPrincipal principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(principal);
        if (ultimaActividad < inicio)
            throw new ArgumentException("La última actividad no puede ser anterior al inicio de la sesión.", nameof(ultimaActividad));

        Clave = clave;
        Area = area;
        Sub = sub;
        Usuario = usuario;
        Sid = sid;
        Rol = rol;
        Inicio = inicio;
        UltimaActividad = ultimaActividad;
        Tokens = tokens;
        Principal = principal;
    }

    public string Clave { get; }

    public AreaDeSesion Area { get; }

    public string Sub { get; }

    /// <summary>preferred_username, como "cc1234567".</summary>
    public string Usuario { get; }

    /// <summary>Id de la sesión de Keycloak (claim "sid").</summary>
    public string Sid { get; }

    public RolDeSesion? Rol { get; }

    public DateTimeOffset Inicio { get; }

    public DateTimeOffset UltimaActividad { get; private set; }

    public TokensDeLaSesion Tokens { get; private set; }

    public ClaimsPrincipal Principal { get; }

    /// <summary>Nunca retrocede: si ahora &lt; UltimaActividad, no cambia.</summary>
    public void RegistrarActividad(DateTimeOffset ahora)
    {
        if (ahora > UltimaActividad)
            UltimaActividad = ahora;
    }

    public void ReemplazarTokens(TokensDeLaSesion tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Tokens = tokens;
    }
}
