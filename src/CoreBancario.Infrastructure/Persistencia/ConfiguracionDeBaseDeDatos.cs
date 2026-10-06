using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia;

public static class ConfiguracionDeBaseDeDatos
{
    /// <summary>Nombre de la cadena en configuracion: ConnectionStrings:CoreBancario.</summary>
    public const string NombreCadenaDeConexion = "CoreBancario";

    /// <summary>UseNpgsql(cadena) + UseSnakeCaseNamingConvention().</summary>
    // SDD: esqueleto creado por test-writer
    public static DbContextOptionsBuilder UsarPostgres(this DbContextOptionsBuilder opciones, string cadenaDeConexion)
        => throw new NotImplementedException();
}
