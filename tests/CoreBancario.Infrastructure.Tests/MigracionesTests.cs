using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Tests;

public class MigracionesTests(PostgresFixture bd)
{
    [Fact]
    public async Task F002_RNF02_BaseVacia_QuedaConTodasLasMigracionesAplicadas()
    {
        // Arrange: el fixture ya aplico MigrateAsync sobre un contenedor vacio.
        await using var db = bd.CrearContexto();

        // Act
        var pendientes = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(pendientes);
        Assert.Contains(aplicadas, migracion => migracion.EndsWith("_Inicial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task F002_RNF02_Modelo_NoTieneCambiosSinMigracion()
    {
        // Arrange
        await using var db = bd.CrearContexto();

        // Act
        var hayCambios = db.Database.HasPendingModelChanges();

        // Assert
        Assert.False(hayCambios);
    }
}
