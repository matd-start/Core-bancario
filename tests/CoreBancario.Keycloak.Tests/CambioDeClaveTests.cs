namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RF-15 (cambiar la clave) y CL-06 (la clave nueva debe cumplir la politica): la politica la aplica el servidor
/// (length(12), maxLength(64), notUsername, notContainsUsername); el numero de documento suelto lo avisa el navegador
/// (documento.js) y ese aviso se puede saltar, asi que aqui se documenta el hueco.
/// </summary>
public class CambioDeClaveTests(KeycloakFixture kc)
{
    public static TheoryData<string, string, bool> CasosDeNumeroDeDocumento => new()
    {
        { "cc1234567", "Abc1234567xyz!", true },
        { "CC1234567", "xx1234567xx", true },
        { "pa7654321", "ABC7654321def", true },
        { "cc1234567", "OtraClaveLarga!", false },
        { "cc1234567", "Abc123456xyz!", false },
        { "cc1234567", "CC-solo-letras", false },
    };

    [Theory]
    [MemberData(nameof(CasosDeNumeroDeDocumento))]
    public void F003_CL06_ContieneNumeroDeDocumento_DetectaElNumeroSuelto(string usuario, string clave, bool esperado)
    {
        // Arrange / Act
        var contiene = ScriptDelTema.ContieneNumeroDeDocumento(usuario, clave);

        // Assert
        Assert.Equal(esperado, contiene);
    }

    /// <summary>Llega a la pantalla de cambio de clave de un cliente nuevo (clave temporal), sin enviarla.</summary>
    private async Task<(UsuarioDePrueba Usuario, NavegadorSimulado Navegador, Pagina Pantalla)> LlegarAlCambioDeClaveAsync()
    {
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(usuario.Realm);
        var cambio = await navegador.AvanzarHastaAsync(login, usuario, TipoDePagina.CambioDeClave);
        return (usuario, navegador, cambio);
    }

    [Fact]
    public async Task F003_CL06_PaginaDeCambioDeClave_CargaElScriptYTraeElUsuario()
    {
        // Arrange / Act
        var (usuario, navegador, cambio) = await LlegarAlCambioDeClaveAsync();
        using var _ = navegador;

        // Assert: documento.js lee el usuario de la propia pantalla (campo username oculto o atributo data-*).
        Assert.True(cambio.CargaElScript("documento.js"), "La pantalla de cambio de clave no carga documento.js.");
        Assert.True(cambio.TraeElUsuario(usuario.NombreDeUsuario), "La pantalla de cambio de clave no trae el usuario.");
    }

    [Fact]
    public async Task F003_CL06_ClaveNuevaConElUsuarioCompleto_ElServidorLaRechaza()
    {
        // Arrange
        var (usuario, navegador, cambio) = await LlegarAlCambioDeClaveAsync();
        using var _ = navegador;
        var clave = $"Segura-{usuario.NombreDeUsuario}-zq";

        // Act
        var respuesta = await navegador.EnviarAsync(cambio, ("password-new", clave), ("password-confirm", clave));

        // Assert
        Assert.Equal(TipoDePagina.CambioDeClave, respuesta.Tipo);
        Assert.True(respuesta.TieneError, "Keycloak acepto una clave que contiene el usuario completo.");
    }

    [Fact]
    public async Task F003_CL06_ClaveNuevaConSoloElNumero_ElServidorLaAcepta()
    {
        // Arrange: documenta el hueco aceptado (decision del autor): solo el navegador avisa del numero suelto, y ese
        // aviso se puede saltar. Si algun dia el servidor la rechaza, esta prueba avisa de que el limite cambio.
        var (usuario, navegador, cambio) = await LlegarAlCambioDeClaveAsync();
        using var _ = navegador;
        var clave = $"Segura-{usuario.NombreDeUsuario[2..]}-zq";

        // Act
        var respuesta = await navegador.EnviarAsync(cambio, ("password-new", clave), ("password-confirm", clave));

        // Assert
        Assert.False(respuesta.TieneError, respuesta.MensajesDeError);
        Assert.NotEqual(TipoDePagina.CambioDeClave, respuesta.Tipo);
    }

    [Fact]
    public async Task F003_CA13_ClaveNuevaDemasiadoCorta_SeRechazaIndicandoLaRegla()
    {
        // Arrange
        var (_, navegador, cambio) = await LlegarAlCambioDeClaveAsync();
        using var _ = navegador;
        const string corta = "Corta-1";

        // Act
        var respuesta = await navegador.EnviarAsync(cambio, ("password-new", corta), ("password-confirm", corta));

        // Assert: el mensaje de Keycloak nombra la regla rota (longitud minima 12).
        Assert.Equal(TipoDePagina.CambioDeClave, respuesta.Tipo);
        Assert.Contains("12", respuesta.MensajesDeError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F003_CA13_CambiarClave_PideLaActualYDespuesSoloFuncionaLaNueva()
    {
        // Arrange: un cliente con sesion (ya cambio la clave temporal e inscribio su OTP).
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        using var navegador = kc.NuevoNavegador();
        await kc.IniciarSesionAsync(navegador, usuario);
        var claveAnterior = usuario.Clave;
        usuario.ClaveNueva = Ayudas.ClaveValida();

        // Act: la SPA pide cambiar la clave (kc_action=UPDATE_PASSWORD, max_auth_age = 0 en el realm). Keycloak compara la
        // edad de la autenticacion en segundos: se deja pasar un poco de tiempo, como le pasaria a una persona.
        await Task.Delay(TimeSpan.FromSeconds(2), Ayudas.Ct);
        var inicio = await navegador.IniciarAsync(usuario.Realm, accionDeLaAplicacion: "UPDATE_PASSWORD");
        var (final, recorrido) = await navegador.ConducirAsync(inicio, usuario);

        // Assert: primero pide la clave actual (una pantalla de clave), despues el cambio, y termina bien.
        Assert.True(recorrido[0] is TipoDePagina.Clave or TipoDePagina.Login, $"Primera pantalla: {recorrido[0]}");
        Assert.Contains(TipoDePagina.CambioDeClave, recorrido);
        Assert.Equal(TipoDePagina.CodigoEmitido, final.Tipo);

        // Assert (siguiente inicio de sesion): la anterior ya no sirve y la nueva si.
        var conLaAnterior = await IntentarClaveAsync(usuario, claveAnterior);
        using var otroNavegador = kc.NuevoNavegador();
        var (tokens, _) = await kc.IniciarSesionAsync(otroNavegador, usuario);
        Assert.True(conLaAnterior.TieneError);
        Assert.NotEmpty(tokens.AccessToken);
    }

    private async Task<Pagina> IntentarClaveAsync(UsuarioDePrueba usuario, string clave)
    {
        using var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(usuario.Realm);
        return await navegador.EnviarAsync(login, ("username", usuario.NombreDeUsuario), ("password", clave));
    }
}
