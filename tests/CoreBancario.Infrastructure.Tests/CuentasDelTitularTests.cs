using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using CoreBancario.Infrastructure.Persistencia.Repositorios;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// ICuentasDelTitular (RN-12, ADR-0016): el filtro de dueño va dentro de la consulta, asi que una cuenta ajena y
/// una inexistente son indistinguibles. Contra PostgreSQL real, porque lo que se prueba es el SQL generado.
/// </summary>
public class CuentasDelTitularTests(PostgresFixture bd)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Guarda un cliente con las cuentas indicadas y lo vincula a un sub nuevo.</summary>
    private async Task<(Cliente Cliente, string Sub)> TitularConAsync(params Func<Guid, Cuenta>[] cuentas)
    {
        var cliente = Ayudas.NuevoCliente();
        var sub = AyudasDeAcceso.SubUnico();

        await using var db = bd.CrearContexto();
        db.Clientes.Add(cliente);
        foreach (var crear in cuentas)
            db.Cuentas.Add(crear(cliente.Id));
        await db.GuardarCambiosAsync(Ct);

        await AyudasDeAcceso.VincularAsync(db, cliente.Id, sub);
        return (cliente, sub);
    }

    private async Task<IReadOnlyList<Cuenta>> ListarAsync(string sub)
    {
        await using var db = bd.CrearContexto();
        return await new CuentasDelTitular(db).ListarAsync(sub, Ct);
    }

    private async Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub)
    {
        await using var db = bd.CrearContexto();
        return await new CuentasDelTitular(db).ObtenerAsync(cuentaId, sub, Ct);
    }

    // ---- RN-12 / CA-14: solo las cuentas del cliente vinculado ----

    [Fact]
    public async Task F003_RN12_CuentasDelTitular_SoloDevuelveLasDelClienteVinculado()
    {
        // Arrange
        var (_, subPropio) = await TitularConAsync(id => Ayudas.NuevaCuenta(id), id => Ayudas.NuevaCuenta(id, Moneda.USD));
        var (_, subAjeno) = await TitularConAsync(id => Ayudas.NuevaCuenta(id));

        // Act
        var propias = await ListarAsync(subPropio);
        var ajenas = await ListarAsync(subAjeno);

        // Assert
        Assert.Equal(2, propias.Count);
        Assert.Single(ajenas);
        Assert.Empty(propias.Select(c => c.Id).Intersect(ajenas.Select(c => c.Id)));
    }

    [Fact]
    public async Task F003_CL18_ListarAsync_IncluyeLasCuentasCerradasConSuEstado()
    {
        // Arrange
        var (_, sub) = await TitularConAsync(
            id => Ayudas.NuevaCuenta(id),
            id =>
            {
                var cuenta = Ayudas.NuevaCuenta(id, Moneda.USD);
                cuenta.Cerrar(Ayudas.Instante.AddDays(1));
                return cuenta;
            });

        // Act
        var cuentas = await ListarAsync(sub);

        // Assert
        Assert.Equal([EstadoCuenta.Activa, EstadoCuenta.Cerrada], cuentas.Select(c => c.Estado).Order());
        Assert.Contains(cuentas, c => c.Estado == EstadoCuenta.Cerrada && c.FechaCierre == Ayudas.Instante.AddDays(1));
    }

    [Fact]
    public async Task F003_CA14_ListarAsync_OrdenaPorFechaDeAperturaYDespuesPorId()
    {
        // Arrange: dos cuentas con la misma fecha (desempata el Id) y una anterior, agregadas en desorden.
        var (_, sub) = await TitularConAsync(
            id => Ayudas.NuevaCuenta(id, apertura: Ayudas.Instante.AddHours(2)),
            id => Ayudas.NuevaCuenta(id, apertura: Ayudas.Instante),
            id => Ayudas.NuevaCuenta(id, apertura: Ayudas.Instante.AddHours(2)));

        // Act
        var cuentas = await ListarAsync(sub);

        // Assert: PostgreSQL ordena un uuid byte a byte, de izquierda a derecha.
        var esperado = cuentas
            .OrderBy(c => c.FechaApertura)
            .ThenBy(c => c.Id.ToByteArray(bigEndian: true), Comparer<byte[]>.Create((a, b) => a.AsSpan().SequenceCompareTo(b)))
            .Select(c => c.Id);
        Assert.Equal(esperado, cuentas.Select(c => c.Id));
        Assert.Equal(Ayudas.Instante, cuentas[0].FechaApertura);
    }

    [Fact]
    public async Task F003_CA17_ListarAsync_ClienteVinculadoSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        var (_, sub) = await TitularConAsync();

        // Act
        var cuentas = await ListarAsync(sub);

        // Assert
        Assert.Empty(cuentas);
    }

    [Fact]
    public async Task F003_CA17_ListarAsync_SubSinVinculo_DevuelveListaVacia()
    {
        // Arrange: hay cuentas en la base, pero ese sub no esta vinculado a ningun cliente.
        await TitularConAsync(id => Ayudas.NuevaCuenta(id));

        // Act
        var cuentas = await ListarAsync(AyudasDeAcceso.SubUnico());

        // Assert
        Assert.Empty(cuentas);
    }

    // ---- CA-15 / CL-17: ajena = inexistente ----

    [Fact]
    public async Task F003_CA15_CuentaAjenaEInexistente_DevuelvenNull()
    {
        // Arrange
        var (_, subPropio) = await TitularConAsync(id => Ayudas.NuevaCuenta(id));
        var (clienteAjeno, _) = await TitularConAsync(id => Ayudas.NuevaCuenta(id));
        var cuentaAjena = (await ListarPorClienteAsync(clienteAjeno.Id)).Single();

        // Act
        var porAjena = await ObtenerAsync(cuentaAjena.Id, subPropio);
        var porInexistente = await ObtenerAsync(Guid.NewGuid(), subPropio);

        // Assert
        Assert.Null(porAjena);
        Assert.Null(porInexistente);
    }

    [Fact]
    public async Task F003_CA16_ObtenerAsync_CuentaPropia_DevuelveLaCuentaConSuSaldoYFechas()
    {
        // Arrange
        var (cliente, sub) = await TitularConAsync(id =>
        {
            var cuenta = Ayudas.NuevaCuenta(id, Moneda.USD);
            cuenta.Acreditar(Dinero.Crear(10.5m, Moneda.USD), Ayudas.Instante);
            return cuenta;
        });
        var guardada = (await ListarPorClienteAsync(cliente.Id)).Single();

        // Act
        var cuenta = await ObtenerAsync(guardada.Id, sub);

        // Assert
        Assert.NotNull(cuenta);
        Assert.Equal(guardada.Numero, cuenta.Numero);
        Assert.Equal(cliente.Id, cuenta.ClienteId);
        Assert.Equal(Dinero.Crear(10.5m, Moneda.USD), cuenta.Saldo);
        Assert.Equal(Ayudas.Instante, cuenta.FechaApertura);
    }

    [Fact]
    public async Task F003_ADR0004_CuentasDelTitular_NoRastreaLasEntidadesLeidas()
    {
        // Arrange
        var (_, sub) = await TitularConAsync(id => Ayudas.NuevaCuenta(id));
        await using var db = bd.CrearContexto();
        var repositorio = new CuentasDelTitular(db);

        // Act
        var cuentas = await repositorio.ListarAsync(sub, Ct);

        // Assert
        Assert.NotEmpty(cuentas);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private async Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId)
    {
        await using var db = bd.CrearContexto();
        return await new CuentaRepositorio(db).ListarPorClienteAsync(clienteId, Ct);
    }
}
