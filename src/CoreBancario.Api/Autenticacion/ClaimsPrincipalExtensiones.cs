// SDD: esqueleto creado por test-writer
using System.Security.Claims;

namespace CoreBancario.Api.Autenticacion;

public static class ClaimsPrincipalExtensiones
{
    /// <summary>El claim "sub" (MapInboundClaims = false). EventosDeJwt garantiza que existe.</summary>
    /// <exception cref="InvalidOperationException">No hay "sub" (error de programación).</exception>
    public static string Sub(this ClaimsPrincipal usuario) => throw new NotImplementedException();
}
