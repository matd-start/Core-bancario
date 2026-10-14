using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoreBancario.Api.Autenticacion;

public static class SelectorDeEsquema
{
    private const string PrefijoBearer = "Bearer ";

    /// <summary>
    /// Lee el "iss" del Bearer SIN validarlo, solo para elegir quién lo valida.
    /// iss == Empleados.Emisor → Empleados. Cualquier otro caso (sin cabecera, ilegible, otro emisor) → Clientes,
    /// que lo rechazará y registrará el motivo. Nunca lanza.
    /// </summary>
    public static string Elegir(HttpContext contexto, OpcionesDeAutenticacion opciones)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(opciones);

        var emisor = LeerEmisor(contexto.Request.Headers.Authorization.ToString());

        return emisor is not null && emisor == opciones.Empleados.Emisor
            ? EsquemasDeAutenticacion.Empleados
            : EsquemasDeAutenticacion.Clientes;
    }

    // Aquí no se valida nada: un token falso que diga ser de "empleados" solo logra que lo valide el esquema de
    // empleados, que lo rechaza por firma. Elegir mal nunca concede acceso.
    private static string? LeerEmisor(string cabecera)
    {
        if (!cabecera.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = cabecera[PrefijoBearer.Length..].Trim();

        try
        {
            return new JsonWebToken(token).Issuer;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or SecurityTokenException or JsonException or FormatException)
        {
            return null;
        }
    }
}
