using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// Defensa en profundidad: aunque el dominio tuviera un error, la base rechaza los estados imposibles.
/// Se inserta por SQL para saltarse el dominio a proposito.
/// </summary>
public class RestriccionesDeLaBaseTests(PostgresFixture bd)
{
    private const string CodigoCheckViolation = "23514";

    private async Task<Guid> GuardarClienteAsync()
    {
        var cliente = Ayudas.NuevoCliente();
        await using var db = bd.CrearContexto();
        db.Clientes.Add(cliente);
        await db.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        return cliente.Id;
    }

    private async Task InsertarCuentaPorSqlAsync(string moneda, string estado, decimal saldo, string saldoMoneda,
        DateTimeOffset? fechaCierre)
    {
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO cuentas (id, numero, cliente_id, moneda, estado, saldo, saldo_moneda, fecha_apertura, fecha_cierre)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, @fc)
            """,
            [
                Guid.CreateVersion7(),
                Ayudas.NumeroDeCuentaUnico(),
                clienteId,
                moneda,
                estado,
                saldo,
                saldoMoneda,
                Ayudas.Instante,
                new NpgsqlParameter { ParameterName = "fc", NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)fechaCierre ?? DBNull.Value },
            ],
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RN01_SaldoNegativoPorSql_LoRechazaElCheck()
    {
        // Arrange / Act
        var accion = () => InsertarCuentaPorSqlAsync("COP", "Activa", -1m, "COP", null);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(CodigoCheckViolation, error.SqlState);
        Assert.Equal("ck_cuentas_saldo_no_negativo", error.ConstraintName);
    }

    [Fact]
    public async Task RN02_SaldoEnOtraMonedaPorSql_LoRechazaElCheck()
    {
        // Arrange / Act
        var accion = () => InsertarCuentaPorSqlAsync("COP", "Activa", 0m, "USD", null);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(CodigoCheckViolation, error.SqlState);
        Assert.Equal("ck_cuentas_saldo_en_moneda_de_la_cuenta", error.ConstraintName);
    }

    [Fact]
    public async Task CA12_FechaDeCierreEnCuentaActivaPorSql_LoRechazaElCheck()
    {
        // Arrange / Act
        var accion = () => InsertarCuentaPorSqlAsync("COP", "Activa", 0m, "COP", Ayudas.Instante);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(CodigoCheckViolation, error.SqlState);
        Assert.Equal("ck_cuentas_fecha_cierre_solo_si_cerrada", error.ConstraintName);
    }

    [Fact]
    public async Task CA12_CuentaCerradaSinFechaDeCierrePorSql_LoRechazaElCheck()
    {
        // Arrange / Act
        var accion = () => InsertarCuentaPorSqlAsync("COP", "Cerrada", 0m, "COP", null);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(CodigoCheckViolation, error.SqlState);
        Assert.Equal("ck_cuentas_fecha_cierre_solo_si_cerrada", error.ConstraintName);
    }

    [Fact]
    public async Task CA12_CuentaCerradaConFechaDeCierrePorSql_SeInsertaSinError()
    {
        // Arrange / Act: control positivo, para que los cuatro rechazos anteriores no sean un fallo del propio SQL.
        var accion = () => InsertarCuentaPorSqlAsync("USD", "Cerrada", 0m, "USD", Ayudas.Instante);

        // Assert
        await accion();
    }
}
