using System.Net;
using System.Net.Http.Json;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Sesiones;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// /bff/sesion, /bff/actividad y /bff/salir con el BFF real en memoria, el reloj falso y la identidad falsa. La sesion se
/// abre con POST /__pruebas/sesion (cookie y almacen reales, sin Keycloak).
/// </summary>
public class SesionHttpTests
{
    private static async Task<ProblemaDelBff> ProblemaAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<ProblemaDelBff>(Ayudas.Ct))!;

    private static async Task<SesionDto> SesionAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<SesionDto>(Ayudas.Ct))!;

    // ---- /bff/sesion ----

    [Fact]
    public async Task F004_CL04_ObtenerSesionSinCookie_Responde401SinSesion()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sin-sesion", (await ProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F004_RF14_ObtenerSesion_ConSesionDeCliente_DevuelveAreaUsuarioRolYPlazos()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes);

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(new SesionDto("clientes", "cc1234567", "Cliente", 300, 1800), await SesionAsync(respuesta));
    }

    [Fact]
    public async Task F004_CA02_ObtenerSesion_ConSesionDeOperador_DevuelveRolOperadorYLosPlazosDelBackoffice()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Empleados, usuario: "operador1");

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(new SesionDto("empleados", "operador1", "Operador", 900, 32400), await SesionAsync(respuesta));
    }

    [Fact]
    public async Task F004_CL04_ObtenerSesionSinElRolDelArea_DevuelveRolNuloConSesionValida()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync(AreaDeSesion.Clientes, roles: "operador");

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null((await SesionAsync(respuesta)).Rol);
    }

    [Fact]
    public async Task F004_RN25_ObtenerSesion_NoCuentaComoActividad()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromMinutes(2));
        using var primera = await bff.GetAsync("/bff/sesion", sesion);
        bff.Avanzar(TimeSpan.FromMinutes(2));

        // Act
        using var segunda = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(60, (await SesionAsync(segunda)).SegundosHastaInactividad);
    }

    [Fact]
    public async Task F004_RNF01_ObtenerSesion_ElCuerpoNoContieneTokens()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(Ayudas.Ct);

        // Assert
        Assert.DoesNotContain(sesion.AccessToken, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.RefreshToken, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.IdToken, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", cuerpo, StringComparison.Ordinal);
    }

    // ---- /bff/actividad: el latido ----

    [Fact]
    public async Task F004_RN25_RegistrarActividad_ReiniciaLaInactividadYRenuevaLosTokens()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromMinutes(4));

        // Act
        using var respuesta = await bff.PostAsync("/bff/actividad", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await SesionAsync(respuesta);
        Assert.Equal(300, dto.SegundosHastaInactividad);
        Assert.Equal(1560, dto.SegundosHastaVidaMaxima);
        Assert.Equal(1, bff.Identidad.Renovaciones);
    }

    [Fact]
    public async Task F004_RN25_RegistrarActividadSinSesion_Responde401()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.PostAsync("/bff/actividad");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sin-sesion", (await ProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F004_CL12_RegistrarActividadConRenovacionRechazada_Responde401SesionRevocada()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Identidad.Modo = ModoDeIdentidad.Rechaza;

        // Act
        using var respuesta = await bff.PostAsync("/bff/actividad", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-revocada", (await ProblemaAsync(respuesta)).Codigo);
    }

    // ---- CA-07: actividad deliberada frente a peticiones automaticas ----

    [Fact]
    public async Task F004_CA07_ActividadALosCuatroMinutos_SigueAbiertaALosSeis()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromMinutes(4));
        using var latido = await bff.PostAsync("/bff/actividad", sesion);
        bff.Avanzar(TimeSpan.FromMinutes(2));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task F004_CA07_SinActividadDeliberada_VenceAunqueHayaPeticionesAutomaticas()
    {
        // Arrange: una peticion a /api y otra a /bff/sesion cada 30 s durante 270 s, sin ningun latido.
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        await bff.HacerPeticionesAutomaticasAsync(sesion, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(270));
        bff.Avanzar(TimeSpan.FromSeconds(30));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-vencida-por-inactividad", (await ProblemaAsync(respuesta)).Codigo);
    }

    // ---- RNF-06: rechazo en el instante exacto ----

    [Fact]
    public async Task F004_RNF06_PeticionUnSegundoAntesDelLimite_SigueVigente()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(299));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(1, (await SesionAsync(respuesta)).SegundosHastaInactividad);
    }

    [Fact]
    public async Task F004_RNF06_PeticionEnElInstanteExactoDelLimite_Responde401()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromMinutes(5));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task F004_RNF06_PeticionUnSegundoDespuesDelLimite_Responde401()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(301));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-vencida-por-inactividad", (await ProblemaAsync(respuesta)).Codigo);
    }

    // ---- CA-09 / CL-07: vida maxima ----

    [Fact]
    public async Task F004_CA09_VidaMaxima_VenceAunqueHayaActividad()
    {
        // Arrange: un latido cada 4 minutos hasta el minuto 28, y la sesion llega a los 30 min exactos.
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        await bff.MantenerActividadAsync(sesion, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(28));
        bff.Avanzar(TimeSpan.FromMinutes(2));

        // Act
        using var respuesta = await bff.PostAsync("/bff/actividad", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("sesion-vencida-por-vida-maxima", (await ProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F004_CA09_UnSegundoAntesDeLaVidaMaxima_LaSesionSigueVigenteYQuedaUnSegundo()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        await bff.MantenerActividadAsync(sesion, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(28));
        bff.Avanzar(TimeSpan.FromSeconds(119));

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(1, (await SesionAsync(respuesta)).SegundosHastaVidaMaxima);
    }

    [Fact]
    public async Task F004_CL09_SesionVencida_CierraEnKeycloakPorElCanalTraseroYBorraLaSesion()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromMinutes(6));

        // Act
        using var vencida = await bff.GetAsync("/bff/sesion", sesion);
        using var despues = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal([sesion.RefreshToken], bff.Identidad.RefreshTokensCerrados);
        Assert.Equal("sin-sesion", (await ProblemaAsync(despues)).Codigo);
    }

    // ---- /bff/salir ----

    [Fact]
    public async Task F004_RF14_Salir_CierraLaSesionEnKeycloakYResponde204()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.PostAsync("/bff/salir", sesion);
        using var consulta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal([sesion.RefreshToken], bff.Identidad.RefreshTokensCerrados);
        Assert.Equal(HttpStatusCode.Unauthorized, consulta.StatusCode);
    }

    [Fact]
    public async Task F004_RF14_Salir_SinSesion_Responde204ComoOperacionIdempotente()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.PostAsync("/bff/salir");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(0, bff.Identidad.Cierres);
    }

    // ---- CL-11: nada de esto se guarda en cache ----

    [Fact]
    public async Task F004_CL11_Respuesta401DelBff_LlevaNoStoreYNosniff()
    {
        // Arrange
        await using var bff = new BffFixture();

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion");

        // Assert
        Assert.Contains("no-store", respuesta.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task F004_CL11_RespuestaVigenteDelBff_LlevaNoStoreYNosniff()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();

        // Act
        using var respuesta = await bff.GetAsync("/bff/sesion", sesion);

        // Assert
        Assert.Contains("no-store", respuesta.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
    }

    // ---- RNF-01 / RNF-04: los logs ----

    [Fact]
    public async Task F004_RNF01_FlujoDeSesion_LosLogsNoContienenTokens()
    {
        // Arrange
        await using var bff = new BffFixture();
        var sesion = await bff.AbrirSesionAsync();
        bff.Avanzar(TimeSpan.FromSeconds(245));

        // Act: consulta, dato por el proxy (que renueva) y cierre.
        using var consulta = await bff.GetAsync("/bff/sesion", sesion);
        using var datos = await bff.GetAsync("/api/mis-cuentas", sesion);
        using var salida = await bff.PostAsync("/bff/salir", sesion);

        // Assert
        var texto = string.Join("\n", bff.Logs.Registros.Select(r => $"{r.Mensaje} {r.Excepcion}"));
        Assert.DoesNotContain(sesion.AccessToken, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.RefreshToken, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.IdToken, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("access-renovado", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-renovado", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(sesion.Cookie.Split('=')[1], texto, StringComparison.Ordinal);
    }

    // ---- ADR-0020: la ruta de pruebas no existe fuera de las pruebas ----

    [Fact]
    public async Task F004_ADR0020_SinElFiltroDePruebas_NoExisteLaRutaDeSesionDePruebas()
    {
        // Arrange: el BFF "de produccion", sin los reemplazos de BffFixture.
        await using var fabrica = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<MarcadorDelBff>();
        using var cliente = fabrica.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Act
        using var respuesta = await cliente.PostAsync(FiltroDeSesionDePruebas.Ruta, null, Ayudas.Ct);

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(respuesta.Headers.Contains("Set-Cookie"));
    }
}
