using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Infrastructure.AccesosDigitales;
using CoreBancario.Infrastructure.Cuentas;
using CoreBancario.Infrastructure.Identidad;
using CoreBancario.Infrastructure.Persistencia;
using CoreBancario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CoreBancario.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// AddDbContext con UsarPostgres; la cadena se lee de configuración DENTRO del callback de opciones (perezoso),
    /// para que WebApplicationFactory pueda sobrescribirla. Si falta → InvalidOperationException con mensaje claro.
    /// Registra IUnidadDeTrabajo (el mismo DbContext del scope), los repositorios y `CuentasDelTitular` (Scoped) y el generador (Singleton).
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
        services.AddScoped<ICuentasDelTitular, CuentasDelTitular>();
        services.AddScoped<IAccesoDigitalRepositorio, AccesoDigitalRepositorio>();
        services.AddScoped<IRegistroDeAccesos, RegistroDeAccesos>();
        services.AddSingleton<IGeneradorDeClaveTemporal, GeneradorAleatorioDeClaveTemporal>();
        services.AddSingleton<IGeneradorDeNumeroDeCuenta, GeneradorAleatorioDeNumeroDeCuenta>();

        AgregarProveedorDeIdentidad(services, configuracion);

        return services;
    }

    /// <summary>
    /// Adaptador de la Admin REST API de Keycloak. Como la cadena de conexión, la configuración se lee de forma
    /// perezosa (al crear el cliente HTTP): la Api arranca aunque falte y las pruebas sustituyen el puerto.
    /// Hay dos clientes HTTP: el del adaptador (con el handler que añade el token) y uno sin handler para pedir el token.
    /// </summary>
    private static void AgregarProveedorDeIdentidad(IServiceCollection services, IConfiguration configuracion)
    {
        services.AddOptions<OpcionesDeKeycloak>().Bind(configuracion.GetSection(OpcionesDeKeycloak.Seccion));
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<TokenDeServicioHandler>();

        services.AddHttpClient(TokenDeServicioHandler.NombreDelClienteDeToken, (proveedor, cliente) =>
            cliente.Timeout = proveedor.GetRequiredService<IOptions<OpcionesDeKeycloak>>().Value.TiempoDeEspera);

        services.AddHttpClient<IProveedorDeIdentidad, ProveedorDeIdentidadKeycloak>((proveedor, cliente) =>
            {
                var opciones = proveedor.GetRequiredService<IOptions<OpcionesDeKeycloak>>().Value;
                cliente.BaseAddress = opciones.UrlBase;
                cliente.Timeout = opciones.TiempoDeEspera;
            })
            .AddHttpMessageHandler<TokenDeServicioHandler>();
    }
}
