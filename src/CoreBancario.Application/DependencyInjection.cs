using CoreBancario.Application.Clientes;
using CoreBancario.Application.Cuentas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreBancario.Application;

public static class DependencyInjection
{
    /// <summary>Registra los seis handlers como Scoped y TryAddSingleton(TimeProvider.System).</summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: si quien llama (o una prueba) ya registró otro TimeProvider, se respeta.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<RegistrarClienteHandler>();
        services.AddScoped<ObtenerClienteHandler>();
        services.AddScoped<BuscarClientePorDocumentoHandler>();
        services.AddScoped<AbrirCuentaHandler>();
        services.AddScoped<CambiarEstadoDeCuentaHandler>();
        services.AddScoped<ObtenerCuentaHandler>();

        return services;
    }
}
