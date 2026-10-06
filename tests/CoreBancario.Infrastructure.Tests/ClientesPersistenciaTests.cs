using CoreBancario.Application.Clientes;
using CoreBancario.Domain.Clientes;
using CoreBancario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Tests;

public class ClientesPersistenciaTests(PostgresFixture bd)
{
    [Fact]
    public async Task CA01_ClienteGuardado_SeRecuperaConTodosSusDatos()
    {
        // Arrange
        var cliente = Ayudas.NuevoCliente();
        await using (var escritura = bd.CrearContexto())
        {
            escritura.Clientes.Add(cliente);
            await escritura.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var lectura = bd.CrearContexto();
        var leido = await lectura.Clientes.SingleAsync(c => c.Id == cliente.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(cliente.Documento, leido.Documento);
        Assert.Equal(cliente.Nombres, leido.Nombres);
        Assert.Equal(cliente.Apellidos, leido.Apellidos);
        Assert.Equal(cliente.Correo, leido.Correo);
        Assert.Equal(cliente.Telefono, leido.Telefono);
        Assert.Equal(cliente.FechaRegistro, leido.FechaRegistro);
    }

    [Fact]
    public async Task CA05_DosContextosRegistranElMismoDocumento_ElSegundoLanzaDocumentoDuplicado()
    {
        // Arrange
        var numero = Ayudas.NumeroDeDocumentoUnico();
        await using var primero = bd.CrearContexto();
        await using var segundo = bd.CrearContexto();
        primero.Clientes.Add(Ayudas.NuevoCliente(numeroDocumento: numero));
        segundo.Clientes.Add(Ayudas.NuevoCliente(numeroDocumento: numero));
        await primero.GuardarCambiosAsync(TestContext.Current.CancellationToken);

        // Act
        var accion = () => segundo.GuardarCambiosAsync(TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<DocumentoDuplicadoException>(accion);
    }

    [Fact]
    public async Task CA05_DocumentoDuplicado_DejaUnSoloClienteConEseDocumento()
    {
        // Arrange
        var numero = Ayudas.NumeroDeDocumentoUnico();
        await using (var primero = bd.CrearContexto())
        {
            primero.Clientes.Add(Ayudas.NuevoCliente(numeroDocumento: numero));
            await primero.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        }

        await using (var segundo = bd.CrearContexto())
        {
            segundo.Clientes.Add(Ayudas.NuevoCliente(numeroDocumento: numero));
            await Assert.ThrowsAsync<DocumentoDuplicadoException>(
                () => segundo.GuardarCambiosAsync(TestContext.Current.CancellationToken));
        }

        // Act
        await using var lectura = bd.CrearContexto();
        var cuantos = await lectura.Clientes.CountAsync(
            c => c.Documento.Tipo == TipoDocumento.CC && c.Documento.Numero == numero, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, cuantos);
    }

    [Fact]
    public async Task CL02_MismoNumeroConOtroTipo_SeGuardanLosDos()
    {
        // Arrange
        var numero = Ayudas.NumeroDeDocumentoUnico();
        await using (var escritura = bd.CrearContexto())
        {
            escritura.Clientes.Add(Ayudas.NuevoCliente(TipoDocumento.CC, numero));
            escritura.Clientes.Add(Ayudas.NuevoCliente(TipoDocumento.PA, numero));

            // Act
            await escritura.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var lectura = bd.CrearContexto();
        var cuantos = await lectura.Clientes.CountAsync(c => c.Documento.Numero == numero, TestContext.Current.CancellationToken);
        Assert.Equal(2, cuantos);
    }

    [Fact]
    public async Task CL17_ObtenerPorDocumento_EncuentraAlClienteGuardado()
    {
        // Arrange
        var cliente = Ayudas.NuevoCliente();
        await using (var escritura = bd.CrearContexto())
        {
            escritura.Clientes.Add(cliente);
            await escritura.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        }

        await using var lectura = bd.CrearContexto();
        var repositorio = new ClienteRepositorio(lectura);

        // Act
        var encontrado = await repositorio.ObtenerPorDocumentoAsync(
            Documento.Crear(TipoDocumento.CC, cliente.Documento.Numero), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(encontrado);
        Assert.Equal(cliente.Id, encontrado.Id);
    }

    [Fact]
    public async Task CL18_ObtenerPorDocumentoNoRegistrado_DevuelveNull()
    {
        // Arrange
        await using var lectura = bd.CrearContexto();
        var repositorio = new ClienteRepositorio(lectura);
        var sinRegistrar = Documento.Crear(TipoDocumento.CC, Ayudas.NumeroDeDocumentoUnico());

        // Act
        var encontrado = await repositorio.ObtenerPorDocumentoAsync(sinRegistrar, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(encontrado);
    }

    [Fact]
    public async Task CL18_ObtenerPorIdInexistente_DevuelveNull()
    {
        // Arrange
        await using var lectura = bd.CrearContexto();
        var repositorio = new ClienteRepositorio(lectura);

        // Act
        var encontrado = await repositorio.ObtenerAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(encontrado);
    }
}
