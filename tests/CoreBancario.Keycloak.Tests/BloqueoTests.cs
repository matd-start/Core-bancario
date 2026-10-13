namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// RN-23 (5 intentos fallidos seguidos bloquean la credencial 15 min y luego se libera sola) y CL-03 (un codigo OTP
/// incorrecto cuenta como intento fallido). Con los tiempos bajados por el fixture: la espera es de 10 s, no de 15 min.
/// </summary>
public class BloqueoTests(KeycloakFixture kc)
{
    private async Task<UsuarioDePrueba> UsuarioConOtpInscritoAsync()
    {
        var (_, _, usuario) = await kc.CrearClienteConAccesoAsync();
        using var primerIngreso = kc.NuevoNavegador();
        await kc.IniciarSesionAsync(primerIngreso, usuario);
        return usuario;
    }

    /// <summary>Un intento de ingreso con esos datos que NO llega al OTP. Devuelve la pantalla resultante.</summary>
    private async Task<Pagina> IntentarClaveAsync(UsuarioDePrueba usuario, string clave)
    {
        using var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(usuario.Realm);
        return await navegador.EnviarAsync(login, ("username", usuario.NombreDeUsuario), ("password", clave));
    }

    /// <summary>Un ingreso completo con la clave y el codigo correctos, tal como lo haria el titular.</summary>
    private async Task<Pagina> IngresarConClaveYCodigoCorrectosAsync(UsuarioDePrueba usuario)
    {
        using var navegador = kc.NuevoNavegador();
        var login = await navegador.IniciarAsync(usuario.Realm);
        var (ultima, _) = await navegador.ConducirAsync(login, usuario);
        return ultima;
    }

    [Fact]
    public async Task F003_CA09_CincoFallos_ElSextoCorrectoSeRechazaYTrasLaEsperaEntra()
    {
        // Arrange
        var usuario = await UsuarioConOtpInscritoAsync();
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());

        // Act
        var sexto = await IngresarConClaveYCodigoCorrectosAsync(usuario);
        await Task.Delay(KeycloakFixture.EsperaDelBloqueoEnLasPruebas + TimeSpan.FromSeconds(3), Ayudas.Ct);
        var trasLaEspera = await IngresarConClaveYCodigoCorrectosAsync(usuario);

        // Assert
        Assert.NotEqual(TipoDePagina.CodigoEmitido, sexto.Tipo);
        Assert.True(sexto.TieneError);
        Assert.Equal(TipoDePagina.CodigoEmitido, trasLaEspera.Tipo);
    }

    [Fact]
    public async Task F003_CL03_CodigoOtpIncorrecto_CuentaComoIntentoFallido()
    {
        // Arrange: cuatro claves erroneas...
        var usuario = await UsuarioConOtpInscritoAsync();
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());
        await IntentarClaveAsync(usuario, Ayudas.ClaveValida());

        // Act: ...y el quinto fallo es un codigo OTP erroneo con la clave correcta.
        using (var navegador = kc.NuevoNavegador())
        {
            var login = await navegador.IniciarAsync(usuario.Realm);
            var pantallaDelOtp = await navegador.EnviarAsync(login, ("username", usuario.NombreDeUsuario), ("password", usuario.Clave));
            Assert.Equal(TipoDePagina.Otp, pantallaDelOtp.Tipo);
            var rechazado = await navegador.EnviarAsync(pantallaDelOtp, ("otp", usuario.Otp!.CodigoIncorrecto()));
            Assert.True(rechazado.TieneError);
        }

        var conTodoCorrecto = await IngresarConClaveYCodigoCorrectosAsync(usuario);

        // Assert: la credencial quedo bloqueada, asi que ni con todo correcto entra.
        Assert.NotEqual(TipoDePagina.CodigoEmitido, conTodoCorrecto.Tipo);
        Assert.True(conTodoCorrecto.TieneError);
    }
}
