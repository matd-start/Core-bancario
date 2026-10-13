using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBancario.Application.Tests.Cuentas;

public class MisCuentasHandlerTests
{
    private const string SubDelTitular = "sub-del-titular";
    private const string SubDeOtroCliente = "sub-de-otro-cliente";

    private readonly CuentasDelTitularEnMemoria _cuentas = new();
    private readonly Guid _clienteId = Guid.NewGuid();
    private readonly Guid _otroClienteId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MisCuentasHandlerTests()
    {
        _cuentas.Vincular(SubDelTitular, _clienteId);
        _cuentas.Vincular(SubDeOtroCliente, _otroClienteId);
    }

    // ---- RF-05 / CA-14: mis cuentas ----

    [Fact]
    public async Task F003_CA14_ListarMisCuentas_DevuelveActivaYCerradaSinCuentasAjenas()
    {
        // Arrange
        var activa = Datos.Cuenta(_clienteId, Datos.NumeroA, Moneda.COP);
        var cerrada = Datos.Cuenta(_clienteId, Datos.NumeroB, Moneda.USD);
        cerrada.Cerrar(Datos.Ahora.AddDays(1));
        var ajena = Datos.Cuenta(_otroClienteId, NumeroDeCuentaAjena());
        _cuentas.Cuentas.AddRange([activa, cerrada, ajena]);
        var handler = new ListarMisCuentasHandler(_cuentas);

        // Act
        var resultado = await handler.EjecutarAsync(new ListarMisCuentasConsulta(SubDelTitular), Ct);

        // Assert
        Assert.Equal([activa.Id, cerrada.Id], resultado.Select(c => c.Id));
        Assert.Equal(["1234567897", "9876543217"], resultado.Select(c => c.Numero));
        Assert.Equal(["COP", "USD"], resultado.Select(c => c.Moneda));
        Assert.Equal(["Activa", "Cerrada"], resultado.Select(c => c.Estado));
        Assert.Equal([new DineroDto("0", "COP"), new DineroDto("0.00", "USD")], resultado.Select(c => c.Saldo));
        Assert.DoesNotContain(resultado, c => c.ClienteId == _otroClienteId);
    }

    [Fact]
    public async Task F003_CL18_ClienteConUnaCuentaCerrada_ListaLaCuentaConSuEstadoYFechaDeCierre()
    {
        // Arrange
        var cerrada = Datos.Cuenta(_clienteId);
        var fechaDeCierre = Datos.Ahora.AddDays(3);
        cerrada.Cerrar(fechaDeCierre);
        _cuentas.Cuentas.Add(cerrada);
        var handler = new ListarMisCuentasHandler(_cuentas);

        // Act
        var resultado = await handler.EjecutarAsync(new ListarMisCuentasConsulta(SubDelTitular), Ct);

        // Assert
        var dto = Assert.Single(resultado);
        Assert.Equal("Cerrada", dto.Estado);
        Assert.Equal(fechaDeCierre, dto.FechaCierre);
    }

    [Fact]
    public async Task F003_CA17_ClienteSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        _cuentas.Cuentas.Add(Datos.Cuenta(_otroClienteId, NumeroDeCuentaAjena()));
        var handler = new ListarMisCuentasHandler(_cuentas);

        // Act
        var resultado = await handler.EjecutarAsync(new ListarMisCuentasConsulta(SubDelTitular), Ct);

        // Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task F003_CA17_SubSinVinculoConElBanco_DevuelveListaVaciaYNoLanza()
    {
        // Arrange
        _cuentas.Cuentas.Add(Datos.Cuenta(_clienteId));
        var handler = new ListarMisCuentasHandler(_cuentas);

        // Act
        var resultado = await handler.EjecutarAsync(new ListarMisCuentasConsulta("sub-que-nadie-vinculo"), Ct);

        // Assert
        Assert.Empty(resultado);
    }

    // ---- CA-16: detalle de una cuenta propia ----

    [Fact]
    public async Task F003_CA16_MiCuenta_DevuelveNumeroMonedaEstadoSaldoYFechas()
    {
        // Arrange
        var cuenta = Datos.Cuenta(_clienteId, Datos.NumeroA, Moneda.USD);
        _cuentas.Cuentas.Add(cuenta);
        var handler = new ObtenerMiCuentaHandler(_cuentas);

        // Act
        var dto = await handler.EjecutarAsync(new ObtenerMiCuentaConsulta(cuenta.Id, SubDelTitular), Ct);

        // Assert
        Assert.Equal(cuenta.Id, dto.Id);
        Assert.Equal("1234567897", dto.Numero);
        Assert.Equal(_clienteId, dto.ClienteId);
        Assert.Equal("USD", dto.Moneda);
        Assert.Equal("Activa", dto.Estado);
        Assert.Equal(new DineroDto("0.00", "USD"), dto.Saldo);
        Assert.Equal(Datos.Ahora, dto.FechaApertura);
        Assert.Null(dto.FechaCierre);
    }

    // ---- CA-15 / CL-17: cuenta ajena = cuenta inexistente ----

    [Fact]
    public async Task F003_CA15_CuentaAjena_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var ajena = Datos.Cuenta(_otroClienteId, NumeroDeCuentaAjena());
        _cuentas.Cuentas.Add(ajena);
        var handler = new ObtenerMiCuentaHandler(_cuentas);

        // Act
        var accion = () => handler.EjecutarAsync(new ObtenerMiCuentaConsulta(ajena.Id, SubDelTitular), Ct);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Fact]
    public async Task F003_CA15_CuentaInexistente_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var handler = new ObtenerMiCuentaHandler(_cuentas);

        // Act
        var accion = () => handler.EjecutarAsync(new ObtenerMiCuentaConsulta(Guid.NewGuid(), SubDelTitular), Ct);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Fact]
    public async Task F003_CA15_CuentaAjenaEInexistente_LanzanElMismoMensaje()
    {
        // Arrange
        var ajena = Datos.Cuenta(_otroClienteId, NumeroDeCuentaAjena());
        _cuentas.Cuentas.Add(ajena);
        var handler = new ObtenerMiCuentaHandler(_cuentas);

        // Act
        var porAjena = await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => handler.EjecutarAsync(new ObtenerMiCuentaConsulta(ajena.Id, SubDelTitular), Ct));
        var porInexistente = await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => handler.EjecutarAsync(new ObtenerMiCuentaConsulta(Guid.NewGuid(), SubDelTitular), Ct));

        // Assert
        Assert.Equal(porInexistente.Message, porAjena.Message);
    }

    [Fact]
    public async Task F003_CA15_CuentaInexistente_LanzaElMismoMensajeQueObtenerCuentaHandler()
    {
        // Arrange: el mensaje del operador (002) y el del cliente (003) no deben poder distinguirse.
        var delOperador = new ObtenerCuentaHandler(new CuentaRepositorioEnMemoria());
        var delCliente = new ObtenerMiCuentaHandler(_cuentas);
        var cuentaId = Guid.NewGuid();

        // Act
        var mensajeDelOperador = (await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => delOperador.EjecutarAsync(new ObtenerCuentaConsulta(cuentaId), Ct))).Message;
        var mensajeDelCliente = (await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => delCliente.EjecutarAsync(new ObtenerMiCuentaConsulta(cuentaId, SubDelTitular), Ct))).Message;

        // Assert
        Assert.Equal(mensajeDelOperador, mensajeDelCliente);
        Assert.Equal("No existe una cuenta con ese identificador.", mensajeDelCliente);
    }

    // ---- Registro en la inyeccion de dependencias ----

    [Theory]
    [InlineData(typeof(ListarMisCuentasHandler))]
    [InlineData(typeof(ObtenerMiCuentaHandler))]
    public void F003_CA14_AddAplicacion_RegistraLosHandlersDeMisCuentasComoScoped(Type handler)
    {
        // Arrange
        var servicios = new ServiceCollection();

        // Act
        servicios.AddAplicacion();

        // Assert
        var registro = Assert.Single(servicios, d => d.ServiceType == handler);
        Assert.Equal(ServiceLifetime.Scoped, registro.Lifetime);
    }

    private static NumeroDeCuenta NumeroDeCuentaAjena() =>
        NumeroDeCuenta.Crear("5555555555");
}
