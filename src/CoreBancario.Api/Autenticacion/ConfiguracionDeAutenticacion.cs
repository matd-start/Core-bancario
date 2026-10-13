// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

public static class ConfiguracionDeAutenticacion
{
    /// <summary>
    /// AddAuthentication(PorEmisor) + AddPolicyScheme(PorEmisor, ForwardDefaultSelector = SelectorDeEsquema.Elegir)
    /// + AddJwtBearer(Clientes) + AddJwtBearer(Empleados), políticas Cliente y Operador y FallbackPolicy autenticado.
    /// </summary>
    public static IServiceCollection AddAutenticacionConKeycloak(this IServiceCollection services,
        IConfiguration configuracion, IHostEnvironment entorno) =>
        throw new NotImplementedException();
}
