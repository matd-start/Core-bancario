using System.Net;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using static CoreBancario.Keycloak.Tests.Ayudas;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RN-17, CL-02, CA-12: el usuario con el que el cliente inicia sesion es su documento normalizado. La normalizacion
/// la hace documento.js en el navegador; aqui se ejecuta con Jint (y se compara con el dominio) y se comprueba lo que
/// ocurre en el servidor con el usuario ya normalizado.
/// </summary>
public class DocumentoDelTemaTests(KeycloakFixture kc)
{
    [Theory]
    [InlineData(TipoDocumento.CC, "1.234.567")]
    [InlineData(TipoDocumento.CC, " 1 234 567 ")]
    [InlineData(TipoDocumento.CC, "1-234-567")]
    [InlineData(TipoDocumento.CC, "1234567")]
    [InlineData(TipoDocumento.CE, "987.654")]
    [InlineData(TipoDocumento.PA, "ab-12.345")]
    public void F003_CL02_ScriptDelTema_NormalizaComoElDominio(TipoDocumento tipo, string escrito)
    {
        // Arrange
        var esperado = AccesoDigital.NombreDeUsuarioDe(Documento.Crear(tipo, escrito));

        // Act
        var delScript = ScriptDelTema.NormalizarUsuario(tipo.ToString(), escrito);

        // Assert
        Assert.Equal(esperado, delScript);
    }

    [Fact]
    public async Task F003_CA12_PaginaDeLogin_CargaElScriptDelTema()
    {
        // Arrange
        using var navegador = kc.NuevoNavegador();

        // Act
        var login = await navegador.IniciarAsync(KeycloakFixture.RealmDeClientes);

        // Assert
        Assert.Equal(TipoDePagina.Login, login.Tipo);
        Assert.True(login.CargaElScript("documento.js"), "La pantalla de login de clientes no carga documento.js.");
        Assert.NotNull(login.Documento!.QuerySelector("input[name='username']"));
    }

    [Fact]
    public async Task F003_CA12_PaginaDeLoginDeEmpleados_NoCargaElScriptDelDocumento()
    {
        // Arrange: los empleados no inician sesion con documento (decision 10 del plan).
        using var navegador = kc.NuevoNavegador();

        // Act
        var login = await navegador.IniciarAsync(KeycloakFixture.RealmDeEmpleados);

        // Assert
        Assert.Equal(TipoDePagina.Login, login.Tipo);
        Assert.False(login.CargaElScript("documento.js"));
    }

    [Fact]
    public async Task F003_CA12_UsuarioNormalizado_EntraComoElCliente()
    {
        // Arrange: CC 1234567 se escribe "1.234.567"; el script escribe el usuario normalizado en el campo antes de enviar.
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var escrito = ConPuntos(usuario.NombreDeUsuario[2..]);
        var enElCampo = ScriptDelTema.NormalizarUsuario("CC", escrito);
        using var navegador = kc.NuevoNavegador();
        var porEscrito = new UsuarioDePrueba(usuario.Realm, enElCampo, usuario.Sub, usuario.Clave);

        // Act
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, porEscrito);
        using var api = kc.CrearClienteDeApi(tokens.AccessToken);
        var misCuentas = await api.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(usuario.Sub, (string?)CuerpoDelJwt(tokens.AccessToken)["sub"]);
        Assert.Equal(HttpStatusCode.OK, misCuentas.StatusCode);
    }

    private static string ConPuntos(string numero) =>
        string.Concat(numero.Select((digito, i) => (numero.Length - i) % 3 == 0 && i > 0 ? $".{digito}" : $"{digito}"));
}
