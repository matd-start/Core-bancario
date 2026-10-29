using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Clientes;
using CoreBancario.Application.Cuentas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreBancario.Application;

public static class DependencyInjection
{
    /// <summary>Registra los handlers como Scoped y TryAddSingleton(TimeProvider.System).</summary>
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
        services.AddScoped<ListarMisCuentasHandler>();
        services.AddScoped<ObtenerMiCuentaHandler>();
        services.AddScoped<LocalizadorDeAcceso>();
        services.AddScoped<ReconciliadorDeSolicitudes>();
        services.AddScoped<HabilitarAccesoHandler>();
        services.AddScoped<DeshabilitarAccesoHandler>();
        services.AddScoped<ObtenerEstadoDeAccesoHandler>();

        return services;
    }
}
