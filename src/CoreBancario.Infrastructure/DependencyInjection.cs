using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Infrastructure.Cuentas;
using CoreBancario.Infrastructure.Persistencia;
using CoreBancario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBancario.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// AddDbContext con UsarPostgres; la cadena se lee de configuración DENTRO del callback de opciones (perezoso),
    /// para que WebApplicationFactory pueda sobrescribirla. Si falta → InvalidOperationException con mensaje claro.
    /// Registra IUnidadDeTrabajo (el mismo DbContext del scope), los dos repositorios (Scoped) y el generador (Singleton).
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuracion);

        services.AddDbContext<CoreBancarioDbContext>(opciones =>
        {
            var cadena = configuracion.GetConnectionString(ConfiguracionDeBaseDeDatos.NombreCadenaDeConexion);
            if (string.IsNullOrWhiteSpace(cadena))
            {
                throw new InvalidOperationException(
                    $"Falta la cadena de conexión 'ConnectionStrings:{ConfiguracionDeBaseDeDatos.NombreCadenaDeConexion}'. " +
                    "Defínala con 'dotnet user-secrets' o con la variable de entorno " +
                    $"'ConnectionStrings__{ConfiguracionDeBaseDeDatos.NombreCadenaDeConexion}'.");
            }

            opciones.UsarPostgres(cadena);
        });

        services.AddScoped<IUnidadDeTrabajo>(proveedor => proveedor.GetRequiredService<CoreBancarioDbContext>());
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<ICuentaRepositorio, CuentaRepositorio>();
        services.AddSingleton<IGeneradorDeNumeroDeCuenta, GeneradorAleatorioDeNumeroDeCuenta>();

        return services;
    }
}
