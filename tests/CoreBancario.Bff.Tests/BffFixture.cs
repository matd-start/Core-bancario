using System.Net;
using System.Net.Http.Json;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Yarp.ReverseProxy.Forwarder;

namespace CoreBancario.Bff.Tests;

/// <summary>La sesion que dejo POST /__pruebas/sesion: la cookie para reenviarla y los tokens que guarda el servidor.</summary>
public sealed record SesionDePrueba(string Cookie, string SetCookie, string AccessToken, string RefreshToken, string IdToken, string Usuario);

public sealed record RespuestaDeSesionDePrueba(string AccessToken, string RefreshToken, string IdToken, string Sub, string Usuario);

public sealed record ProblemaDelBff(int? Status, string? Title, string? Detail, string? Codigo, string? TraceId);

/// <summary>
/// El BFF real en memoria (WebApplicationFactory&lt;MarcadorDelBff&gt;) con reemplazos solo para las pruebas (plan,
/// seccion 7): TimeProvider -> FakeTimeProvider, IClienteDeIdentidad -> ClienteDeIdentidadFalso, el cliente HTTP del proxy ->
/// ApiFalsa y la ruta /__pruebas/sesion. Una instancia por prueba (su reloj, su identidad y su API son suyos); el host se
/// crea en el primer uso, asi las pruebas puras no lo necesitan.
/// </summary>
public sealed class BffFixture : IAsyncDisposable
{
    public const string EmisorDeClientes = "http://localhost:8080/realms/clientes";
    public const string EmisorDeEmpleados = "http://localhost:8080/realms/empleados";

    private readonly Lazy<WebApplicationFactory<MarcadorDelBff>> _fabrica;
    private HttpClient? _cliente;

    public BffFixture()
    {
        Identidad = new ClienteDeIdentidadFalso(Reloj);
        _fabrica = new Lazy<WebApplicationFactory<MarcadorDelBff>>(CrearFabrica);
    }

    public FakeTimeProvider Reloj { get; } = new(DateTimeOffset.UtcNow);

    public ClienteDeIdentidadFalso Identidad { get; }

    public ApiFalsa Api { get; } = new();

    public ProveedorDeLogsEnMemoria Logs { get; } = new();

    public HttpClient Cliente =>
        _cliente ??= _fabrica.Value.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    /// <summary>Los servicios del host real (singletons como AlmacenDeSesiones). Crea el host en el primer uso.</summary>
    public IServiceProvider Servicios => _fabrica.Value.Services;

    public void Avanzar(TimeSpan tiempo) => Reloj.Advance(tiempo);

    private WebApplicationFactory<MarcadorDelBff> CrearFabrica() =>
        new WebApplicationFactory<MarcadorDelBff>().WithWebHostBuilder(constructor =>
        {
            constructor.UseEnvironment("Development");
            constructor.UseSetting("Bff:Api:Url", "http://api-falsa");
            constructor.UseSetting("Bff:Clientes:Emisor", EmisorDeClientes);
            constructor.UseSetting("Bff:Clientes:ClientId", "core-bancario-web");
            constructor.UseSetting("Bff:Clientes:ClientSecret", "secreto-de-pruebas-clientes");
            constructor.UseSetting("Bff:Clientes:Inactividad", "00:05:00");
            constructor.UseSetting("Bff:Clientes:VidaMaxima", "00:30:00");
            constructor.UseSetting("Bff:Empleados:Emisor", EmisorDeEmpleados);
            constructor.UseSetting("Bff:Empleados:ClientId", "core-bancario-web");
            constructor.UseSetting("Bff:Empleados:ClientSecret", "secreto-de-pruebas-empleados");
            constructor.UseSetting("Bff:Empleados:Inactividad", "00:15:00");
            constructor.UseSetting("Bff:Empleados:VidaMaxima", "09:00:00");
            constructor.ConfigureLogging(registro => registro.AddProvider(Logs));
            constructor.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<TimeProvider>();
                servicios.AddSingleton<TimeProvider>(Reloj);
                servicios.RemoveAll<IClienteDeIdentidad>();
                servicios.AddSingleton<IClienteDeIdentidad>(Identidad);
                servicios.RemoveAll<IForwarderHttpClientFactory>();
                servicios.AddSingleton<IForwarderHttpClientFactory>(new FabricaHaciaLaApiFalsa(Api));
                servicios.AddTransient<IStartupFilter, FiltroDeSesionDePruebas>();
            });
        });

    /// <summary>Abre una sesion real (cookie y almacen reales) sin pasar por Keycloak.</summary>
    public async Task<SesionDePrueba> AbrirSesionAsync(AreaDeSesion area = AreaDeSesion.Clientes, string? roles = null,
        int vidaDelAccessTokenEnSegundos = 270, string usuario = "cc1234567", int inicioHaceSegundos = 0)
    {
        var segmento = area == AreaDeSesion.Empleados ? "empleados" : "clientes";
        var consulta = $"area={segmento}&vence={vidaDelAccessTokenEnSegundos}&usuario={usuario}&inicioHace={inicioHaceSegundos}";
        if (roles is not null)
            consulta += $"&roles={roles}";

        using var respuesta = await Cliente.PostAsync($"{FiltroDeSesionDePruebas.Ruta}?{consulta}", null, Ayudas.Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var setCookie = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.Contains("cb-sesion", StringComparison.Ordinal));
        var datos = (await respuesta.Content.ReadFromJsonAsync<RespuestaDeSesionDePrueba>(Ayudas.Ct))!;
        return new SesionDePrueba(setCookie.Split(';')[0], setCookie, datos.AccessToken, datos.RefreshToken, datos.IdToken, datos.Usuario);
    }

    /// <summary>Una peticion como la haria la SPA: con X-CSRF: 1 y Sec-Fetch-Site: same-origin salvo que se diga otra cosa.</summary>
    public async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, SesionDePrueba? sesion = null,
        bool conEncabezadoCsrf = true, string? secFetchSite = "same-origin", HttpContent? contenido = null,
        string? authorization = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = contenido };
        if (sesion is not null)
            solicitud.Headers.Add("Cookie", sesion.Cookie);
        if (conEncabezadoCsrf)
            solicitud.Headers.Add("X-CSRF", "1");
        if (secFetchSite is not null)
            solicitud.Headers.Add("Sec-Fetch-Site", secFetchSite);
        if (authorization is not null)
            solicitud.Headers.Add("Authorization", authorization);

        return await Cliente.SendAsync(solicitud, Ayudas.Ct);
    }

    public Task<HttpResponseMessage> GetAsync(string ruta, SesionDePrueba? sesion = null, bool conEncabezadoCsrf = true) =>
        EnviarAsync(HttpMethod.Get, ruta, sesion, conEncabezadoCsrf);

    public Task<HttpResponseMessage> PostAsync(string ruta, SesionDePrueba? sesion = null, bool conEncabezadoCsrf = true) =>
        EnviarAsync(HttpMethod.Post, ruta, sesion, conEncabezadoCsrf);

    /// <summary>Un latido cada "cada" hasta cumplir "hasta" desde ahora: la actividad deliberada de la SPA.</summary>
    public async Task MantenerActividadAsync(SesionDePrueba sesion, TimeSpan cada, TimeSpan hasta)
    {
        for (var transcurrido = TimeSpan.Zero; transcurrido < hasta; transcurrido += cada)
        {
            Avanzar(cada);
            using var respuesta = await PostAsync("/bff/actividad", sesion);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }

    /// <summary>Peticiones que la app hace sola (recargas, renovacion en segundo plano): nunca cuentan como actividad (RN-25).</summary>
    public async Task HacerPeticionesAutomaticasAsync(SesionDePrueba sesion, TimeSpan cada, TimeSpan hasta)
    {
        for (var transcurrido = TimeSpan.Zero; transcurrido < hasta; transcurrido += cada)
        {
            Avanzar(cada);
            using var datos = await GetAsync("/api/mis-cuentas", sesion);
            using var consulta = await GetAsync("/bff/sesion", sesion);
            Assert.Equal(HttpStatusCode.OK, datos.StatusCode);
            Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cliente?.Dispose();
        if (_fabrica.IsValueCreated)
            await _fabrica.Value.DisposeAsync();
    }
}
