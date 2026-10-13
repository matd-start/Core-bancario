using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.AccesosDigitales;

public class AccesoDigitalTests
{
    private static readonly DateTimeOffset Fecha = new(2026, 10, 13, 15, 2, 11, TimeSpan.Zero);

    // ---- RN-17: el nombre de usuario es el documento normalizado ----

    [Theory]
    [InlineData(TipoDocumento.CC, "1234567", "CC1234567")]
    [InlineData(TipoDocumento.CC, "1.234.567", "CC1234567")]
    [InlineData(TipoDocumento.CC, " 1 234 567 ", "CC1234567")]
    [InlineData(TipoDocumento.CC, "1-234-567", "CC1234567")]
    [InlineData(TipoDocumento.CE, "987654", "CE987654")]
    [InlineData(TipoDocumento.PA, "ab-12.345", "PAAB12345")]
    public void F003_RN17_NombreDeUsuario_EsTipoMasNumeroNormalizado(TipoDocumento tipo, string escrito, string esperado)
    {
        // Arrange
        var documento = Documento.Crear(tipo, escrito);

        // Act
        var nombre = AccesoDigital.NombreDeUsuarioDe(documento);

        // Assert
        Assert.Equal(esperado, nombre);
    }

    [Fact]
    public void F003_RN17_NombreDeUsuario_MismoNumeroConOtroTipo_DaNombresDistintos()
    {
        // Arrange
        var cedula = Documento.Crear(TipoDocumento.CC, "123456");
        var extranjeria = Documento.Crear(TipoDocumento.CE, "123456");

        // Act
        var nombreCedula = AccesoDigital.NombreDeUsuarioDe(cedula);
        var nombreExtranjeria = AccesoDigital.NombreDeUsuarioDe(extranjeria);

        // Assert
        Assert.NotEqual(nombreCedula, nombreExtranjeria);
    }

    [Fact]
    public void F003_RN17_NombreDeUsuario_DocumentoNulo_LanzaArgumentNullException()
    {
        // Arrange
        Documento documento = null!;

        // Act
        var accion = () => AccesoDigital.NombreDeUsuarioDe(documento);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // ---- ADR-0016: el vinculo entre el cliente y su usuario del sistema de identidad ----

    [Fact]
    public void F003_ADR0016_Vincular_DatosValidos_ConservaClienteSubYFecha()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        var sub = Guid.NewGuid().ToString();

        // Act
        var acceso = AccesoDigital.Vincular(clienteId, sub, Fecha);

        // Assert
        Assert.Equal(clienteId, acceso.ClienteId);
        Assert.Equal(sub, acceso.Sub);
        Assert.Equal(Fecha, acceso.FechaDeVinculo);
    }

    [Fact]
    public void F003_ADR0016_Vincular_SubDeExactamente255Caracteres_EsValido()
    {
        // Arrange
        var sub = new string('a', 255);

        // Act
        var acceso = AccesoDigital.Vincular(Guid.NewGuid(), sub, Fecha);

        // Assert
        Assert.Equal(sub, acceso.Sub);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void F003_ADR0016_VincularSinSub_LanzaArgumentException(string? sub)
    {
        // Arrange
        var clienteId = Guid.NewGuid();

        // Act
        var accion = () => AccesoDigital.Vincular(clienteId, sub!, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_ADR0016_VincularSubDeMasDe255Caracteres_LanzaArgumentException()
    {
        // Arrange
        var sub = new string('a', 256);

        // Act
        var accion = () => AccesoDigital.Vincular(Guid.NewGuid(), sub, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_ADR0016_VincularClienteVacio_LanzaArgumentException()
    {
        // Arrange
        var sub = Guid.NewGuid().ToString();

        // Act
        var accion = () => AccesoDigital.Vincular(Guid.Empty, sub, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }
}
