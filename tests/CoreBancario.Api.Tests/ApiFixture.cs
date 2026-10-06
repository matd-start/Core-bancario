using CoreBancario.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CoreBancario.Api.Tests.ApiFixture))]

namespace CoreBancario.Api.Tests;

/// <summary>
/// Un contenedor PostgreSQL y una WebApplicationFactory por proyecto de pruebas, compartidos por todas sus clases
/// (xUnit v3 AssemblyFixture). El contenedor usa la misma imagen que docker-compose.yml y las migraciones reales
/// (RNF-02). El aislamiento entre pruebas es por datos unicos (ver Ayudas), sin limpiar la base.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _contenedor.StartAsync();

        var opciones = new DbContextOptionsBuilder<CoreBancarioDbContext>();
        opciones.UsarPostgres(_contenedor.GetConnectionString());
        await using (var db = new CoreBancarioDbContext(opciones.Options))
            await db.Database.MigrateAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            constructor => constructor.UseSetting("ConnectionStrings:CoreBancario", _contenedor.GetConnectionString()));
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        await _contenedor.DisposeAsync();
    }

    /// <summary>Un cliente HTTP contra la API real (sin simulaciones).</summary>
    public HttpClient CrearCliente() => Factory.CreateClient();

    /// <summary>Una API derivada que sustituye servicios (por ejemplo, el generador de numeros o el reloj).</summary>
    public WebApplicationFactory<Program> ConServicios(Action<IServiceCollection> configurar) =>
        Factory.WithWebHostBuilder(constructor => constructor.ConfigureTestServices(configurar));
}
