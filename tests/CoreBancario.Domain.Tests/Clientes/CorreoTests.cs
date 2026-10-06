using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.Clientes;

public class CorreoTests
{
    [Theory]
    [InlineData("maria@example.com")]
    [InlineData("a@b")]
    [InlineData("nombre.apellido+etiqueta@dominio.co")]
    public void CA04_CorreoConFormaUsuarioArrobaDominio_EsValido(string valor)
    {
        // Arrange / Act
        var creado = Correo.TryCrear(valor, out var correo);

        // Assert
        Assert.True(creado);
        Assert.Equal(valor, correo!.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-arroba")]
    [InlineData("dos@@example.com")]
    [InlineData("a@b@c")]
    [InlineData("@example.com")]
    [InlineData("usuario@")]
    [InlineData("us uario@example.com")]
    [InlineData("usuario@exam ple.com")]
    public void CA04_CorreoMalFormado_NoEsValido(string? valor)
    {
        // Arrange / Act
        var creado = Correo.TryCrear(valor, out var correo);

        // Assert
        Assert.False(creado);
        Assert.Null(correo);
    }

    [Fact]
    public void CA04_CorreoConControlDentro_NoEsValido()
    {
        // Arrange
        var valor = "usu\u0007ario@example.com";

        // Act
        var creado = Correo.TryCrear(valor, out _);

        // Assert
        Assert.False(creado);
    }

    [Fact]
    public void CA04_CorreoDe254Caracteres_EsValido()
    {
        // Arrange
        var valor = new string('a', 254 - "@b.co".Length) + "@b.co";

        // Act
        var creado = Correo.TryCrear(valor, out _);

        // Assert
        Assert.Equal(254, valor.Length);
        Assert.True(creado);
    }

    [Fact]
    public void CA04_CorreoDe255Caracteres_NoEsValido()
    {
        // Arrange
        var valor = new string('a', 255 - "@b.co".Length) + "@b.co";

        // Act
        var creado = Correo.TryCrear(valor, out _);

        // Assert
        Assert.Equal(255, valor.Length);
        Assert.False(creado);
    }

    [Fact]
    public void CA01_CorreoConEspaciosAlInicioYAlFinal_SeRecorta()
    {
        // Arrange / Act
        var creado = Correo.TryCrear("  maria@example.com  ", out var correo);

        // Assert
        Assert.True(creado);
        Assert.Equal("maria@example.com", correo!.Valor);
    }

    [Fact]
    public void CA04_CrearConValorInvalido_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => Correo.Crear("no-es-correo");

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void CA01_ToString_DevuelveElValor()
    {
        // Arrange
        var correo = Correo.Crear("maria@example.com");

        // Act
        var texto = correo.ToString();

        // Assert
        Assert.Equal("maria@example.com", texto);
    }
}
