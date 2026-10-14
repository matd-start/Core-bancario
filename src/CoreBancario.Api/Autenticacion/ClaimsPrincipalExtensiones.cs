using System.Security.Claims;

namespace CoreBancario.Api.Autenticacion;

public static class ClaimsPrincipalExtensiones
{
    /// <summary>El claim "sub" (MapInboundClaims = false). EventosDeJwt garantiza que existe.</summary>
    /// <exception cref="InvalidOperationException">No hay "sub" (error de programación).</exception>
    public static string Sub(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return usuario.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("El usuario autenticado no tiene el claim 'sub'.");
    }
}
