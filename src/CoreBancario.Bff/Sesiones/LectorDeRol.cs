using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Sesiones;

/// <summary>Rol de la interfaz a partir del access token (plan 3.1, "De dónde sale rol"). Lee el JWT sin validarlo.</summary>
public static class LectorDeRol
{
    /// <returns>Cliente si área Clientes y realm_access.roles contiene "cliente"; Operador si Empleados y "operador"; null si no o si el token no se puede leer.</returns>
    // SDD: esqueleto creado por test-writer
    public static RolDeSesion? Desde(AreaDeSesion area, string accessToken) => throw new NotImplementedException();
}
