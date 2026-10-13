using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Clientes;
using CoreBancario.Application.Clientes;
using CoreBancario.Domain.Clientes;
using static CoreBancario.Keycloak.Tests.Ayudas;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RF-14, RN-21, RN-24, CL-04: el primer ingreso con una clave temporal. Todo de verdad: pantallas de Keycloak, TOTP
/// calculado con el secreto de la pantalla de inscripcion, y despues la API real validando el token.
/// </summary>
public class PrimerIngresoTests(KeycloakFixture kc)
{
    [Fact]
    public async Task F003_CA06_PrimerIngreso_ExigeCambiarClaveEInscribirOtpYLuegoEntraConRolCliente()
    {
        // Arrange: el operador (token real) registra al cliente por HTTP; su usuario y su vinculo los prepara el fixture
        // (en la 003b los hara el endpoint de habilitar).
        var tokenDelOperador = await kc.ObtenerTokenDeOperadorAsync();
        using var apiDelOperador = kc.CrearClienteDeApi(tokenDelOperador);
        var numero = NumeroDeDocumentoUnico();
        var registro = await apiDelOperador.PostAsJsonAsync("/clientes",
            new RegistrarClienteRequest("CC", numero, "María José", "Núñez", "maria@example.com", "+57 300 123 4567"), Ct);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var cliente = (await registro.Content.ReadFromJsonAsync<ClienteDto>(Ct))!;
        var usuario = await kc.CrearUsuarioDeClienteAsync(Documento.Crear(TipoDocumento.CC, numero));
        await kc.VincularAsync(cliente.Id, usuario.Sub);

        // Act: primer ingreso (clave temporal) y despues un ingreso normal con la clave nueva y el codigo.
        using var primerNavegador = kc.NuevoNavegador();
        var (_, primerRecorrido) = await kc.IniciarSesionAsync(primerNavegador, usuario);
        using var segundoNavegador = kc.NuevoNavegador();
        var (tokens, segundoRecorrido) = await kc.IniciarSesionAsync(segundoNavegador, usuario);
        using var apiDelCliente = kc.CrearClienteDeApi(tokens.AccessToken);
        var misCuentas = await apiDelCliente.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Contains(TipoDePagina.CambioDeClave, primerRecorrido);
        Assert.Contains(TipoDePagina.ConfigurarOtp, primerRecorrido);
        Assert.Equal([TipoDePagina.Login, TipoDePagina.Otp], segundoRecorrido);
        Assert.Contains("cliente", CuerpoDelJwt(tokens.AccessToken)["realm_access"]!["roles"]!.AsArray().Select(r => (string)r!));
        Assert.Equal(usuario.Sub, (string?)CuerpoDelJwt(tokens.AccessToken)["sub"]);
        Assert.Equal(HttpStatusCode.OK, misCuentas.StatusCode);
    }

    [Fact]
    public async Task F003_CA07_ConClaveTemporalSinCompletarLosPasos_NoSeEmiteTokenYLaApiResponde401()
    {
        // Arrange
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(usuario.Realm);

        // Act: entra con la clave temporal y NO completa el cambio de clave ni la inscripcion del OTP.
        var tras = await navegador.EnviarAsync(login, ("username", usuario.NombreDeUsuario), ("password", usuario.Clave));
        var otraVez = await navegador.IniciarAsync(usuario.Realm);
        var canje = await navegador.PedirTokenConCodigoAsync(usuario.Realm, "codigo-inventado");
        using var api = kc.CrearClienteDeApi();
        var misCuentas = await api.GetAsync("/mis-cuentas", Ct);

        // Assert: Keycloak sigue pidiendo los pasos (nunca emite el codigo), el canje falla y la API responde 401.
        Assert.True(tras.Tipo is TipoDePagina.CambioDeClave or TipoDePagina.ConfigurarOtp, $"Pantalla: {tras.Tipo}");
        Assert.NotEqual(TipoDePagina.CodigoEmitido, otraVez.Tipo);
        Assert.Equal(HttpStatusCode.BadRequest, canje.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, misCuentas.StatusCode);
    }
}
