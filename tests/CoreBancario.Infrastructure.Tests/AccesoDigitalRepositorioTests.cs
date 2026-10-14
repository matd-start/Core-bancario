using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Infrastructure.Persistencia;
using CoreBancario.Infrastructure.Persistencia.Repositorios;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// ADR-0016: el vínculo entre un cliente y su usuario en el sistema de identidad, escrito por la API desde la 003b.
/// La base garantiza un vínculo por cliente y un cliente por sub, y la traducción convierte esas violaciones en
/// ConflictoDeConcurrenciaException (409).
/// </summary>
public class AccesoDigitalRepositorioTests(PostgresFixture bd)
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

    private static async Task GuardarVinculoAsync(CoreBancarioDbContext db, Guid clienteId, string sub)
    {
        new AccesoDigitalRepositorio(db).Agregar(AccesoDigital.Vincular(clienteId, sub, Ayudas.Instante));
        await db.GuardarCambiosAsync(Ct);
    }

    [Fact]
    public async Task F003_ADR0016_ObtenerPorCliente_DevuelveElVinculoGuardado()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        var sub = AyudasDeAcceso.SubUnico();
        await using (var escritura = bd.CrearContexto())
            await GuardarVinculoAsync(escritura, clienteId, sub);

        // Act
        await using var lectura = bd.CrearContexto();
        var vinculo = await new AccesoDigitalRepositorio(lectura).ObtenerPorClienteAsync(clienteId, Ct);

        // Assert
        Assert.NotNull(vinculo);
        Assert.Equal(clienteId, vinculo.ClienteId);
        Assert.Equal(sub, vinculo.Sub);
        Assert.Equal(Ayudas.Instante, vinculo.FechaDeVinculo);
    }

    [Fact]
    public async Task F003_ADR0016_ObtenerPorClienteSinVinculo_DevuelveNull()
    {
        // Arrange
        var clienteId = await GuardarClienteAsync();
        await using var db = bd.CrearContexto();

        // Act
        var vinculo = await new AccesoDigitalRepositorio(db).ObtenerPorClienteAsync(clienteId, Ct);

        // Assert
        Assert.Null(vinculo);
    }

    [Fact]
    public async Task F003_ADR0016_DosClientesConElMismoSub_LanzaConflicto()
    {
        // Arrange
        var primero = await GuardarClienteAsync();
        var segundo = await GuardarClienteAsync();
        var sub = AyudasDeAcceso.SubUnico();
        await using (var escritura = bd.CrearContexto())
            await GuardarVinculoAsync(escritura, primero, sub);

        await using var db = bd.CrearContexto();

        // Act
        var accion = () => GuardarVinculoAsync(db, segundo, sub);

        // Assert: el índice ux_accesos_digitales_sub se traduce a 409.
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }

    [Fact]
    public async Task F003_ADR0016_DosVinculosParaElMismoCliente_LanzaConflicto()
    {
        // Arrange: dos operadores habilitan a la vez al mismo cliente y los dos crean el vínculo.
        var clienteId = await GuardarClienteAsync();
        await using (var escritura = bd.CrearContexto())
            await GuardarVinculoAsync(escritura, clienteId, AyudasDeAcceso.SubUnico());

        await using var db = bd.CrearContexto();

        // Act
        var accion = () => GuardarVinculoAsync(db, clienteId, AyudasDeAcceso.SubUnico());

        // Assert: la clave primaria pk_accesos_digitales se traduce a 409.
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }
}
