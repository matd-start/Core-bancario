using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Infrastructure.Persistencia;
using CoreBancario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// Migración RegistroDeAccesos (003b), RN-20 y ADR-0017: el registro de accesos es inmutable y la base lo garantiza.
/// Los rechazos de la base se comprueban insertando por SQL, sin pasar por el dominio; la persistencia de las
/// entidades y la traducción de errores, con el repositorio real.
/// </summary>
public class RegistroDeAccesosTests(PostgresFixture bd)
{
    private const string OperadorDePruebas = "operador-de-pruebas";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>La consulta de solicitudes sin resultado, tal como la documenta el plan (sección 4) y el README.</summary>
    private const string ConsultaDocumentada = """
        SELECT s.id, s.cliente_id, s.operador, s.accion, s.fecha
        FROM solicitudes_de_acceso s
        LEFT JOIN resultados_de_acceso r ON r.solicitud_id = s.id
        WHERE r.solicitud_id IS NULL
        ORDER BY s.fecha;
        """;

    private async Task<Guid> GuardarClienteAsync()
    {
        var cliente = Ayudas.NuevoCliente();
        await using var db = bd.CrearContexto();
        db.Clientes.Add(cliente);
        await db.GuardarCambiosAsync(Ct);
        return cliente.Id;
    }

    private static Task InsertarSolicitudAsync(CoreBancarioDbContext db, Guid id, Guid clienteId,
        string accion = "Habilitar", DateTimeOffset? fecha = null) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO solicitudes_de_acceso (id, cliente_id, operador, accion, fecha) VALUES ({id}, {clienteId}, {OperadorDePruebas}, {accion}, {fecha ?? Ayudas.Instante})",
            Ct);

    private static Task InsertarResultadoAsync(CoreBancarioDbContext db, Guid solicitudId,
        string resultado = "Completado", string? motivo = null) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO resultados_de_acceso (solicitud_id, resultado, fecha, motivo) VALUES ({solicitudId}, {resultado}, {Ayudas.Instante}, {motivo})",
            Ct);

    /// <summary>Un cliente con una solicitud y su resultado ya guardados, por SQL.</summary>
    private async Task<(Guid ClienteId, Guid SolicitudId)> SolicitudConResultadoAsync()
    {
        var clienteId = await GuardarClienteAsync();
        var solicitudId = Guid.CreateVersion7();
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, solicitudId, clienteId);
        await InsertarResultadoAsync(db, solicitudId);
        return (clienteId, solicitudId);
    }

    /// <summary>Ejecuta ConsultaDocumentada con ADO.NET, sin pasar por EF Core, y devuelve la columna id.</summary>
    private async Task<List<Guid>> EjecutarLaConsultaDocumentadaAsync()
    {
        var ids = new List<Guid>();
        await using var conexion = new NpgsqlConnection(bd.CadenaDeConexion);
        await conexion.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand(ConsultaDocumentada, conexion);
        await using var lector = await comando.ExecuteReaderAsync(Ct);
        while (await lector.ReadAsync(Ct))
            ids.Add(lector.GetGuid(0));

        return ids;
    }

    private static async Task<int> ContarSolicitudesAsync(CoreBancarioDbContext db, Guid solicitudId) =>
        await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM solicitudes_de_acceso WHERE id = {solicitudId}")
            .SingleAsync(Ct);

    private static async Task<int> ContarResultadosAsync(CoreBancarioDbContext db, Guid solicitudId) =>
        await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM resultados_de_acceso WHERE solicitud_id = {solicitudId}")
            .SingleAsync(Ct);

    // ---- RN-20: el esquema acepta lo válido ----

    [Fact]
    public async Task F003_RN20_SolicitudYResultadoValidos_SeInsertanPorSql()
    {
        // Arrange: control positivo, para que los rechazos siguientes no sean un fallo del propio SQL.
        var clienteId = await GuardarClienteAsync();
        var solicitudId = Guid.CreateVersion7();
        await using var db = bd.CrearContexto();

        // Act
        await InsertarSolicitudAsync(db, solicitudId, clienteId);
        await InsertarResultadoAsync(db, solicitudId, "Fallido", "HTTP 500 al restablecer la clave");

        // Assert
        Assert.Equal(1, await ContarSolicitudesAsync(db, solicitudId));
        Assert.Equal(1, await ContarResultadosAsync(db, solicitudId));
    }

    // ---- RN-20: inmutabilidad (trigger BEFORE UPDATE OR DELETE) ----

    [Theory]
    [InlineData("UPDATE solicitudes_de_acceso SET operador = 'otro-operador' WHERE id = {0}")]
    [InlineData("DELETE FROM solicitudes_de_acceso WHERE id = {0}")]
    [InlineData("UPDATE resultados_de_acceso SET resultado = 'Fallido' WHERE solicitud_id = {0}")]
    [InlineData("DELETE FROM resultados_de_acceso WHERE solicitud_id = {0}")]
    public async Task F003_RN20_EditarOBorrarElRegistro_LoRechazaLaBase(string sentencia)
    {
        // Arrange
        var (_, solicitudId) = await SolicitudConResultadoAsync();
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => db.Database.ExecuteSqlRawAsync(sentencia, [solicitudId], Ct);

        // Assert: lo rechaza el trigger (excepción de PL/pgSQL), no una clave foránea ni un CHECK.
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        await using var verificacion = bd.CrearContexto();
        Assert.Equal(1, await ContarSolicitudesAsync(verificacion, solicitudId));
        Assert.Equal(1, await ContarResultadosAsync(verificacion, solicitudId));
    }

    // ---- RN-20: restricciones del esquema ----

    [Fact]
    public async Task F003_RN20_AccionDesconocida_LoRechazaElCheck()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => InsertarSolicitudAsync(db, Guid.CreateVersion7(), clienteId, accion: "Borrar");

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_solicitudes_de_acceso_accion", error.ConstraintName);
    }

    [Fact]
    public async Task F003_RN20_ResultadoDesconocido_LoRechazaElCheck()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        var solicitudId = Guid.CreateVersion7();
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, solicitudId, clienteId);

        // Act
        var accion = () => InsertarResultadoAsync(db, solicitudId, resultado: "Pendiente");

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_resultados_de_acceso_resultado", error.ConstraintName);
    }

    [Fact]
    public async Task F003_RN20_SolicitudDeClienteInexistente_LoRechazaLaClaveForanea()
    {
        // Arrange
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => InsertarSolicitudAsync(db, Guid.CreateVersion7(), Guid.NewGuid());

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task F003_RN20_ResultadoDeSolicitudInexistente_LoRechazaLaClaveForanea()
    {
        // Arrange
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => InsertarResultadoAsync(db, Guid.CreateVersion7());

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task F003_RN20_DosResultadosParaLaMismaSolicitud_LoRechazaLaClavePrimaria()
    {
        // Arrange
        var (_, solicitudId) = await SolicitudConResultadoAsync();
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => InsertarResultadoAsync(db, solicitudId, "Fallido", "segundo cierre");

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pk_resultados_de_acceso", error.ConstraintName);
    }

    [Fact]
    public async Task F003_RN20_MotivoDeMasDe200Caracteres_LoRechazaLaColumna()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        var solicitudId = Guid.CreateVersion7();
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, solicitudId, clienteId);

        // Act
        var accion = () => InsertarResultadoAsync(db, solicitudId, "Fallido", new string('m', 201));

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, error.SqlState);
    }

    [Fact]
    public async Task F003_RN20_BorrarUnClienteConSolicitudes_LoImpideLaClaveForaneaRestrict()
    {
        // Arrange
        var (clienteId, _) = await SolicitudConResultadoAsync();
        await using var db = bd.CrearContexto();

        // Act
        var accion = () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM clientes WHERE id = {clienteId}", Ct);

        // Assert
        var error = await Assert.ThrowsAsync<PostgresException>(accion);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    // ---- RN-20: el repositorio persiste las entidades ----

    [Fact]
    public async Task F003_RN20_AgregarSolicitudYResultado_SePersistenConSusColumnas()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        var solicitud = SolicitudDeAcceso.ParaHabilitar(clienteId, "operador-de-pruebas", EstadoDeAcceso.SinAcceso, Ayudas.Instante);
        var resultado = solicitud.Completar(Ayudas.Instante.AddSeconds(1), "Confirmada al reintentar");

        // Act
        await using (var db = bd.CrearContexto())
        {
            var registro = new RegistroDeAccesos(db);
            registro.AgregarSolicitud(solicitud);
            registro.AgregarResultado(resultado);
            await db.GuardarCambiosAsync(Ct);
        }

        // Assert: la acción y el resultado se guardan como texto.
        await using var lectura = bd.CrearContexto();
        var accion = await lectura.Database
            .SqlQuery<string>($"SELECT accion AS \"Value\" FROM solicitudes_de_acceso WHERE id = {solicitud.Id}")
            .SingleAsync(Ct);
        var operador = await lectura.Database
            .SqlQuery<string>($"SELECT operador AS \"Value\" FROM solicitudes_de_acceso WHERE id = {solicitud.Id}")
            .SingleAsync(Ct);
        var estado = await lectura.Database
            .SqlQuery<string>($"SELECT resultado AS \"Value\" FROM resultados_de_acceso WHERE solicitud_id = {solicitud.Id}")
            .SingleAsync(Ct);
        Assert.Equal("Habilitar", accion);
        Assert.Equal("operador-de-pruebas", operador);
        Assert.Equal("Completado", estado);
    }

    [Fact]
    public async Task F003_RN20_DosResultadosParaLaMismaSolicitud_LanzaConflicto()
    {
        // Arrange: dos reconciliaciones simultáneas intentan cerrar la misma solicitud; la segunda pierde.
        var clienteId = await GuardarClienteAsync();
        var solicitud = SolicitudDeAcceso.ParaHabilitar(clienteId, "operador-de-pruebas", EstadoDeAcceso.SinAcceso, Ayudas.Instante);
        await using (var db = bd.CrearContexto())
        {
            var registro = new RegistroDeAccesos(db);
            registro.AgregarSolicitud(solicitud);
            registro.AgregarResultado(solicitud.Completar(Ayudas.Instante.AddSeconds(1)));
            await db.GuardarCambiosAsync(Ct);
        }

        await using var otro = bd.CrearContexto();
        new RegistroDeAccesos(otro).AgregarResultado(solicitud.Fallar(Ayudas.Instante.AddSeconds(2), "No se aplicó; cerrada al reintentar"));

        // Act
        var accion = () => otro.GuardarCambiosAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }

    // ---- ADR-0017: solicitudes pendientes ----

    [Fact]
    public async Task F003_ADR0017_ListarPendientes_SoloDevuelveSolicitudesSinResultado()
    {
        // Arrange: una pendiente, una cerrada y otra pendiente.
        var clienteId = await GuardarClienteAsync();
        var (primera, cerrada, tercera) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, primera, clienteId, fecha: Ayudas.Instante.AddMinutes(-3));
        await InsertarSolicitudAsync(db, cerrada, clienteId, fecha: Ayudas.Instante.AddMinutes(-2));
        await InsertarResultadoAsync(db, cerrada);
        await InsertarSolicitudAsync(db, tercera, clienteId, fecha: Ayudas.Instante.AddMinutes(-1));

        // Act
        var pendientes = await new RegistroDeAccesos(db).ListarPendientesAsync(clienteId, Ct);

        // Assert
        Assert.Equal([primera, tercera], pendientes.Select(s => s.Id));
    }

    [Fact]
    public async Task F003_ADR0017_ListarPendientes_VaDeLaMasAntiguaALaMasReciente()
    {
        // Arrange: se insertan en orden inverso al de sus fechas.
        var clienteId = await GuardarClienteAsync();
        var (reciente, antigua) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, reciente, clienteId, fecha: Ayudas.Instante.AddMinutes(-1));
        await InsertarSolicitudAsync(db, antigua, clienteId, fecha: Ayudas.Instante.AddMinutes(-10));

        // Act
        var pendientes = await new RegistroDeAccesos(db).ListarPendientesAsync(clienteId, Ct);

        // Assert
        Assert.Equal([antigua, reciente], pendientes.Select(s => s.Id));
    }

    [Fact]
    public async Task F003_ADR0017_ListarPendientes_NoIncluyeLasDeOtroCliente()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        var ajeno = await GuardarClienteAsync();
        var (propia, deOtro) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using var db = bd.CrearContexto();
        await InsertarSolicitudAsync(db, propia, clienteId);
        await InsertarSolicitudAsync(db, deOtro, ajeno);

        // Act
        var pendientes = await new RegistroDeAccesos(db).ListarPendientesAsync(clienteId, Ct);

        // Assert
        Assert.Equal([propia], pendientes.Select(s => s.Id));
    }

    [Fact]
    public async Task F003_ADR0017_ConsultaDocumentadaDeSolicitudesSinResultado_DevuelveLasPendientes()
    {
        // Arrange: ADR-0004 exige probar el SQL escrito a mano. La base es compartida: se buscan solo los ids de esta prueba.
        var clienteId = await GuardarClienteAsync();
        var (pendiente, cerrada) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using (var db = bd.CrearContexto())
        {
            await InsertarSolicitudAsync(db, pendiente, clienteId);
            await InsertarSolicitudAsync(db, cerrada, clienteId);
            await InsertarResultadoAsync(db, cerrada);
        }

        // Act
        var ids = await EjecutarLaConsultaDocumentadaAsync();

        // Assert
        Assert.Contains(pendiente, ids);
        Assert.DoesNotContain(cerrada, ids);
    }
}
