using System.Net;
using System.Web;
using CoreBancario.Infrastructure.Identidad;
using Microsoft.Extensions.Options;
using static CoreBancario.Infrastructure.Tests.Identidad.ManejadorHttpDeGuion;

namespace CoreBancario.Infrastructure.Tests.Identidad;

/// <summary>
/// ADR-0014 (menor privilegio): la API usa la cuenta de servicio core-bancario-api-admin con client credentials. El
/// manejador pide el token de la cuenta de servicio y lo reutiliza hasta 30 s antes de que venza.
/// </summary>
public class TokenDeServicioHandlerTests
{
    private const string SecretoDePrueba = "secreto-de-servicio-de-pruebas";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _ahora = inicio;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
    }

    private sealed class FabricaDeClientesFalsa(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false);
    }

    /// <summary>Un Keycloak falso con el endpoint de token y una API que devuelve 200 a todo.</summary>
    private sealed class Escenario
    {
        public Escenario(int segundosDeVida = 300)
        {
            Token = new ManejadorHttpDeGuion(_ =>
                Json(HttpStatusCode.OK, $$"""{"access_token":"token-{{++_emitidos}}","expires_in":{{segundosDeVida}},"token_type":"Bearer"}"""));
            Api = new ManejadorHttpDeGuion(_ => Vacia(HttpStatusCode.OK));
            var opciones = Options.Create(new OpcionesDeKeycloak
            {
                UrlBase = new Uri("http://keycloak.test/"),
                Realm = "clientes",
                ClientId = "core-bancario-api-admin",
                ClientSecret = SecretoDePrueba,
            });
            var manejador = new TokenDeServicioHandler(new FabricaDeClientesFalsa(Token), opciones, Reloj) { InnerHandler = Api };
            Cliente = new HttpClient(manejador) { BaseAddress = new Uri("http://keycloak.test/") };
        }

        private int _emitidos;

        public ManejadorHttpDeGuion Token { get; }

        public ManejadorHttpDeGuion Api { get; }

        public RelojManual Reloj { get; } = new(new DateTimeOffset(2026, 10, 13, 15, 0, 0, TimeSpan.Zero));

        public HttpClient Cliente { get; }

        public Task LlamarALaApiAsync() => Cliente.GetAsync("/admin/realms/clientes/users", Ct);
    }

    [Fact]
    public async Task F003_ADR0014_TokenDeServicio_PideClientCredentialsConElClienteYElSecretoDeLasOpciones()
    {
        // Arrange
        var escenario = new Escenario();

        // Act
        await escenario.LlamarALaApiAsync();

        // Assert
        var peticion = Assert.Single(escenario.Token.Peticiones);
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal("/realms/clientes/protocol/openid-connect/token", peticion.Ruta);
        var formulario = HttpUtility.ParseQueryString(peticion.Cuerpo!);
        Assert.Equal("client_credentials", formulario["grant_type"]);
        Assert.Equal("core-bancario-api-admin", formulario["client_id"]);
        Assert.Equal(SecretoDePrueba, formulario["client_secret"]);
    }

    [Fact]
    public async Task F003_ADR0014_TokenDeServicio_LaPeticionALaApiLlevaElTokenComoBearer()
    {
        // Arrange
        var escenario = new Escenario();

        // Act
        await escenario.LlamarALaApiAsync();

        // Assert
        Assert.Equal("Bearer token-1", Assert.Single(escenario.Api.Peticiones).Autorizacion);
    }

    [Fact]
    public async Task F003_ADR0014_TokenDeServicio_SeReutilizaMientrasNoEstePorVencer()
    {
        // Arrange: el token dura 300 s; 200 s después todavía le quedan más de 30.
        var escenario = new Escenario(segundosDeVida: 300);
        await escenario.LlamarALaApiAsync();
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(200));

        // Act
        await escenario.LlamarALaApiAsync();

        // Assert
        Assert.Single(escenario.Token.Peticiones);
        Assert.Equal(2, escenario.Api.Peticiones.Count);
    }

    [Fact]
    public async Task F003_ADR0014_TokenDeServicio_SePideDeNuevoCuandoFaltanMenosDeTreintaSegundos()
    {
        // Arrange: a los 280 s del token de 300 s le quedan 20 s.
        var escenario = new Escenario(segundosDeVida: 300);
        await escenario.LlamarALaApiAsync();
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(280));

        // Act
        await escenario.LlamarALaApiAsync();

        // Assert
        Assert.Equal(2, escenario.Token.Peticiones.Count);
    }
}
