using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.Clientes;

public class DocumentoTests
{
    [Theory]
    [InlineData("1.234.567")]
    [InlineData(" 1234567 ")]
    [InlineData("1-234-567")]
    [InlineData("1 234 567")]
    public void CL01_NumeroEscritoDistinto_SonElMismoDocumento(string escrito)
    {
        // Arrange
        Documento.TryCrear(TipoDocumento.CC, "1234567", out var referencia);

        // Act
        var creado = Documento.TryCrear(TipoDocumento.CC, escrito, out var documento);

        // Assert
        Assert.True(creado);
        Assert.Equal(referencia, documento);
        Assert.Equal("1234567", documento!.Numero);
    }

    [Fact]
    public void CL02_MismoNumeroConOtroTipo_SonDocumentosDistintos()
    {
        // Arrange
        Documento.TryCrear(TipoDocumento.CC, "1234567", out var cedula);
        Documento.TryCrear(TipoDocumento.PA, "1234567", out var pasaporte);

        // Act
        var iguales = cedula == pasaporte;

        // Assert
        Assert.False(iguales);
        Assert.NotNull(cedula);
        Assert.NotNull(pasaporte);
    }

    [Fact]
    public void CA01_PasaporteEnMinusculasConGuion_SeNormalizaAMayusculasSinGuion()
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.PA, " ab-12.345 ", out var documento);

        // Assert
        Assert.True(creado);
        Assert.Equal("AB12345", documento!.Numero);
        Assert.Equal(TipoDocumento.PA, documento.Tipo);
    }

    [Theory]
    [InlineData(TipoDocumento.CC, "123")]
    [InlineData(TipoDocumento.CC, "1234567890")]
    [InlineData(TipoDocumento.CE, "987654")]
    [InlineData(TipoDocumento.PA, "A1234")]
    [InlineData(TipoDocumento.PA, "ABCDEFGHIJ12345")]
    public void CL05_DocumentoDentroDeRango_EsValido(TipoDocumento tipo, string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(tipo, numero, out var documento);

        // Assert
        Assert.True(creado);
        Assert.NotNull(documento);
    }

    [Theory]
    [InlineData("12A4567")]
    [InlineData("12.5e7")]
    public void CL05_CedulaConLetras_NoEsValida(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.CC, numero, out var documento);

        // Assert
        Assert.False(creado);
        Assert.Null(documento);
    }

    [Theory]
    [InlineData("0123456")]
    [InlineData("000")]
    public void CL05_CedulaQueEmpiezaPorCero_NoEsValida(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.CC, numero, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345678901")]
    public void CL05_CedulaFueraDeRango_NoEsValida(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.CC, numero, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData("AB/12345")]
    [InlineData("AB#12345")]
    [InlineData("ÁB12345")]
    public void CL05_PasaporteConSimbolos_NoEsValido(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.PA, numero, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData("A123")]
    [InlineData("ABCDEFGHIJ123456")]
    public void CL05_PasaporteFueraDeRango_NoEsValido(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.PA, numero, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData("AB12345")]
    [InlineData("12E4567")]
    public void CL05_CedulaDeExtranjeriaConLetras_NoEsValida(string numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.CE, numero, out _);

        // Assert
        Assert.False(creado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".-.")]
    public void CL05_NumeroAusenteOVacioAlNormalizar_NoEsValido(string? numero)
    {
        // Arrange / Act
        var creado = Documento.TryCrear(TipoDocumento.CC, numero, out var documento);

        // Assert
        Assert.False(creado);
        Assert.Null(documento);
    }

    [Fact]
    public void CL05_TipoNoDefinido_NoEsValido()
    {
        // Arrange / Act
        var creado = Documento.TryCrear((TipoDocumento)99, "1234567", out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void CL05_CrearConFormatoInvalido_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => Documento.Crear(TipoDocumento.CC, "ABC");

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void CA01_CrearConNumeroValido_DevuelveElDocumentoNormalizado()
    {
        // Arrange / Act
        var documento = Documento.Crear(TipoDocumento.CC, "1.234.567");

        // Assert
        Assert.Equal(TipoDocumento.CC, documento.Tipo);
        Assert.Equal("1234567", documento.Numero);
    }

    [Fact]
    public void RN17_ToString_MuestraTipoYNumeroNormalizado()
    {
        // Arrange
        var documento = Documento.Crear(TipoDocumento.CC, "1.234.567");

        // Act
        var texto = documento.ToString();

        // Assert
        Assert.Equal("CC 1234567", texto);
    }
}
