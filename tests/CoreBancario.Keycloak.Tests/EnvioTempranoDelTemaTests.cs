namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// CL-02/CA-12 y CL-06: documento.js corre en el head y DOMContentLoaded espera a los módulos de la plantilla
/// (authChecker.js). Mientras tanto el formulario ya se ve y se puede enviar. Estas pruebas envían antes y después de
/// que termine de cargar la página, sobre un DOM de mentira (PaginaDelTema).
/// </summary>
public class EnvioTempranoDelTemaTests
{
    [Fact]
    public void F003_CL02_EnvioAntesDeQueCargueLaPagina_NormalizaConElTipoPorDefecto()
    {
        // Arrange: el selector aún no existe; el tipo por defecto es el que mostrará (CC).
        var pagina = PaginaDelTema.DeIngreso();
        pagina.Escribir("username", "6.237.280");

        // Act
        var sale = pagina.Enviar();

        // Assert
        Assert.True(sale);
        Assert.Equal("CC6237280", pagina.Valor("username"));
    }

    [Fact]
    public void F003_CL02_EnvioAntesDeCargar_UsuarioYaNormalizado_NoLoDuplica()
    {
        // Arrange: tras un intento fallido, Keycloak devuelve el campo con el usuario ya normalizado.
        var pagina = PaginaDelTema.DeIngreso();
        pagina.Escribir("username", "CE987654");

        // Act
        pagina.Enviar();

        // Assert
        Assert.Equal("CE987654", pagina.Valor("username"));
    }

    [Fact]
    public void F003_CL02_EnvioConLaPaginaCargada_UsaElTipoElegidoUnaSolaVez()
    {
        // Arrange
        var pagina = PaginaDelTema.DeIngreso();
        pagina.TerminarDeCargar();
        pagina.ElegirTipo("PA");
        pagina.Escribir("username", "ab-12.345");

        // Act
        pagina.Enviar();

        // Assert
        Assert.Equal("PAAB12345", pagina.Valor("username"));
    }

    [Fact]
    public void F003_CL06_EnvioAntesDeCargar_ClaveConElNumero_CancelaElEnvio()
    {
        // Arrange
        var pagina = PaginaDelTema.DeCambioDeClave("CC1234567");
        pagina.Escribir("password-new", "Abc1234567xyz!");

        // Act
        var sale = pagina.Enviar();

        // Assert
        Assert.False(sale, "La clave nueva contiene el número de documento y el envío no se canceló.");
    }

    [Fact]
    public void F003_CL06_EnvioConLaPaginaCargada_ClaveSinElNumero_SeEnvia()
    {
        // Arrange
        var pagina = PaginaDelTema.DeCambioDeClave("CC1234567");
        pagina.TerminarDeCargar();
        pagina.Escribir("password-new", "OtraClaveLarga!");

        // Act
        var sale = pagina.Enviar();

        // Assert
        Assert.True(sale);
    }
}
