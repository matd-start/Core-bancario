namespace CoreBancario.Bff.Configuracion;

// SDD: esqueleto creado por test-writer
public static class AreasDeSesion
{
    /// <summary>"clientes" / "empleados": segmento de ruta del BFF y valor de SesionDto.Area.</summary>
    public static string Segmento(AreaDeSesion area) => throw new NotImplementedException();

    /// <summary>Sin distinguir mayúsculas. false con null, vacío u otro texto.</summary>
    public static bool TryParse(string? segmento, out AreaDeSesion area) => throw new NotImplementedException();

    /// <summary>"/" (Clientes) o "/backoffice" (Empleados): donde la SPA muestra el inicio y los avisos.</summary>
    public static string PrefijoDeLaSpa(AreaDeSesion area) => throw new NotImplementedException();

    /// <summary>"/mis-cuentas" o "/backoffice": pantalla inicial con sesión.</summary>
    public static string InicioDeLaSpa(AreaDeSesion area) => throw new NotImplementedException();

    /// <summary>"OidcClientes" / "OidcEmpleados".</summary>
    public static string EsquemaOidc(AreaDeSesion area) => throw new NotImplementedException();

    /// <summary>"/bff/signin-oidc/clientes" / "/bff/signin-oidc/empleados".</summary>
    public static string RutaDeRegresoOidc(AreaDeSesion area) => throw new NotImplementedException();
}
