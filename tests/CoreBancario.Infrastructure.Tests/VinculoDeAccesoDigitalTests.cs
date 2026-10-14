using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// Migracion VinculoDeAccesoDigital (003a) y esquema de accesos_digitales (ADR-0016). Se insertan filas por SQL
/// para comprobar lo que garantiza la propia base, sin pasar por el dominio.
/// </summary>
public class VinculoDeAccesoDigitalTests(PostgresFixture bd)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> GuardarClienteAsync()
    {
        var cliente = Ayudas.NuevoCliente();
        await using var db = bd.CrearContexto();
        db.Clientes.Add(cliente);
        await db.GuardarCambiosAsync(Ct);
        return cliente.Id;
    }

    [Fact]
    public async Task F003_RNF02_BaseVacia_TieneLasMigracionesDeLa003()
    {
        // Arrange: el fixture ya aplico MigrateAsync sobre un contenedor vacio.
        await using var db = bd.CrearContexto();

        // Act
        var pendientes = await db.Database.GetPendingMigrationsAsync(Ct);
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(Ct);

        // Assert
        Assert.Empty(pendientes);
        Assert.Contains(aplicadas, migracion => migracion.EndsWith("_VinculoDeAccesoDigital", StringComparison.Ordinal));
        // 003b: el registro de accesos (solicitudes, resultados y el trigger de inmutabilidad).
        Assert.Contains(aplicadas, migracion => migracion.EndsWith("_RegistroDeAccesos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task F003_ADR0016_VinculoValido_SeInsertaSinError()
    {
        // Arrange: control positivo, para que los rechazos siguientes no sean un fallo del propio SQL.
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();

        // Act
        await AyudasDeAcceso.VincularAsync(db, clienteId, AyudasDeAcceso.SubUnico());

        // Assert
        var vinculos = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM accesos_digitales WHERE cliente_id = {clienteId}")
            .SingleAsync(Ct);
        Assert.Equal(1, vinculos);
    }

    [Fact]
    public async Task F003_ADR0016_DosVinculosParaElMismoCliente_LoRechazaLaClavePrimaria()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();
        await AyudasDeAcceso.VincularAsync(db, clienteId, AyudasDeAcceso.SubUnico());

        // Act
        var accion = () => AyudasDeAcceso.VincularAsync(db, clienteId, AyudasDeAcceso.SubUnico());

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pk_accesos_digitales", error.ConstraintName);
    }

    [Fact]
    public async Task F003_ADR0016_DosClientesConElMismoSub_LoRechazaElIndiceUnico()
    {
        // Arrange
        var primero = await GuardarClienteAsync();
        var segundo = await GuardarClienteAsync();
        var sub = AyudasDeAcceso.SubUnico();
        await using var db = bd.CrearContexto();
        await AyudasDeAcceso.VincularAsync(db, primero, sub);

        // Act
        var accion = () => AyudasDeAcceso.VincularAsync(db, segundo, sub);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_accesos_digitales_sub", error.ConstraintName);
    }

    [Fact]
    public async Task F003_ADR0016_VinculoDeClienteInexistente_LoRechazaLaClaveForanea()
    {
        // Arrange
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => AyudasDeAcceso.VincularAsync(db, Guid.NewGuid(), AyudasDeAcceso.SubUnico());

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task F003_ADR0016_BorrarUnClienteConVinculo_LoImpideLaClaveForaneaRestrict()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();
        await AyudasDeAcceso.VincularAsync(db, clienteId, AyudasDeAcceso.SubUnico());

        // Act
        var accion = () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM clientes WHERE id = {clienteId}", Ct);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task F003_ADR0016_SubDeMasDe255Caracteres_LoRechazaLaColumna()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => AyudasDeAcceso.VincularAsync(db, clienteId, new string('a', 256));

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, error.SqlState);
    }
}
