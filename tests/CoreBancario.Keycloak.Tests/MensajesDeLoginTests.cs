using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Keycloak.Tests;

/// <summary>CA-08 y CL-01: la pantalla de login no revela si un documento es cliente del banco (mismo mensaje generico).</summary>
public class MensajesDeLoginTests(KeycloakFixture kc)
{
    private const string MensajeGenerico = "Los datos de ingreso no son correctos.";

    /// <summary>Intenta entrar con esos datos en un navegador nuevo y devuelve lo que la pantalla dice.</summary>
    private async Task<string> MensajeAlIngresarAsync(string nombreDeUsuario, string clave)
    {
        using var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(KeycloakFixture.RealmDeClientes);
        var respuesta = await navegador.EnviarAsync(login, ("username", nombreDeUsuario), ("password", clave));
        Assert.Equal(TipoDePagina.Login, respuesta.Tipo);
        return respuesta.MensajesDeError;
    }

    [Fact]
    public async Task F003_CA08_DocumentoInexistenteClaveIncorrectaYDeshabilitado_MismoMensaje()
    {
        // Arrange
        var (_, _, existente) = await kc.CrearClienteConAccesoAsync();
        var (_, _, deshabilitado) = await kc.CrearClienteConAccesoAsync();
        await kc.Admin.DeshabilitarUsuarioAsync(KeycloakFixture.RealmDeClientes, deshabilitado.Sub);
        var inexistente = AccesoDigital.NombreDeUsuarioDe(Documento.Crear(TipoDocumento.CC, Ayudas.NumeroDeDocumentoUnico()));

        // Act
        var documentoInexistente = await MensajeAlIngresarAsync(inexistente, Ayudas.ClaveValida());
        var claveIncorrecta = await MensajeAlIngresarAsync(existente.NombreDeUsuario, Ayudas.ClaveValida());
        var accesoDeshabilitado = await MensajeAlIngresarAsync(deshabilitado.NombreDeUsuario, deshabilitado.Clave);

        // Assert
        Assert.Equal(MensajeGenerico, documentoInexistente);
        Assert.Equal(MensajeGenerico, claveIncorrecta);
        Assert.Equal(MensajeGenerico, accesoDeshabilitado);
    }

    [Fact]
    public async Task F003_CL01_CredencialBloqueada_MismoMensajeGenerico()
    {
        // Arrange: un usuario que ya inscribio su OTP y al que se le agotan los 5 intentos.
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        using (var primerIngreso = kc.NuevoNavegador())
            await kc.IniciarSesionAsync(primerIngreso, usuario);
        var mensajeDeClaveIncorrecta = await MensajeAlIngresarAsync(usuario.NombreDeUsuario, Ayudas.ClaveValida());
        await FallarAsync(usuario, intentos: 4);

        // Act: la clave CORRECTA, con la credencial ya bloqueada.
        var bloqueada = await MensajeAlIngresarAsync(usuario.NombreDeUsuario, usuario.Clave);

        // Assert
        Assert.Equal(MensajeGenerico, mensajeDeClaveIncorrecta);
        Assert.Equal(MensajeGenerico, bloqueada);
    }

    private async Task FallarAsync(UsuarioDePrueba usuario, int intentos)
    {
        for (var i = 0; i < intentos; i++)
            await MensajeAlIngresarAsync(usuario.NombreDeUsuario, Ayudas.ClaveValida());
    }
}
