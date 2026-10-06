using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Tests.Fakes;

namespace CoreBancario.Application.Tests.Clientes;

public class ConsultasDeClientesHandlerTests
{
    private readonly ClienteRepositorioEnMemoria _clientes = new();
    private readonly CuentaRepositorioEnMemoria _cuentas = new();

    private ObtenerClienteHandler CrearObtener() => new(_clientes, _cuentas);

    private BuscarClientePorDocumentoHandler CrearBuscar() => new(_clientes, _cuentas);

    // ---- ObtenerClienteHandler ----

    [Fact]
    public async Task CL18_ObtenerClienteInexistente_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var handler = CrearObtener();

        // Act
        var accion = () => handler.EjecutarAsync(new ObtenerClienteConsulta(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Fact]
    public async Task CL19_ClienteSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        var cliente = Datos.Cliente();
        _clientes.Agregar(cliente);
        var handler = CrearObtener();

        // Act
        var ficha = await handler.EjecutarAsync(new ObtenerClienteConsulta(cliente.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(cliente.Id, ficha.Cliente.Id);
        Assert.Empty(ficha.Cuentas);
    }

    [Fact]
    public async Task CA15_ClienteConCuentas_LasOrdenaPorFechaDeAperturaYLuegoPorId()
    {
        // Arrange
        var cliente = Datos.Cliente();
        _clientes.Agregar(cliente);
        var reciente = Datos.Cuenta(cliente.Id, Datos.NumeroA, apertura: Datos.Ahora.AddDays(1));
        var antigua = Datos.Cuenta(cliente.Id, Datos.NumeroB, apertura: Datos.Ahora);
        _cuentas.Agregar(reciente);
        _cuentas.Agregar(antigua);
        var handler = CrearObtener();

        // Act
        var ficha = await handler.EjecutarAsync(new ObtenerClienteConsulta(cliente.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([antigua.Id, reciente.Id], ficha.Cuentas.Select(c => c.Id));
    }

    [Fact]
    public async Task CA15_CuentasConLaMismaFechaDeApertura_SeOrdenanPorId()
    {
        // Arrange
        var cliente = Datos.Cliente();
        _clientes.Agregar(cliente);
        var primera = Datos.Cuenta(cliente.Id, Datos.NumeroA);
        var segunda = Datos.Cuenta(cliente.Id, Datos.NumeroB);
        _cuentas.Agregar(primera);
        _cuentas.Agregar(segunda);
        var esperado = new[] { primera.Id, segunda.Id }.Order().ToList();
        var handler = CrearObtener();

        // Act
        var ficha = await handler.EjecutarAsync(new ObtenerClienteConsulta(cliente.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(esperado, ficha.Cuentas.Select(c => c.Id));
    }

    [Fact]
    public async Task CA15_FichaDeUnCliente_NoIncluyeCuentasDeOtroCliente()
    {
        // Arrange
        var cliente = Datos.Cliente("1234567");
        var otro = Datos.Cliente("7654321");
        _clientes.Agregar(cliente);
        _clientes.Agregar(otro);
        _cuentas.Agregar(Datos.Cuenta(otro.Id));
        var handler = CrearObtener();

        // Act
        var ficha = await handler.EjecutarAsync(new ObtenerClienteConsulta(cliente.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(ficha.Cuentas);
    }

    // ---- BuscarClientePorDocumentoHandler ----

    [Fact]
    public async Task CL17_BuscarConElNumeroConPuntos_EncuentraAlCliente()
    {
        // Arrange
        var cliente = Datos.Cliente("1234567");
        _clientes.Agregar(cliente);
        _cuentas.Agregar(Datos.Cuenta(cliente.Id));
        var handler = CrearBuscar();

        // Act
        var resultado = await handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta("CC", "1.234.567"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resultado.EsExito);
        Assert.Equal(cliente.Id, resultado.Valor.Cliente.Id);
        Assert.Single(resultado.Valor.Cuentas);
    }

    [Fact]
    public async Task CL02_MismoNumeroConOtroTipo_NoEncuentraAlCliente()
    {
        // Arrange
        _clientes.Agregar(Datos.Cliente("1234567"));
        var handler = CrearBuscar();

        // Act
        var accion = () => handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta("PA", "1234567"), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Fact]
    public async Task CA16_BuscarDocumentoNoRegistrado_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var handler = CrearBuscar();

        // Act
        var accion = () => handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta("CC", "9999999"), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Fact]
    public async Task CL18_BuscarNumeroConFormatoInvalidoParaSuTipo_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var handler = CrearBuscar();

        // Act
        var accion = () => handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta("CC", "ABC"), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("cc")]
    public async Task CL18_BuscarConTipoInvalido_DevuelveInvalidoConLaClaveTipoDocumento(string? tipo)
    {
        // Arrange
        var handler = CrearBuscar();

        // Act
        var resultado = await handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta(tipo, "1234567"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Contains("tipoDocumento", resultado.Errores.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".-.")]
    public async Task CL18_BuscarSinNumeroOVacioAlNormalizar_DevuelveInvalidoConLaClaveNumeroDocumento(string? numero)
    {
        // Arrange
        var handler = CrearBuscar();

        // Act
        var resultado = await handler.EjecutarAsync(
            new BuscarClientePorDocumentoConsulta("CC", numero), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Contains("numeroDocumento", resultado.Errores.Keys);
    }
}
