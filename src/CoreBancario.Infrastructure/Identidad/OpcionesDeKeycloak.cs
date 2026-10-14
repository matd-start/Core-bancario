namespace CoreBancario.Infrastructure.Identidad;

/// <summary>Sección "Keycloak:Administracion". ClientSecret NUNCA en el repositorio: user-secrets o variable de entorno.</summary>
public sealed class OpcionesDeKeycloak
{
    public const string Seccion = "Keycloak:Administracion";

    /// <summary>http://localhost:8080</summary>
    public Uri? UrlBase { get; set; }

    public string Realm { get; set; } = "clientes";

    public string ClientId { get; set; } = "core-bancario-api-admin";

    public string ClientSecret { get; set; } = "";

    public TimeSpan TiempoDeEspera { get; set; } = TimeSpan.FromSeconds(10);
}
