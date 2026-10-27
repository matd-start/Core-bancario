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
    // SDD: esqueleto creado por test-writer
    public SesionDelNavegador(string clave, AreaDeSesion area, string sub, string usuario, string sid, RolDeSesion? rol,
        DateTimeOffset inicio, DateTimeOffset ultimaActividad, TokensDeLaSesion tokens, ClaimsPrincipal principal) =>
        throw new NotImplementedException();

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
    // SDD: esqueleto creado por test-writer
    public void RegistrarActividad(DateTimeOffset ahora) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public void ReemplazarTokens(TokensDeLaSesion tokens) => throw new NotImplementedException();
}
