namespace CoreBancario.Bff.InicioDeSesion;

public static class ConfiguracionDeSesion
{
    public const string EsquemaDeCookie = "Sesion";

    /// <summary>"__Host-cb-sesion" fuera de Development; "cb-sesion" en Development (HTTP).</summary>
    // SDD: esqueleto creado por test-writer
    public static string NombreDeLaCookie(IHostEnvironment entorno) => throw new NotImplementedException();

    /// <summary>AddAuthentication(Sesion) + cookie + dos AddOpenIdConnect + almacén, servicio, cliente de identidad y reloj.</summary>
    // SDD: esqueleto creado por test-writer
    public static IServiceCollection AddSesionDelNavegador(this IServiceCollection services, IConfiguration configuracion,
        IHostEnvironment entorno) => throw new NotImplementedException();
}
