using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBancario.Infrastructure;

public static class DependencyInjection
{
    /// <summary>AddDbContext con UsarPostgres; registra IUnidadDeTrabajo, repositorios y generador.</summary>
    // SDD: esqueleto creado por test-writer
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuracion)
        => throw new NotImplementedException();
}
