using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>ADR-0017: encontrar el acceso de un cliente (vínculo en la base y usuario en el sistema de identidad).</summary>
public class LocalizadorDeAccesoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly UnidadDeTrabajoEspia _unidad = new();
    private readonly ProveedorDeIdentidadEnMemoria _proveedor = new();
    private readonly Domain.Clientes.Cliente _cliente = Datos.Cliente();

    private AccesoDigitalRepositorioEnMemoria CrearVinculos() => new(_unidad);

    private LocalizadorDeAcceso CrearLocalizador(AccesoDigitalRepositorioEnMemoria vinculos) => new(vinculos, _proveedor);

    private string NombreDeUsuario => AccesoDigital.NombreDeUsuarioDe(_cliente.Documento);

    [Fact]
    public async Task F003_ADR0017_LocalizarConVinculo_ObtieneAlUsuarioPorSuSub()
    {
        // Arrange
        var usuario = _proveedor.Sembrar(NombreDeUsuario, habilitado: true);
        var vinculos = CrearVinculos();
        vinculos.Sembrar(AccesoDigital.Vincular(_cliente.Id, usuario.Sub, Datos.Ahora));

        // Act
        var acceso = await CrearLocalizador(vinculos).LocalizarAsync(_cliente, Ct);

        // Assert
        Assert.Equal(usuario.Sub, acceso.Usuario?.Sub);
        Assert.Equal(_cliente.Id, acceso.Vinculo?.ClienteId);
        Assert.Equal([nameof(IProveedorDeIdentidad.ObtenerAsync)], _proveedor.Llamadas);
    }

    [Fact]
    public async Task F003_ADR0017_LocalizarSinVinculo_BuscaPorElNombreDeUsuarioDelDocumento()
    {
        // Arrange: un habilitar interrumpido dejó al usuario en el proveedor, sin vínculo en la base.
        var huerfano = _proveedor.Sembrar(NombreDeUsuario, habilitado: true);

        // Act
        var acceso = await CrearLocalizador(CrearVinculos()).LocalizarAsync(_cliente, Ct);

        // Assert: se adopta; no hay vínculo todavía.
        Assert.Null(acceso.Vinculo);
        Assert.Equal(huerfano.Sub, acceso.Usuario?.Sub);
        Assert.Equal([nameof(IProveedorDeIdentidad.BuscarPorNombreDeUsuarioAsync)], _proveedor.Llamadas);
    }

    [Fact]
    public async Task F003_ADR0017_LocalizarSinVinculoNiUsuario_NoEncuentraNada()
    {
        // Arrange / Act
        var acceso = await CrearLocalizador(CrearVinculos()).LocalizarAsync(_cliente, Ct);

        // Assert
        Assert.Null(acceso.Vinculo);
        Assert.Null(acceso.Usuario);
    }

    [Fact]
    public async Task F003_ADR0017_VinculoSinUsuarioEnElProveedor_LanzaInvalidOperationException()
    {
        // Arrange: alguien borró a mano al usuario de Keycloak y el vínculo sigue vivo (inconsistencia).
        var vinculos = CrearVinculos();
        vinculos.Sembrar(AccesoDigital.Vincular(_cliente.Id, Guid.NewGuid().ToString(), Datos.Ahora));

        // Act
        var accion = () => CrearLocalizador(vinculos).LocalizarAsync(_cliente, Ct);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(accion);
    }

    [Fact]
    public async Task F003_ADR0017_LocalizarConElProveedorCaido_PropagaSinRespuesta()
    {
        // Arrange
        _proveedor.NoResponder(tambienAlLeer: true);

        // Act
        var accion = () => CrearLocalizador(CrearVinculos()).LocalizarAsync(_cliente, Ct);

        // Assert
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(accion);
    }

    // ---- AccesoLocalizado.Estado ----

    [Fact]
    public void F003_ADR0017_AccesoLocalizadoSinUsuario_EstaSinAcceso()
    {
        // Arrange
        var acceso = new AccesoLocalizado(null, null);

        // Act
        var estado = acceso.Estado;

        // Assert
        Assert.Equal(EstadoDeAcceso.SinAcceso, estado);
    }

    [Theory]
    [InlineData(true, EstadoDeAcceso.Habilitado)]
    [InlineData(false, EstadoDeAcceso.Deshabilitado)]
    public void F003_ADR0017_AccesoLocalizadoConUsuario_TomaElEstadoDelProveedor(bool habilitado, EstadoDeAcceso esperado)
    {
        // Arrange
        var acceso = new AccesoLocalizado(null, new UsuarioDeIdentidad("sub", habilitado, null));

        // Act
        var estado = acceso.Estado;

        // Assert
        Assert.Equal(esperado, estado);
    }
}
