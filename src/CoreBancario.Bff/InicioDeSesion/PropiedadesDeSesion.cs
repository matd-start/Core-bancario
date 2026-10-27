using Microsoft.AspNetCore.Authentication;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>Claves de AuthenticationProperties.Items que usa el BFF.</summary>
public static class PropiedadesDeSesion
{
    public const string Area = "cb:area";
    public const string Clave = "cb:clave";
    public const string Accion = "cb:accion";
    public const string Inicio = "cb:inicio";

    /// <summary>La clave de la sesión de esta petición (Items[Clave] del ticket de la cookie), o null si no hay cookie válida.</summary>
    public static async Task<string?> LeerClaveAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var cookie = await contexto.AuthenticateAsync(ConfiguracionDeSesion.EsquemaDeCookie);
        return cookie.Succeeded && cookie.Properties is not null && cookie.Properties.Items.TryGetValue(Clave, out var clave)
            ? clave
            : null;
    }
}
