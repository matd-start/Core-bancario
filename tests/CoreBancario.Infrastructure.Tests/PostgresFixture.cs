using CoreBancario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CoreBancario.Infrastructure.Tests.PostgresFixture))]

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// Un contenedor PostgreSQL por proyecto de pruebas, compartido por todas sus clases (xUnit v3 AssemblyFixture).
/// Arranca con la misma imagen que docker-compose.yml y aplica las migraciones reales (RNF-02), nunca EnsureCreated.
/// El aislamiento entre pruebas es por datos unicos (ver Ayudas), sin limpiar la base.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17").Build();

    public string CadenaDeConexion => _contenedor.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _contenedor.StartAsync();

        await using var db = CrearContexto();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _contenedor.DisposeAsync();

    /// <summary>Un DbContext nuevo con la misma configuracion que usa la aplicacion (UsarPostgres).</summary>
    public CoreBancarioDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<CoreBancarioDbContext>();
        opciones.UsarPostgres(CadenaDeConexion);
        return new CoreBancarioDbContext(opciones.Options);
    }
}
