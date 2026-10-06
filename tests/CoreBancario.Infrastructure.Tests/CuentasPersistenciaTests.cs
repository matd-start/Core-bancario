using CoreBancario.Application.Comun;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using CoreBancario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Tests;

public class CuentasPersistenciaTests(PostgresFixture bd)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- Forma canonica del saldo al recargar (riesgo 3 del plan) ----

    [Fact]
    public async Task CA06_SaldoCopRecargado_ConservaFormaCanonica()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd, Moneda.COP, Dinero.Crear(50_000m, Moneda.COP));

        // Act
        await using var db = bd.CrearContexto();
        var leida = await Ayudas.LeerCuentaAsync(db, guardada.Id);

        // Assert: numeric(19,2) devuelve 50000.00, pero Dinero lo recupera como 50000 (escala 0).
        Assert.Equal(0, leida.Saldo.Monto.Scale);
        Assert.Equal(Dinero.Crear(50_000m, Moneda.COP), leida.Saldo);
    }

    [Fact]
    public async Task CA06_CuentaGuardada_SeRecuperaConNumeroEstadoYFechas()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd);

        // Act
        await using var db = bd.CrearContexto();
        var leida = await Ayudas.LeerCuentaAsync(db, guardada.Id);

        // Assert
        Assert.Equal(guardada.Numero, leida.Numero);
        Assert.Equal(guardada.ClienteId, leida.ClienteId);
        Assert.Equal(Moneda.COP, leida.Moneda);
        Assert.Equal(EstadoCuenta.Activa, leida.Estado);
        Assert.Equal(guardada.FechaApertura, leida.FechaApertura);
        Assert.Null(leida.FechaCierre);
    }

    [Fact]
    public async Task CA07_SaldoUsdRecargado_ConservaDosDecimales()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd, Moneda.USD, Dinero.Crear(10.5m, Moneda.USD));

        // Act
        await using var db = bd.CrearContexto();
        var leida = await Ayudas.LeerCuentaAsync(db, guardada.Id);

        // Assert
        Assert.Equal(2, leida.Saldo.Monto.Scale);
        Assert.Equal(Dinero.Crear(10.50m, Moneda.USD), leida.Saldo);
        Assert.Equal(Moneda.USD, leida.Moneda);
    }

    [Fact]
    public async Task CA07_SaldoCeroEnUsdRecargado_ConservaDosDecimales()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd, Moneda.USD);

        // Act
        await using var db = bd.CrearContexto();
        var leida = await Ayudas.LeerCuentaAsync(db, guardada.Id);

        // Assert
        Assert.Equal(2, leida.Saldo.Monto.Scale);
        Assert.Equal(Dinero.Crear(0m, Moneda.USD), leida.Saldo);
    }

    // ---- RN-18: el indice unico es la garantia final ----

    [Fact]
    public async Task RN18_NumeroRepetido_LoRechazaElIndiceUnico()
    {
        // Arrange
        var existente = await Ayudas.GuardarCuentaAsync(bd);
        var otroCliente = Ayudas.NuevoCliente();
        await using var db = bd.CrearContexto();
        db.Clientes.Add(otroCliente);
        db.Cuentas.Add(Ayudas.NuevaCuenta(otroCliente.Id, numero: existente.Numero.Valor));

        // Act
        var accion = () => db.GuardarCambiosAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }

    [Fact]
    public async Task RN18_ExisteNumero_DistingueEntreNumeroGuardadoYLibre()
    {
        // Arrange
        var existente = await Ayudas.GuardarCuentaAsync(bd);
        await using var db = bd.CrearContexto();
        var repositorio = new CuentaRepositorio(db);
        var libre = NumeroDeCuenta.Crear(Ayudas.NumeroDeCuentaUnico());

        // Act
        var existe = await repositorio.ExisteNumeroAsync(existente.Numero, Ct);
        var libreExiste = await repositorio.ExisteNumeroAsync(libre, Ct);

        // Assert
        Assert.True(existe);
        Assert.False(libreExiste);
    }

    // ---- CL-09: la FK es la segunda defensa ----

    [Fact]
    public async Task CL09_CuentaDeClienteInexistente_LaClaveForaneaLaRechaza()
    {
        // Arrange
        await using var db = bd.CrearContexto();
        db.Cuentas.Add(Ayudas.NuevaCuenta(Guid.NewGuid()));

        // Act
        var accion = () => db.GuardarCambiosAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(accion);
    }

    // ---- Repositorio ----

    [Fact]
    public async Task CL19_ListarPorClienteSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        var cliente = Ayudas.NuevoCliente();
        await using (var escritura = bd.CrearContexto())
        {
            escritura.Clientes.Add(cliente);
            await escritura.GuardarCambiosAsync(Ct);
        }

        await using var db = bd.CrearContexto();
        var repositorio = new CuentaRepositorio(db);

        // Act
        var cuentas = await repositorio.ListarPorClienteAsync(cliente.Id, Ct);

        // Assert
        Assert.Empty(cuentas);
    }

    [Fact]
    public async Task CA15_ListarPorCliente_OrdenaPorFechaDeAperturaYDevuelveSoloLasDelCliente()
    {
        // Arrange
        var cliente = Ayudas.NuevoCliente();
        var reciente = Ayudas.NuevaCuenta(cliente.Id, apertura: Ayudas.Instante.AddDays(1));
        var antigua = Ayudas.NuevaCuenta(cliente.Id, apertura: Ayudas.Instante);
        var deOtro = Ayudas.NuevoCliente();
        await using (var escritura = bd.CrearContexto())
        {
            escritura.Clientes.AddRange(cliente, deOtro);
            escritura.Cuentas.AddRange(reciente, antigua, Ayudas.NuevaCuenta(deOtro.Id));
            await escritura.GuardarCambiosAsync(Ct);
        }

        await using var db = bd.CrearContexto();
        var repositorio = new CuentaRepositorio(db);

        // Act
        var cuentas = await repositorio.ListarPorClienteAsync(cliente.Id, Ct);

        // Assert
        Assert.Equal([antigua.Id, reciente.Id], cuentas.Select(c => c.Id));
    }

    // ---- Concurrencia por fila con xmin (ADR-0012) ----

    [Fact]
    public async Task CA14_BloquearYCerrarIntercalados_ElSegundoRecibeConflicto()
    {
        // Arrange: dos contextos leen la misma version de la cuenta.
        var guardada = await Ayudas.GuardarCuentaAsync(bd);
        await using var primero = bd.CrearContexto();
        await using var segundo = bd.CrearContexto();
        var enPrimero = await Ayudas.LeerCuentaAsync(primero, guardada.Id);
        var enSegundo = await Ayudas.LeerCuentaAsync(segundo, guardada.Id);
        enPrimero.Bloquear();
        await primero.GuardarCambiosAsync(Ct);
        enSegundo.Cerrar(Ayudas.Instante);

        // Act
        var accion = () => segundo.GuardarCambiosAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }

    [Fact]
    public async Task CA14_TrasElConflicto_LaBaseConservaLaOperacionAplicada()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd);
        await using (var primero = bd.CrearContexto())
        await using (var segundo = bd.CrearContexto())
        {
            var enPrimero = await Ayudas.LeerCuentaAsync(primero, guardada.Id);
            var enSegundo = await Ayudas.LeerCuentaAsync(segundo, guardada.Id);
            enPrimero.Bloquear();
            await primero.GuardarCambiosAsync(Ct);
            enSegundo.Cerrar(Ayudas.Instante);
            await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() => segundo.GuardarCambiosAsync(Ct));
        }

        // Act
        await using var lectura = bd.CrearContexto();
        var recargada = await Ayudas.LeerCuentaAsync(lectura, guardada.Id);

        // Assert
        Assert.Equal(EstadoCuenta.Bloqueada, recargada.Estado);
        Assert.Null(recargada.FechaCierre);
    }

    [Fact]
    public async Task CL15_ReintentoTrasConflicto_EvaluaLaReglaSobreElEstadoNuevo()
    {
        // Arrange: tras el conflicto, la cuenta esta Bloqueada en la base.
        var guardada = await Ayudas.GuardarCuentaAsync(bd);
        await using (var primero = bd.CrearContexto())
        {
            var enPrimero = await Ayudas.LeerCuentaAsync(primero, guardada.Id);
            enPrimero.Bloquear();
            await primero.GuardarCambiosAsync(Ct);
        }

        await using var reintento = bd.CrearContexto();
        var recargada = await Ayudas.LeerCuentaAsync(reintento, guardada.Id);

        // Act: cerrar una cuenta Bloqueada ya no es posible (RN-14), no un conflicto.
        var accion = () => recargada.Cerrar(Ayudas.Instante);

        // Assert
        Assert.Throws<TransicionNoPermitidaException>(accion);
    }

    [Fact]
    public async Task CL15_CerrarConVersionViejaTrasCambioDeSaldo_LanzaConflicto()
    {
        // Arrange: write skew del S4. El segundo contexto ve saldo 0 y cerraria una cuenta que ya tiene saldo.
        var guardada = await Ayudas.GuardarCuentaAsync(bd);
        await using var deposito = bd.CrearContexto();
        await using var cierre = bd.CrearContexto();
        var enDeposito = await Ayudas.LeerCuentaAsync(deposito, guardada.Id);
        var enCierre = await Ayudas.LeerCuentaAsync(cierre, guardada.Id);
        enDeposito.Acreditar(Dinero.Crear(1_000m, Moneda.COP), Ayudas.Instante);
        await deposito.GuardarCambiosAsync(Ct);
        enCierre.Cerrar(Ayudas.Instante);

        // Act
        var accion = () => cierre.GuardarCambiosAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }

    [Fact]
    public async Task CA12_CierreGuardado_SeRecuperaConEstadoYFechaDeCierre()
    {
        // Arrange
        var guardada = await Ayudas.GuardarCuentaAsync(bd);
        await using (var escritura = bd.CrearContexto())
        {
            var cuenta = await Ayudas.LeerCuentaAsync(escritura, guardada.Id);
            cuenta.Cerrar(Ayudas.Instante.AddDays(1));
            await escritura.GuardarCambiosAsync(Ct);
        }

        // Act
        await using var lectura = bd.CrearContexto();
        var leida = await Ayudas.LeerCuentaAsync(lectura, guardada.Id);

        // Assert
        Assert.Equal(EstadoCuenta.Cerrada, leida.Estado);
        Assert.Equal(Ayudas.Instante.AddDays(1), leida.FechaCierre);
    }
}
