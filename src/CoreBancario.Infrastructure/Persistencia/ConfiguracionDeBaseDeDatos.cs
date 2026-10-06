using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia;

public static class ConfiguracionDeBaseDeDatos
{
    /// <summary>Nombre de la cadena en configuración: ConnectionStrings:CoreBancario.</summary>
    public const string NombreCadenaDeConexion = "CoreBancario";

    /// <summary>
    /// UseNpgsql(cadena) + UseSnakeCaseNamingConvention(). Lo usan la DI, la fábrica de diseño y las pruebas:
    /// una sola configuración para que todos hablen con la misma forma de base.
    /// </summary>
    public static DbContextOptionsBuilder UsarPostgres(this DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        return opciones
            .UseNpgsql(cadenaDeConexion)
            .UseSnakeCaseNamingConvention();
    }
}
