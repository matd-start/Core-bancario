using CoreBancario.Application.Clientes;
using CoreBancario.Application.Tests.Fakes;

namespace CoreBancario.Application.Tests.Clientes;

public class RegistrarClienteHandlerTests
{
    private readonly ClienteRepositorioEnMemoria _clientes = new();
    private readonly UnidadDeTrabajoEspia _unidad = new();

    private RegistrarClienteHandler CrearHandler(UnidadDeTrabajoEspia? unidad = null) =>
        new(_clientes, unidad ?? _unidad, Datos.Reloj);

    private static RegistrarClienteComando ComandoValido() => new(
        "CC", "1.234.567", "María José", "Núñez", "maria@example.com", "+57 300 123 4567");

    [Fact]
    public async Task CA01_RegistrarClienteValido_GuardaUnaVezYDevuelveDatosNormalizadosConLaFechaDelReloj()
    {
        // Arrange
        var handler = CrearHandler();

        // Act
        var resultado = await handler.EjecutarAsync(ComandoValido(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resultado.EsExito);
        var dto = resultado.Valor;
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("CC", dto.TipoDocumento);
        Assert.Equal("1234567", dto.NumeroDocumento);
        Assert.Equal("+573001234567", dto.Telefono);
        Assert.Equal(Datos.Ahora, dto.FechaRegistro);
        Assert.Single(_clientes.Clientes);
        Assert.Equal(1, _unidad.Llamadas);
    }

    [Fact]
    public async Task CA04_DocumentoCorreoYTelefonoInvalidos_DevuelveLosTresErroresSinGuardar()
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { NumeroDocumento = "12AB", Correo = "no-es-correo", Telefono = "3001234567" };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["correo", "numeroDocumento", "telefono"], resultado.Errores.Keys.Order());
        Assert.Empty(_clientes.Clientes);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CL04_TodosLosCamposAusentes_DevuelveUnErrorPorCampo()
    {
        // Arrange
        var handler = CrearHandler();
        var comando = new RegistrarClienteComando(null, null, null, null, null, null);

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(
            ["apellidos", "correo", "nombres", "numeroDocumento", "telefono", "tipoDocumento"],
            resultado.Errores.Keys.Order());
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(" .-. ")]
    public async Task CL04_NumeroQueQuedaVacioAlNormalizar_DevuelveErrorDeObligatorioComoLaBusqueda(string numero)
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { NumeroDocumento = numero };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["El número de documento es obligatorio."], resultado.Errores["numeroDocumento"]);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("1")]
    [InlineData("cc")]
    [InlineData("CC,PA")]
    public async Task CL05_TipoDocumentoDesconocido_DevuelveErrorDeTipoYNoEvaluaElNumero(string tipo)
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { TipoDocumento = tipo };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["tipoDocumento"], resultado.Errores.Keys);
    }

    [Fact]
    public async Task CL05_NumeroConFormatoInvalidoParaSuTipo_DevuelveErrorDeNumero()
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { TipoDocumento = "CC", NumeroDocumento = "ABC1234" };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["numeroDocumento"], resultado.Errores.Keys);
    }

    [Fact]
    public async Task CL06_NombresYApellidosConSoloEspacios_DevuelveErrorEnAmbosCampos()
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { Nombres = "   ", Apellidos = "" };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["apellidos", "nombres"], resultado.Errores.Keys.Order());
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task CL07_TelefonoSinCodigoDePais_DevuelveErrorDeTelefono()
    {
        // Arrange
        var handler = CrearHandler();
        var comando = ComandoValido() with { Telefono = "3001234567" };

        // Act
        var resultado = await handler.EjecutarAsync(comando, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(resultado.EsExito);
        Assert.Equal(["telefono"], resultado.Errores.Keys);
    }

    [Fact]
    public async Task CL01_LaBaseRechazaElDocumentoDuplicado_PropagaDocumentoDuplicadoException()
    {
        // Arrange
        var unidad = new UnidadDeTrabajoEspia { ExcepcionAlGuardar = new DocumentoDuplicadoException() };
        var handler = CrearHandler(unidad);

        // Act
        var accion = () => handler.EjecutarAsync(ComandoValido(), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<DocumentoDuplicadoException>(accion);
    }

    [Fact]
    public async Task CL01_RegistrarSinConsultaPrevia_NoBuscaElDocumentoAntesDeGuardar()
    {
        // Arrange: ya existe un cliente con el mismo documento en el repositorio en memoria.
        _clientes.Agregar(Datos.Cliente("1234567"));
        var handler = CrearHandler();

        // Act
        var resultado = await handler.EjecutarAsync(ComandoValido(), TestContext.Current.CancellationToken);

        // Assert: la unicidad la decide el indice de la base (plan, decision 3), no el handler.
        Assert.True(resultado.EsExito);
        Assert.Equal(1, _unidad.Llamadas);
    }
}
