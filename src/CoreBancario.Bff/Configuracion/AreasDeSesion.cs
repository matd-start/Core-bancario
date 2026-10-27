namespace CoreBancario.Bff.Configuracion;

public static class AreasDeSesion
{
    /// <summary>"clientes" / "empleados": segmento de ruta del BFF y valor de SesionDto.Area.</summary>
    public static string Segmento(AreaDeSesion area) => area switch
    {
        AreaDeSesion.Clientes => "clientes",
        AreaDeSesion.Empleados => "empleados",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    /// <summary>Sin distinguir mayúsculas. false con null, vacío u otro texto.</summary>
    public static bool TryParse(string? segmento, out AreaDeSesion area)
    {
        if (string.Equals(segmento, "clientes", StringComparison.OrdinalIgnoreCase))
        {
            area = AreaDeSesion.Clientes;
            return true;
        }

        if (string.Equals(segmento, "empleados", StringComparison.OrdinalIgnoreCase))
        {
            area = AreaDeSesion.Empleados;
            return true;
        }

        area = default;
        return false;
    }

    /// <summary>"/" (Clientes) o "/backoffice" (Empleados): donde la SPA muestra el inicio y los avisos.</summary>
    public static string PrefijoDeLaSpa(AreaDeSesion area) => area == AreaDeSesion.Clientes ? "/" : "/backoffice";

    /// <summary>"/mis-cuentas" o "/backoffice": pantalla inicial con sesión.</summary>
    public static string InicioDeLaSpa(AreaDeSesion area) => area == AreaDeSesion.Clientes ? "/mis-cuentas" : "/backoffice";

    /// <summary>"OidcClientes" / "OidcEmpleados".</summary>
    public static string EsquemaOidc(AreaDeSesion area) => area == AreaDeSesion.Clientes ? "OidcClientes" : "OidcEmpleados";

    /// <summary>"/bff/signin-oidc/clientes" / "/bff/signin-oidc/empleados".</summary>
    public static string RutaDeRegresoOidc(AreaDeSesion area) => $"/bff/signin-oidc/{Segmento(area)}";
}
