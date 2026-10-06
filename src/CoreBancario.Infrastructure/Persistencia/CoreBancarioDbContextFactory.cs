using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoreBancario.Infrastructure.Persistencia;

/// <summary>
/// Para `dotnet ef`: lee la variable de entorno ConnectionStrings__CoreBancario o usa una cadena de relleno
/// (`migrations add` no se conecta a la base, solo necesita saber qué proveedor usar).
/// </summary>
public sealed class CoreBancarioDbContextFactory : IDesignTimeDbContextFactory<CoreBancarioDbContext>
{
    private const string CadenaDeRelleno = "Host=localhost;Database=diseno";

    public CoreBancarioDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable($"ConnectionStrings__{ConfiguracionDeBaseDeDatos.NombreCadenaDeConexion}")
            ?? CadenaDeRelleno;

        var opciones = new DbContextOptionsBuilder<CoreBancarioDbContext>();
        opciones.UsarPostgres(cadena);

        return new CoreBancarioDbContext(opciones.Options);
    }
}
