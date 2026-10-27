using System.Text.Json;
using CoreBancario.Bff.Configuracion;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoreBancario.Bff.Sesiones;

/// <summary>Rol de la interfaz a partir del access token (plan 3.1, "De dónde sale rol"). Lee el JWT sin validarlo.</summary>
public static class LectorDeRol
{
    private const string RolDeClientes = "cliente";
    private const string RolDeEmpleados = "operador";

    /// <returns>Cliente si área Clientes y realm_access.roles contiene "cliente"; Operador si Empleados y "operador"; null si no o si el token no se puede leer.</returns>
    public static RolDeSesion? Desde(AreaDeSesion area, string accessToken)
    {
        var roles = LeerRoles(accessToken);
        return area switch
        {
            AreaDeSesion.Clientes when roles.Contains(RolDeClientes) => RolDeSesion.Cliente,
            AreaDeSesion.Empleados when roles.Contains(RolDeEmpleados) => RolDeSesion.Operador,
            _ => null,
        };
    }

    private static HashSet<string> LeerRoles(string accessToken)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(accessToken))
            return roles;

        try
        {
            // Sin validar: el BFF obtuvo este token él mismo por el canal trasero y solo lo usa para la interfaz.
            var token = new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
            if (!token.TryGetPayloadValue<JsonElement>("realm_access", out var acceso)
                || acceso.ValueKind != JsonValueKind.Object
                || !acceso.TryGetProperty("roles", out var lista)
                || lista.ValueKind != JsonValueKind.Array)
                return roles;

            foreach (var rol in lista.EnumerateArray())
            {
                if (rol.ValueKind == JsonValueKind.String && rol.GetString() is { } texto)
                    roles.Add(texto);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or InvalidOperationException)
        {
            roles.Clear();
        }

        return roles;
    }
}
