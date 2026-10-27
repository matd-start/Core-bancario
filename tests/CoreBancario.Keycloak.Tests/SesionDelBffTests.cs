using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static CoreBancario.Keycloak.Tests.Ayudas;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// 004a, el BFF REAL contra Keycloak real y la API real (plan, seccion 7): inicio de sesion por el BFF, validacion del
/// regreso, ruta de regreso, una sola sesion por navegador, cambio de clave, revocacion e inactividad con tiempos cortos
/// (inactividad 40 s, vida maxima 120 s). El navegador es NavegadorDelBff: cookies a mano, el BFF en memoria y Keycloak
/// en su puerto.
/// </summary>
public partial class SesionDelBffTests(KeycloakFixture kc)
{
    private const string MensajeGenerico = "Los datos de ingreso no son correctos.";

    private static readonly TimeSpan EsperaMasAllaDeLaInactividad = KeycloakFixture.InactividadEnLasPruebas + TimeSpan.FromSeconds(5);

    [GeneratedRegex(@"eyJ[\w-]+\.eyJ[\w-]+")]
    private static partial Regex PatronDeJwt();

    /// <summary>Un cliente con acceso habilitado que ya inicio sesion por el BFF (primer ingreso con OTP incluido).</summary>
    private sealed class ClienteConSesion(NavegadorDelBff navegador, UsuarioDePrueba usuario, Guid clienteId, RespuestaDelBff regreso) : IDisposable
    {
        public NavegadorDelBff Navegador { get; } = navegador;

        public UsuarioDePrueba Usuario { get; } = usuario;

        public Guid ClienteId { get; } = clienteId;

        public RespuestaDelBff Regreso { get; } = regreso;

        public void Dispose() => Navegador.Dispose();
    }

    private async Task<ClienteConSesion> ClienteConSesionAsync(string? regreso = null)
    {
        var (clienteId, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var navegador = kc.NuevoNavegadorDelBff();
        var respuesta = await navegador.IniciarSesionCompletaAsync(usuario, "clientes", regreso);
        return new ClienteConSesion(navegador, usuario, clienteId, respuesta);
    }

    /// <summary>Llega hasta el regreso de Keycloak con el codigo, sin entregarlo al BFF (para alterarlo).</summary>
    private async Task<(NavegadorDelBff Navegador, Pagina Emitida)> HastaElCodigoAsync(UsuarioDePrueba usuario, string area)
    {
        var navegador = kc.NuevoNavegadorDelBff();
        var (ultima, _) = await navegador.ConducirAsync(await navegador.IniciarSesionAsync(area), usuario);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
        return (navegador, ultima);
    }

    // ---- CA-01: el cliente entra, ve sus cuentas y al salir Keycloak no deja sesiones ----

    [Fact]
    public async Task F004_CA01_SesionPorElBff_MisCuentasYAlSalirKeycloakSinSesiones()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();

        // Act
        var misCuentas = await cliente.Navegador.GetAsync("/api/mis-cuentas");
        var salida = await cliente.Navegador.PostAsync("/bff/salir");
        var despues = await cliente.Navegador.GetAsync("/api/mis-cuentas");

        // Assert
        Assert.Equal(HttpStatusCode.OK, misCuentas.Estado);
        Assert.IsType<JsonArray>(JsonNode.Parse(misCuentas.Cuerpo));
        Assert.Equal(HttpStatusCode.NoContent, salida.Estado);
        Assert.Equal(0, await kc.Admin.SesionesAbiertasAsync(cliente.Usuario.Realm, cliente.Usuario.Sub));
        Assert.Equal(HttpStatusCode.Unauthorized, despues.Estado);
    }

    [Fact]
    public async Task F004_CA01_TrasSalir_VolverAEntrarPideUsuarioClaveYCodigo()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();
        await cliente.Navegador.PostAsync("/bff/salir");

        // Act
        var inicio = await cliente.Navegador.IniciarSesionAsync("clientes");
        var (ultima, recorrido) = await cliente.Navegador.ConducirAsync(inicio, cliente.Usuario);

        // Assert
        Assert.Equal(TipoDePagina.Login, inicio.Tipo);
        Assert.Equal([TipoDePagina.Login, TipoDePagina.Otp], recorrido);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
    }

    // ---- CA-02: el operador ----

    [Fact]
    public async Task F004_CA02_OperadorPorElBff_SesionConRolOperador()
    {
        // Arrange
        var operador = await kc.CrearOperadorAsync();
        using var navegador = kc.NuevoNavegadorDelBff();

        // Act
        await navegador.IniciarSesionCompletaAsync(operador, "empleados");
        var sesion = await navegador.ObtenerSesionAsync();

        // Assert
        Assert.NotNull(sesion);
        Assert.Equal("empleados", (string?)sesion["area"]);
        Assert.Equal("Operador", (string?)sesion["rol"]);
        Assert.Equal(operador.NombreDeUsuario, (string?)sesion["usuario"], ignoreCase: true);
    }

    // ---- CA-04 / CL-01: el regreso se valida ----

    [Fact]
    public async Task F004_CA04_RegresoConStateDistinto_NoCreaSesion()
    {
        // Arrange
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var (navegador, emitida) = await HastaElCodigoAsync(usuario, "clientes");
        using var cierre = navegador;

        // Act
        var respuesta = await navegador.EntregarRegresoAsync(NavegadorDelBff.ConParametro(emitida.Url, "state", "estado-que-no-inicio-este-flujo"));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.Estado);
        Assert.Equal("/?aviso=inicio-fallido", respuesta.Destino);
        Assert.Null(await navegador.ObtenerSesionAsync());
    }

    [Fact]
    public async Task F004_CA04_RegresoConIssDeOtroEmisor_NoCreaSesion()
    {
        // Arrange
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var (navegador, emitida) = await HastaElCodigoAsync(usuario, "clientes");
        using var cierre = navegador;

        // Act
        var respuesta = await navegador.EntregarRegresoAsync(NavegadorDelBff.ConParametro(emitida.Url, "iss", "http://otro-emisor.example/realms/clientes"));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.Estado);
        Assert.Equal("/?aviso=inicio-fallido", respuesta.Destino);
        Assert.Null(await navegador.ObtenerSesionAsync());
    }

    [Fact]
    public async Task F004_CA04_RegresoConError_NoCreaSesion()
    {
        // Arrange: el mismo regreso, pero Keycloak "dice" que hubo un error y no entrega el codigo.
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var (navegador, emitida) = await HastaElCodigoAsync(usuario, "clientes");
        using var cierre = navegador;
        var conError = NavegadorDelBff.ConParametro(NavegadorDelBff.ConParametro(emitida.Url, "code", null), "error", "invalid_request");

        // Act
        var respuesta = await navegador.EntregarRegresoAsync(conError);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.Estado);
        Assert.Equal("/?aviso=inicio-fallido", respuesta.Destino);
        Assert.Null(await navegador.ObtenerSesionAsync());
    }

    // ---- CL-02: regreso reutilizado ----

    [Fact]
    public async Task F004_CL02_RegresoReutilizado_NoCreaOtraSesionYVaAlInicio()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();

        // Act
        var segunda = await cliente.Navegador.EntregarRegresoAsync(cliente.Navegador.UltimoRegreso!);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, segunda.Estado);
        Assert.Equal("/mis-cuentas", segunda.Destino);
        Assert.NotNull(await cliente.Navegador.ObtenerSesionAsync());
    }

    [Fact]
    public async Task F004_CL02_RegresoReutilizadoSinSesion_VaAlAvisoInicioFallido()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();
        await cliente.Navegador.PostAsync("/bff/salir");

        // Act
        var segunda = await cliente.Navegador.EntregarRegresoAsync(cliente.Navegador.UltimoRegreso!);

        // Assert
        Assert.Equal("/?aviso=inicio-fallido", segunda.Destino);
        Assert.Null(await cliente.Navegador.ObtenerSesionAsync());
    }

    // ---- CL-03: cada area inicia en su realm ----

    [Fact]
    public async Task F004_CL03_IniciarEnEmpleados_RedirigeAlRealmDeEmpleados()
    {
        // Arrange
        using var navegador = kc.NuevoNavegadorDelBff();

        // Act
        var pantalla = await navegador.IniciarSesionAsync("empleados");

        // Assert
        Assert.Equal(TipoDePagina.Login, pantalla.Tipo);
        Assert.Contains("/realms/empleados/", pantalla.Url.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F004_CL03_CredencialDeClientesEnEmpleados_MuestraElErrorGenerico()
    {
        // Arrange
        var (_, _, cliente) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegadorDelBff();
        var login = await navegador.IniciarSesionAsync("empleados");

        // Act
        var intento = await navegador.EnviarAsync(login, ("username", cliente.NombreDeUsuario), ("password", cliente.Clave));

        // Assert
        Assert.Equal(TipoDePagina.Login, intento.Tipo);
        Assert.Equal(MensajeGenerico, intento.MensajesDeError);
    }

    // ---- CA-05 / RNF-01: ningun token en la cookie ni en lo que recibe la pagina ----

    [Fact]
    public async Task F004_CA05_CookieDeSesion_EsHttpOnlyYNoContieneTokens()
    {
        // Arrange / Act
        using var cliente = await ClienteConSesionAsync();
        var cookie = cliente.Regreso.Cookie(NavegadorDelBff.NombreDeLaCookieDeSesion)!.ToLowerInvariant();
        var sesion = await cliente.Navegador.GetAsync("/bff/sesion");
        var misCuentas = await cliente.Navegador.GetAsync("/api/mis-cuentas");

        // Assert
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.DoesNotMatch(PatronDeJwt(), cookie);
        Assert.DoesNotMatch(PatronDeJwt(), cliente.Regreso.Cuerpo);
        Assert.DoesNotMatch(PatronDeJwt(), sesion.Cuerpo);
        Assert.DoesNotMatch(PatronDeJwt(), misCuentas.Cuerpo);
    }

    // ---- RNF-05: la peticion de autorizacion ----

    [Fact]
    public async Task F004_RNF05_AutorizacionDelBff_UsaCodigoConPkceS256YPromptLogin()
    {
        // Arrange
        using var navegador = kc.NuevoNavegadorDelBff();

        // Act
        var respuesta = await navegador.PedirAsync(HttpMethod.Get, "/bff/iniciar-sesion/clientes", conCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.Estado);
        var consulta = System.Web.HttpUtility.ParseQueryString(respuesta.Ubicacion!.Query);
        Assert.Contains("/realms/clientes/protocol/openid-connect/auth", respuesta.Ubicacion.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("code", consulta["response_type"]);
        Assert.Equal("S256", consulta["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(consulta["code_challenge"]));
        Assert.Equal("login", consulta["prompt"]);
        Assert.Equal(NavegadorSimulado.ClientId, consulta["client_id"]);
        Assert.Equal("http://localhost/bff/signin-oidc/clientes", consulta["redirect_uri"]);
        Assert.Equal("openid", consulta["scope"]);
        Assert.False(string.IsNullOrEmpty(consulta["state"]));
        Assert.Null(consulta["request_uri"]);
    }

    // ---- CA-10 / CL-09: tras cerrar por inactividad Keycloak no reabre la sesion ----

    [Fact]
    public async Task F004_CA10_TrasCerrarPorInactividad_EntrarPideUsuarioClaveYCodigo()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();
        await Task.Delay(EsperaMasAllaDeLaInactividad, Ct);
        var vencida = await cliente.Navegador.GetAsync("/bff/sesion");

        // Act
        var inicio = await cliente.Navegador.IniciarSesionAsync("clientes");
        var (ultima, recorrido) = await cliente.Navegador.ConducirAsync(inicio, cliente.Usuario);

        // Assert
        Assert.Equal("sesion-vencida-por-inactividad", NavegadorDelBff.CodigoDe(vencida));
        Assert.Equal(TipoDePagina.Login, inicio.Tipo);
        Assert.Equal([TipoDePagina.Login, TipoDePagina.Otp], recorrido);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
    }

    [Fact]
    public async Task F004_CL09_IniciarConSesionDeKeycloakViva_PideCredenciales()
    {
        // Arrange: la sesion de Keycloak sigue viva (no se cerro), pero esta "ventana" ya no tiene la cookie del BFF.
        using var cliente = await ClienteConSesionAsync();
        cliente.Navegador.OlvidarCookiesDelBff();

        // Act: prompt=login obliga a reautenticar. Keycloak 26 muestra la pantalla de clave (o la de usuario y clave), nunca el codigo.
        var pantalla = await cliente.Navegador.IniciarSesionAsync("clientes");
        var (ultima, recorrido) = await cliente.Navegador.ConducirAsync(pantalla, cliente.Usuario);

        // Assert
        Assert.True(pantalla.Tipo is TipoDePagina.Clave or TipoDePagina.Login, $"Primera pantalla: {pantalla.Tipo}");
        Assert.NotEqual(TipoDePagina.CodigoEmitido, pantalla.Tipo);
        Assert.Contains(TipoDePagina.Otp, recorrido);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
    }

    // ---- CA-13 / CL-08: la ruta de regreso ----

    [Fact]
    public async Task F004_CA13_EnlaceProfundo_TrasIniciarSesionLlegaALaRuta()
    {
        // Arrange
        var ruta = $"/mis-cuentas/{Guid.NewGuid()}";

        // Act
        using var cliente = await ClienteConSesionAsync(regreso: ruta);

        // Assert
        Assert.Equal(ruta, cliente.Regreso.Destino);
    }

    [Theory]
    [InlineData("https://evil.example/robar")]
    [InlineData("/backoffice/clientes")]
    public async Task F004_CA13_RegresoExternoODelBackoffice_LlegaAMisCuentas(string regreso)
    {
        // Arrange / Act
        using var cliente = await ClienteConSesionAsync(regreso);

        // Assert
        Assert.Equal("/mis-cuentas", cliente.Regreso.Destino);
    }

    // ---- CA-14 / RN-26: una sola sesion por navegador ----

    [Fact]
    public async Task F004_CA14_ConSesionDeCliente_IniciarEnEmpleados_RedirigeAOtraSesionAbierta()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();

        // Act
        var respuesta = await cliente.Navegador.PedirAsync(HttpMethod.Get, "/bff/iniciar-sesion/empleados", conCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, respuesta.Estado);
        Assert.Equal("/backoffice?aviso=otra-sesion-abierta", respuesta.Destino);
    }

    [Fact]
    public async Task F004_RN26_RegresoDeOtraSesion_CierraLaAnteriorEnKeycloak()
    {
        // Arrange: B empieza su inicio de sesion; mientras tanto A inicia sesion en el mismo navegador (el BFF ya tiene su
        // sesion); B termina y su regreso llega con la cookie de A.
        var (_, _, primero) = await kc.CrearClienteConAccesoAsync();
        var (_, _, segundo) = await kc.CrearClienteConAccesoAsync();
        using var navegadorDeB = kc.NuevoNavegadorDelBff();
        var pantallaDeB = await navegadorDeB.IniciarSesionAsync("clientes");
        using var navegadorDeA = kc.NuevoNavegadorDelBff();
        await navegadorDeA.IniciarSesionCompletaAsync(primero, "clientes");
        navegadorDeB.CopiarCookieDeSesionDe(navegadorDeA);
        var (emitida, _) = await navegadorDeB.ConducirAsync(pantallaDeB, segundo);

        // Act
        var regreso = await navegadorDeB.EntregarRegresoAsync(emitida.Url);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, regreso.Estado);
        Assert.Equal(0, await kc.Admin.SesionesAbiertasAsync(primero.Realm, primero.Sub));
        var sesion = await navegadorDeB.ObtenerSesionAsync();
        Assert.Equal(segundo.NombreDeUsuario, (string?)sesion?["usuario"], ignoreCase: true);
        Assert.Equal(1, await kc.Admin.SesionesAbiertasAsync(segundo.Realm, segundo.Sub));
    }

    // ---- CA-16 / CL-12: revocacion ----

    [Fact]
    public async Task F004_CA16_OperadorDeshabilita_LaSiguienteActividadCierraComoRevocadaYNoPuedeEntrar()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();
        using var operador = kc.CrearClienteDeApi(await kc.ObtenerTokenDeOperadorAsync());
        var deshabilitar = await operador.PostAsync($"/clientes/{cliente.ClienteId}/acceso-digital/deshabilitar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, deshabilitar.StatusCode);

        // Act
        var actividad = await cliente.Navegador.PostAsync("/bff/actividad");
        var login = await cliente.Navegador.IniciarSesionAsync("clientes");
        var intento = await cliente.Navegador.EnviarAsync(login,
            ("username", cliente.Usuario.NombreDeUsuario), ("password", cliente.Usuario.Clave));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, actividad.Estado);
        Assert.Equal("sesion-revocada", NavegadorDelBff.CodigoDe(actividad));
        Assert.Equal(TipoDePagina.Login, intento.Tipo);
        Assert.Equal(MensajeGenerico, intento.MensajesDeError);
    }

    // ---- CA-21 / RF-15: cambiar la clave por el BFF ----

    private async Task<(RespuestaDelBff Regreso, string ClaveAnterior)> CambiarClaveAsync(ClienteConSesion cliente)
    {
        var claveAnterior = cliente.Usuario.Clave;
        cliente.Usuario.ClaveNueva = ClaveValida();
        var inicio = await cliente.Navegador.NavegarAsync("/bff/cambiar-clave");
        var (ultima, _) = await cliente.Navegador.ConducirAsync(inicio, cliente.Usuario);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
        return (await cliente.Navegador.EntregarRegresoAsync(ultima.Url), claveAnterior);
    }

    [Fact]
    public async Task F004_CA21_CambiarClavePorElBff_VuelveConAvisoClaveActualizada()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();

        // Act
        var (regreso, _) = await CambiarClaveAsync(cliente);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, regreso.Estado);
        Assert.Equal("/mis-cuentas?aviso=clave-actualizada", regreso.Destino);
        Assert.NotNull(await cliente.Navegador.ObtenerSesionAsync());
    }

    [Fact]
    public async Task F004_CA21_TrasCambiarLaClave_ElSiguienteInicioSoloSirveConLaNueva()
    {
        // Arrange
        using var cliente = await ClienteConSesionAsync();
        var (_, claveAnterior) = await CambiarClaveAsync(cliente);
        await cliente.Navegador.PostAsync("/bff/salir");

        // Act
        var conLaAnterior = await cliente.Navegador.IniciarSesionAsync("clientes");
        var rechazo = await cliente.Navegador.EnviarAsync(conLaAnterior,
            ("username", cliente.Usuario.NombreDeUsuario), ("password", claveAnterior));
        var conLaNueva = await cliente.Navegador.IniciarSesionAsync("clientes");
        var (ultima, _) = await cliente.Navegador.ConducirAsync(conLaNueva, cliente.Usuario);

        // Assert
        Assert.Equal(MensajeGenerico, rechazo.MensajesDeError);
        Assert.Equal(TipoDePagina.CodigoEmitido, ultima.Tipo);
    }

    // ---- RN-25: la inactividad la mide la actividad deliberada ----

    [Fact]
    public async Task F004_RN25_PeticionesAutomaticasPorElBff_NoMantienenLaSesion()
    {
        // Arrange: peticiones a la API y a /bff/sesion cada 8 s durante 32 s, sin ningun latido.
        using var cliente = await ClienteConSesionAsync();
        await HacerPeticionesAutomaticasAsync(cliente.Navegador, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(32));
        await Task.Delay(TimeSpan.FromSeconds(10), Ct);

        // Act
        var respuesta = await cliente.Navegador.GetAsync("/bff/sesion");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.Estado);
        Assert.Equal("sesion-vencida-por-inactividad", NavegadorDelBff.CodigoDe(respuesta));
    }

    [Fact]
    public async Task F004_RN25_ActividadDeliberada_MantieneTambienLaSesionDeKeycloak()
    {
        // Arrange: un latido a los 25 s y otro a los 50 s: pasa la inactividad de Keycloak (40 s) sin que se cierre.
        using var cliente = await ClienteConSesionAsync();
        await Task.Delay(TimeSpan.FromSeconds(25), Ct);
        var primero = await cliente.Navegador.PostAsync("/bff/actividad");
        await Task.Delay(TimeSpan.FromSeconds(25), Ct);

        // Act
        var segundo = await cliente.Navegador.PostAsync("/bff/actividad");

        // Assert
        Assert.Equal(HttpStatusCode.OK, primero.Estado);
        Assert.Equal(HttpStatusCode.OK, segundo.Estado);
        Assert.Equal(1, await kc.Admin.SesionesAbiertasAsync(cliente.Usuario.Realm, cliente.Usuario.Sub));
    }

    private static async Task HacerPeticionesAutomaticasAsync(NavegadorDelBff navegador, TimeSpan cada, TimeSpan hasta)
    {
        for (var transcurrido = TimeSpan.Zero; transcurrido < hasta; transcurrido += cada)
        {
            await Task.Delay(cada, Ct);
            var datos = await navegador.GetAsync("/api/mis-cuentas");
            var consulta = await navegador.GetAsync("/bff/sesion");
            Assert.Equal(HttpStatusCode.OK, datos.Estado);
            Assert.Equal(HttpStatusCode.OK, consulta.Estado);
        }
    }
}
