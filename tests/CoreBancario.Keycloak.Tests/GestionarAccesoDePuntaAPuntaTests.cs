using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoreBancario.Api.Clientes;
using CoreBancario.Api.Cuentas;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Clientes;
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using static CoreBancario.Keycloak.Tests.Ayudas;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RF-13, RNF-08 y ADR-0018, punta a punta: un operador con sesión real gestiona el acceso de un cliente por la API, y el
/// cliente entra de verdad por Keycloak. A diferencia de la 003a, aquí NO se crea al usuario con el admin de master: lo
/// hace el endpoint de habilitar con la cuenta de servicio y su secreto.
/// </summary>
public class GestionarAccesoDePuntaAPuntaTests(KeycloakFixture kc)
{
    private const string MensajeGenerico = "Los datos de ingreso no son correctos.";

    private sealed record ClienteDelOperador(HttpClient Operador, ClienteDto Cliente);

    /// <summary>Un operador que inició sesión de verdad y un cliente que él registra por HTTP.</summary>
    private async Task<ClienteDelOperador> OperadorYClienteRegistradoAsync()
    {
        var tokenDelOperador = await kc.ObtenerTokenDeOperadorAsync();
        var operador = kc.CrearClienteDeApi(tokenDelOperador);
        var registro = await operador.PostAsJsonAsync("/clientes",
            new RegistrarClienteRequest("CC", NumeroDeDocumentoUnico(), "María José", "Núñez", "maria@example.com", "+57 300 123 4567"), Ct);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        return new ClienteDelOperador(operador, (await registro.Content.ReadFromJsonAsync<ClienteDto>(Ct))!);
    }

    private static async Task<AccesoHabilitadoDto> HabilitarAsync(HttpClient operador, ClienteDto cliente)
    {
        var respuesta = await operador.PostAsync($"/clientes/{cliente.Id}/acceso-digital/habilitar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct))!;
    }

    private static UsuarioDePrueba UsuarioDe(ClienteDto cliente, string claveTemporal) =>
        new(KeycloakFixture.RealmDeClientes,
            AccesoDigital.NombreDeUsuarioDe(Documento.Crear(TipoDocumento.CC, cliente.NumeroDocumento)),
            sub: string.Empty,
            claveTemporal);

    // ---- RNF-08: el flujo completo ----

    [Fact]
    public async Task F003_RNF08_FlujoCompleto_OperadorHabilitaYClienteConsultaSusCuentas()
    {
        // Arrange: el operador registra al cliente, le abre una cuenta y habilita su acceso (sin tocar el admin de master).
        var (operador, cliente) = await OperadorYClienteRegistradoAsync();
        var apertura = await operador.PostAsJsonAsync($"/clientes/{cliente.Id}/cuentas", new AbrirCuentaRequest("COP"), Ct);
        var cuenta = (await apertura.Content.ReadFromJsonAsync<CuentaDto>(Ct))!;
        var acceso = await HabilitarAsync(operador, cliente);
        var usuario = UsuarioDe(cliente, acceso.ClaveTemporal);

        // Act: el cliente entra por primera vez con la clave temporal y consulta sus cuentas con el token real.
        using var navegador = kc.NuevoNavegador();
        var (tokens, recorrido) = await kc.IniciarSesionAsync(navegador, usuario);
        using var apiDelCliente = kc.CrearClienteDeApi(tokens.AccessToken);
        var misCuentas = await apiDelCliente.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Contains(TipoDePagina.CambioDeClave, recorrido);
        Assert.Contains(TipoDePagina.ConfigurarOtp, recorrido);
        Assert.Equal(HttpStatusCode.OK, misCuentas.StatusCode);
        var cuentas = await misCuentas.Content.ReadFromJsonAsync<List<CuentaDto>>(Ct);
        Assert.Equal([cuenta.Numero], cuentas!.Select(c => c.Numero));
        Assert.Contains("cliente", CuerpoDelJwt(tokens.AccessToken)["realm_access"]!["roles"]!.AsArray().Select(r => (string)r!));
    }

    // ---- CA-03: deshabilitar con una sesión abierta ----

    [Fact]
    public async Task F003_CA03_Deshabilitar_SesionNoSeRenuevaYNoPuedeIniciarSesion()
    {
        // Arrange: un cliente con acceso habilitado y una sesión abierta (ya completó el primer ingreso).
        var (operador, cliente) = await OperadorYClienteRegistradoAsync();
        var acceso = await HabilitarAsync(operador, cliente);
        var usuario = UsuarioDe(cliente, acceso.ClaveTemporal);
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, usuario);

        // Act
        var deshabilitar = await operador.PostAsync($"/clientes/{cliente.Id}/acceso-digital/deshabilitar", null, Ct);
        using var renovacion = await navegador.RenovarAsync(usuario.Realm, tokens.RefreshToken);
        using var otroNavegador = kc.NuevoNavegador();
        var login = await otroNavegador.IniciarAsync(usuario.Realm);
        var intento = await otroNavegador.EnviarAsync(login, ("username", usuario.NombreDeUsuario), ("password", usuario.Clave));

        // Assert: la sesión no se renueva y el inicio de sesión se rechaza con el mensaje genérico (CL-01).
        Assert.Equal(HttpStatusCode.OK, deshabilitar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, renovacion.StatusCode);
        Assert.Equal("invalid_grant", (string?)(await renovacion.Content.ReadFromJsonAsync<JsonNode>(Ct))!["error"]);
        Assert.Equal(TipoDePagina.Login, intento.Tipo);
        Assert.Equal(MensajeGenerico, intento.MensajesDeError);
    }

    // ---- CL-15: restablecer con una sesión abierta ----

    [Fact]
    public async Task F003_CL15_Restablecer_SesionNoSeRenueva()
    {
        // Arrange: un cliente con una sesión abierta; el operador habilita DE NUEVO (restablece) su acceso.
        var (operador, cliente) = await OperadorYClienteRegistradoAsync();
        var primera = await HabilitarAsync(operador, cliente);
        var usuario = UsuarioDe(cliente, primera.ClaveTemporal);
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, usuario);

        // Act
        var segunda = await HabilitarAsync(operador, cliente);
        using var renovacion = await navegador.RenovarAsync(usuario.Realm, tokens.RefreshToken);

        // Assert
        Assert.Equal("Restablecer", segunda.Accion);
        Assert.NotEqual(primera.ClaveTemporal, segunda.ClaveTemporal);
        Assert.Equal(HttpStatusCode.BadRequest, renovacion.StatusCode);
    }

    // ---- CA-02: tras restablecer, el cliente entra con la clave temporal nueva ----

    [Fact]
    public async Task F003_CA02_Restablecer_ElClienteEntraConLaClaveNuevaYReinscribeElSegundoFactor()
    {
        // Arrange: el cliente ya completó su primer ingreso (clave propia y OTP inscrito).
        var (operador, cliente) = await OperadorYClienteRegistradoAsync();
        var primera = await HabilitarAsync(operador, cliente);
        var usuario = UsuarioDe(cliente, primera.ClaveTemporal);
        using (var primerIngreso = kc.NuevoNavegador())
            await kc.IniciarSesionAsync(primerIngreso, usuario);
        var segunda = await HabilitarAsync(operador, cliente);

        // Act
        usuario.Clave = segunda.ClaveTemporal;
        usuario.Otp = null;
        usuario.ClaveNueva = ClaveValida();
        using var segundoIngreso = kc.NuevoNavegador();
        var (_, recorrido) = await kc.IniciarSesionAsync(segundoIngreso, usuario);

        // Assert
        Assert.Contains(TipoDePagina.CambioDeClave, recorrido);
        Assert.Contains(TipoDePagina.ConfigurarOtp, recorrido);
    }

    // ---- CL-16: rehabilitar ----

    [Fact]
    public async Task F003_CL16_HabilitarUnAccesoDeshabilitado_ElClienteVuelveAEntrar()
    {
        // Arrange
        var (operador, cliente) = await OperadorYClienteRegistradoAsync();
        var primera = await HabilitarAsync(operador, cliente);
        (await operador.PostAsync($"/clientes/{cliente.Id}/acceso-digital/deshabilitar", null, Ct)).EnsureSuccessStatusCode();

        // Act
        var nueva = await HabilitarAsync(operador, cliente);
        var usuario = UsuarioDe(cliente, nueva.ClaveTemporal);
        using var navegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(navegador, usuario);

        // Assert
        Assert.Equal("Habilitar", nueva.Accion);
        Assert.NotEqual(primera.ClaveTemporal, nueva.ClaveTemporal);
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
    }
}
