using System.Text.Json;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.Sesiones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// ServicioDeSesion con un reloj falso y un IClienteDeIdentidad falso (plan, seccion 7, capa 1). Sin red ni Keycloak.
/// El access token de la sesion de partida vence a los 270 s.
/// </summary>
public class ServicioDeSesionTests
{
    private sealed class Escenario
    {
        public Escenario(AreaDeSesion area = AreaDeSesion.Clientes)
        {
            Reloj = new FakeTimeProvider(Construir.Origen);
            Identidad = new ClienteDeIdentidadFalso(Reloj);
            var opciones = Construir.OpcionesEnvueltas();
            Almacen = new AlmacenDeSesiones(Reloj, opciones);
            Sesion = Construir.Sesion(area, rol: area == AreaDeSesion.Clientes ? RolDeSesion.Cliente : RolDeSesion.Operador);
            Almacen.Agregar(Sesion);
            Servicio = new ServicioDeSesion(Almacen, Identidad, opciones, Reloj, NullLogger<ServicioDeSesion>.Instance);
        }

        public FakeTimeProvider Reloj { get; }

        public ClienteDeIdentidadFalso Identidad { get; }

        public AlmacenDeSesiones Almacen { get; }

        public SesionDelNavegador Sesion { get; }

        public ServicioDeSesion Servicio { get; }

        public string Clave => Sesion.Clave;

        public void Avanzar(TimeSpan tiempo) => Reloj.Advance(tiempo);
    }

    // ---- ConsultarAsync ----

    [Fact]
    public async Task F004_RN25_ConsultarSesion_NoCuentaComoActividad()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(2));

        // Act
        var dto = await e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(180, dto.SegundosHastaInactividad);
        Assert.Equal(Construir.Origen, e.Sesion.UltimaActividad);
        Assert.Equal(0, e.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_RN22_ConsultarSesion_ReportaLosPlazosEnSegundosTruncados()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMilliseconds(100_500));

        // Act
        var dto = await e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(199, dto.SegundosHastaInactividad);
        Assert.Equal(1699, dto.SegundosHastaVidaMaxima);
    }

    [Fact]
    public async Task F004_RF14_ConsultarSesion_DevuelveAreaUsuarioYRol()
    {
        // Arrange
        var e = new Escenario();

        // Act
        var dto = await e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal("clientes", dto.Area);
        Assert.Equal("cc1234567", dto.Usuario);
        Assert.Equal("Cliente", dto.Rol);
    }

    [Fact]
    public async Task F004_RNF01_SesionDto_NoContieneTokens()
    {
        // Arrange
        var e = new Escenario();

        // Act
        var json = JsonSerializer.Serialize(await e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.DoesNotContain(e.Sesion.Tokens.AccessToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(e.Sesion.Tokens.RefreshToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(e.Sesion.Tokens.IdToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave-que-no-existe")]
    public async Task F004_CL04_ConsultarSinSesion_LanzaSinSesion(string? clave)
    {
        // Arrange
        var e = new Escenario();

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.ConsultarAsync(clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.SinSesion, falla.Motivo);
    }

    [Fact]
    public async Task F004_CA07_ConsultarPasadaLaInactividad_LanzaInactividadYCierraEnKeycloak()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(5));

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.Inactividad, falla.Motivo);
        Assert.Equal([e.Sesion.Tokens.RefreshToken], e.Identidad.RefreshTokensCerrados);
        Assert.Null(e.Almacen.Obtener(e.Clave));
    }

    [Fact]
    public async Task F004_CA09_ConsultarPasadaLaVidaMaximaAunqueHayaActividad_LanzaVidaMaxima()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(29));
        e.Sesion.RegistrarActividad(e.Reloj.GetUtcNow());
        e.Avanzar(TimeSpan.FromMinutes(1));

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.ConsultarAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.VidaMaxima, falla.Motivo);
    }

    // ---- RegistrarActividadAsync (el latido) ----

    [Fact]
    public async Task F004_RN25_RegistrarActividad_RenuevaLosTokensYMueveLaUltimaActividad()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(4));

        // Act
        var dto = await e.Servicio.RegistrarActividadAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(1, e.Identidad.Renovaciones);
        Assert.Equal(e.Reloj.GetUtcNow(), e.Sesion.UltimaActividad);
        Assert.Equal(300, dto.SegundosHastaInactividad);
    }

    [Fact]
    public async Task F004_CL12_Renovacion_ReemplazaElAccessYElRefreshToken()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(1));

        // Act
        await e.Servicio.RegistrarActividadAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal("access-renovado-1", e.Sesion.Tokens.AccessToken);
        Assert.Equal("refresh-renovado-1", e.Sesion.Tokens.RefreshToken);
        Assert.Equal(e.Reloj.GetUtcNow() + TimeSpan.FromSeconds(270), e.Sesion.Tokens.AccessTokenVence);
    }

    [Fact]
    public async Task F004_CL12_RenovacionSinIdToken_ConservaElAnterior()
    {
        // Arrange
        var e = new Escenario();

        // Act
        await e.Servicio.RegistrarActividadAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal("id-1", e.Sesion.Tokens.IdToken);
    }

    [Fact]
    public async Task F004_RN25_RegistrarActividadConKeycloakSinRespuesta_RegistraLaActividadIgualmente()
    {
        // Arrange
        var e = new Escenario();
        e.Identidad.Modo = ModoDeIdentidad.NoResponde;
        e.Avanzar(TimeSpan.FromMinutes(4));

        // Act
        var dto = await e.Servicio.RegistrarActividadAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(300, dto.SegundosHastaInactividad);
        Assert.Equal(e.Reloj.GetUtcNow(), e.Sesion.UltimaActividad);
        Assert.Equal("access-1", e.Sesion.Tokens.AccessToken);
    }

    [Fact]
    public async Task F004_CL12_RenovacionRechazada_CierraComoRevocada()
    {
        // Arrange
        var e = new Escenario();
        e.Identidad.Modo = ModoDeIdentidad.Rechaza;

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.RegistrarActividadAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.Revocada, falla.Motivo);
        Assert.Null(e.Almacen.Obtener(e.Clave));
    }

    [Fact]
    public async Task F004_RN25_RegistrarActividadSinSesion_LanzaSinSesion()
    {
        // Arrange
        var e = new Escenario();

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.RegistrarActividadAsync(null, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.SinSesion, falla.Motivo);
    }

    // ---- ObtenerAccessTokenAsync (el proxy) ----

    [Fact]
    public async Task F004_RN25_ObtenerAccessToken_NuncaRegistraActividad()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromSeconds(250));

        // Act
        await e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(Construir.Origen, e.Sesion.UltimaActividad);
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConMasDe30SegundosRestantes_NoRenueva()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromSeconds(239));

        // Act
        var token = await e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal("access-1", token);
        Assert.Equal(0, e.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConExactamente30SegundosRestantes_NoRenueva()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromSeconds(240));

        // Act
        await e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(0, e.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConMenosDe30SegundosRestantes_RenuevaYDevuelveElNuevo()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromSeconds(241));

        // Act
        var token = await e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal("access-renovado-1", token);
        Assert.Equal(1, e.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConcurrente_RenuevaUnaSolaVez()
    {
        // Arrange
        var e = new Escenario();
        e.Identidad.Demora = TimeSpan.FromMilliseconds(100);
        e.Avanzar(TimeSpan.FromSeconds(245));

        // Act
        var tokens = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct)));

        // Assert
        Assert.Equal(1, e.Identidad.Renovaciones);
        Assert.All(tokens, token => Assert.Equal("access-renovado-1", token));
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConRenovacionRechazada_LanzaRevocada()
    {
        // Arrange
        var e = new Escenario();
        e.Identidad.Modo = ModoDeIdentidad.Rechaza;
        e.Avanzar(TimeSpan.FromSeconds(245));

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.Revocada, falla.Motivo);
        Assert.Null(e.Almacen.Obtener(e.Clave));
    }

    [Fact]
    public async Task F004_CL12_ObtenerAccessTokenConKeycloakSinRespuesta_LanzaYLaSesionSigue()
    {
        // Arrange
        var e = new Escenario();
        e.Identidad.Modo = ModoDeIdentidad.NoResponde;
        e.Avanzar(TimeSpan.FromSeconds(245));

        // Act
        await Assert.ThrowsAsync<IdentidadSinRespuestaException>(() => e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.NotNull(e.Almacen.Obtener(e.Clave));
    }

    [Fact]
    public async Task F004_CA07_ObtenerAccessTokenPasadaLaInactividad_LanzaYCierra()
    {
        // Arrange
        var e = new Escenario();
        e.Avanzar(TimeSpan.FromMinutes(5));

        // Act
        var falla = await Assert.ThrowsAsync<SesionTerminadaException>(() => e.Servicio.ObtenerAccessTokenAsync(e.Clave, Ayudas.Ct));

        // Assert
        Assert.Equal(MotivoDeCierre.Inactividad, falla.Motivo);
        Assert.Equal(1, e.Identidad.Cierres);
    }

    // ---- CerrarAsync ----

    [Fact]
    public async Task F004_RF14_Cerrar_CierraEnKeycloakConElRefreshTokenYBorraLaSesion()
    {
        // Arrange
        var e = new Escenario();

        // Act
        await e.Servicio.CerrarAsync(e.Clave, Ayudas.Ct);

        // Assert
        Assert.Equal(["refresh-1"], e.Identidad.RefreshTokensCerrados);
        Assert.Null(e.Almacen.Obtener(e.Clave));
    }

    [Fact]
    public async Task F004_RF14_Cerrar_EsIdempotente()
    {
        // Arrange
        var e = new Escenario();
        await e.Servicio.CerrarAsync(e.Clave, Ayudas.Ct);

        // Act
        await e.Servicio.CerrarAsync(e.Clave, Ayudas.Ct);
        await e.Servicio.CerrarAsync(null, Ayudas.Ct);

        // Assert
        Assert.Equal(1, e.Identidad.Cierres);
    }
}
