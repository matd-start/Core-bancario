using System.Collections.Concurrent;
using System.Net;
using System.Text;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// ClienteDeIdentidadKeycloak (T05) contra un HttpMessageHandler falso: lo que envia a Keycloak y como traduce sus
/// respuestas. Los endpoints salen del ConfigurationManager del esquema OIDC del area, que aqui es estatico.
/// </summary>
public class ClienteDeIdentidadKeycloakTests
{
    private const string EndpointDeToken = "http://keycloak:8080/realms/clientes/protocol/openid-connect/token";

    // Como lo publica Keycloak con KC_HOSTNAME + KC_HOSTNAME_BACKCHANNEL_DYNAMIC: el token por la direccion trasera,
    // pero el end_session con la del navegador, que dentro del contenedor del BFF es el propio BFF.
    private const string EndpointDeCierrePublicado = "http://localhost:8080/realms/clientes/protocol/openid-connect/logout";
    private const string EndpointDeCierreTrasero = "http://keycloak:8080/realms/clientes/protocol/openid-connect/logout";

    private sealed record Envio(HttpMethod Metodo, Uri Uri, string Cuerpo);

    private sealed class ManejadorFalso(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<Envio> _envios = new();

        public IReadOnlyList<Envio> Envios => [.. _envios];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            _envios.Enqueue(new Envio(request.Method, request.RequestUri!, cuerpo));
            return await responder(request, cancellationToken);
        }
    }

    private sealed record Escenario(ClienteDeIdentidadKeycloak Cliente, ManejadorFalso Manejador, FakeTimeProvider Reloj, ProveedorDeLogsEnMemoria Logs);

    private static Escenario Crear(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        string? metadatos = null, string tokenEndpoint = EndpointDeToken)
    {
        var configuracion = new OpenIdConnectConfiguration
        {
            Issuer = BffFixture.EmisorDeClientes,
            TokenEndpoint = tokenEndpoint,
            EndSessionEndpoint = EndpointDeCierrePublicado,
        };
        var oidc = new ServiceCollection()
            .AddOptions<OpenIdConnectOptions>("OidcClientes")
            .Configure(o =>
            {
                o.Configuration = configuracion;
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
            })
            .Services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();

        var opciones = Construir.Opciones();
        opciones.Clientes.Metadatos = metadatos;
        var manejador = new ManejadorFalso(responder);
        var reloj = new FakeTimeProvider(Construir.Origen);
        var logs = new ProveedorDeLogsEnMemoria();
        var fabricaDeLogs = LoggerFactory.Create(b => b.AddProvider(logs));
        var cliente = new ClienteDeIdentidadKeycloak(new HttpClient(manejador), oidc, Options.Create(opciones), reloj,
            fabricaDeLogs.CreateLogger<ClienteDeIdentidadKeycloak>());
        return new Escenario(cliente, manejador, reloj, logs);
    }

    private static Task<HttpResponseMessage> Json(HttpStatusCode estado, string json) =>
        Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    private const string RespuestaDeRenovacion =
        """{"access_token":"a2","refresh_token":"r2","id_token":"i2","expires_in":270,"token_type":"Bearer"}""";

    // ---- RenovarAsync ----

    [Fact]
    public async Task F004_RF14_RenovarAsync_EnviaGrantRefreshTokenConElSecretoDelArea()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.OK, RespuestaDeRenovacion));

        // Act
        await e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        var envio = Assert.Single(e.Manejador.Envios);
        Assert.Equal(HttpMethod.Post, envio.Metodo);
        Assert.Equal(EndpointDeToken, envio.Uri.ToString());
        var campos = System.Web.HttpUtility.ParseQueryString(envio.Cuerpo);
        Assert.Equal("refresh_token", campos["grant_type"]);
        Assert.Equal("core-bancario-web", campos["client_id"]);
        Assert.Equal("secreto-de-pruebas-clientes", campos["client_secret"]);
        Assert.Equal("r1", campos["refresh_token"]);
    }

    [Fact]
    public async Task F004_CL12_RenovarAsync_DevuelveLosTokensNuevosYCuandoVenceElAccess()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.OK, RespuestaDeRenovacion));

        // Act
        var tokens = await e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        Assert.Equal("a2", tokens.AccessToken);
        Assert.Equal("r2", tokens.RefreshToken);
        Assert.Equal("i2", tokens.IdToken);
        Assert.Equal(e.Reloj.GetUtcNow() + TimeSpan.FromSeconds(270), tokens.AccessTokenVence);
    }

    [Fact]
    public async Task F004_CL12_RenovarAsync_SinIdTokenEnLaRespuesta_DevuelveIdTokenNulo()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.OK, """{"access_token":"a2","refresh_token":"r2","expires_in":270}"""));

        // Act
        var tokens = await e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        Assert.Null(tokens.IdToken);
    }

    [Fact]
    public async Task F004_CL12_RenovarAsync_400InvalidGrant_LanzaRenovacionRechazada()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Session not active"}"""));

        // Act
        var renovar = () => e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        await Assert.ThrowsAsync<RenovacionRechazadaException>(renovar);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task F004_CL12_RenovarAsync_Respuesta5xx_LanzaIdentidadSinRespuesta(HttpStatusCode estado)
    {
        // Arrange
        var e = Crear((_, _) => Json(estado, "{}"));

        // Act
        var renovar = () => e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        await Assert.ThrowsAsync<IdentidadSinRespuestaException>(renovar);
    }

    [Fact]
    public async Task F004_CL12_RenovarAsync_ConErrorDeRed_LanzaIdentidadSinRespuesta()
    {
        // Arrange
        var e = Crear((_, _) => throw new HttpRequestException("sin red"));

        // Act
        var renovar = () => e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        await Assert.ThrowsAsync<IdentidadSinRespuestaException>(renovar);
    }

    // ---- CerrarSesionAsync ----

    [Fact]
    public async Task F004_RF14_CerrarSesionAsync_EnviaClienteSecretoYRefreshTokenAlEndSessionPorElCanalTrasero()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.NoContent, ""));

        // Act
        await e.Cliente.CerrarSesionAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        var envio = Assert.Single(e.Manejador.Envios);
        Assert.Equal(HttpMethod.Post, envio.Metodo);
        Assert.Equal(EndpointDeCierreTrasero, envio.Uri.ToString());
        var campos = System.Web.HttpUtility.ParseQueryString(envio.Cuerpo);
        Assert.Equal("core-bancario-web", campos["client_id"]);
        Assert.Equal("secreto-de-pruebas-clientes", campos["client_secret"]);
        Assert.Equal("r1", campos["refresh_token"]);
        Assert.Null(campos["id_token_hint"]);
    }

    [Fact]
    public async Task F004_RF14_CerrarSesionAsync_TokenEndpointSinRutaDeKeycloak_UsaElEndSessionPublicado()
    {
        // Arrange: termina en /token, pero no en la ruta de Keycloak, asi que no se puede derivar el cierre.
        var e = Crear((_, _) => Json(HttpStatusCode.NoContent, ""), tokenEndpoint: "http://keycloak:8080/oauth/token");

        // Act
        await e.Cliente.CerrarSesionAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        Assert.Equal(EndpointDeCierrePublicado, Assert.Single(e.Manejador.Envios).Uri.ToString());
    }

    [Fact]
    public async Task F004_RF14_CerrarSesionAsync_ConErrorDeRed_NoLanza()
    {
        // Arrange
        var e = Crear((_, _) => throw new HttpRequestException("sin red"));

        // Act
        var cerrar = () => e.Cliente.CerrarSesionAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        Assert.Null(await Record.ExceptionAsync(cerrar));
    }

    [Fact]
    public async Task F004_RF14_CerrarSesionAsync_ConRespuesta5xx_NoLanza()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.InternalServerError, "{}"));

        // Act
        var cerrar = () => e.Cliente.CerrarSesionAsync(AreaDeSesion.Clientes, "r1", Ayudas.Ct);

        // Assert
        Assert.Null(await Record.ExceptionAsync(cerrar));
    }

    // ---- EstaDisponibleAsync ----

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_ConRespuestaCorrecta_DevuelveTrue()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.OK, "{}"));

        // Act
        var disponible = await e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);

        // Assert
        Assert.True(disponible);
    }

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_ConRespuesta5xx_DevuelveFalse()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.ServiceUnavailable, "{}"));

        // Act
        var disponible = await e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);

        // Assert
        Assert.False(disponible);
    }

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_ConErrorDeRed_DevuelveFalseSinLanzar()
    {
        // Arrange
        var e = Crear((_, _) => throw new HttpRequestException("sin red"));

        // Act
        var disponible = await e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);

        // Assert
        Assert.False(disponible);
    }

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_SinMetadatos_PideElDescubrimientoDelEmisor()
    {
        // Arrange
        var e = Crear((_, _) => Json(HttpStatusCode.OK, "{}"));

        // Act
        await e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);

        // Assert
        var envio = Assert.Single(e.Manejador.Envios);
        Assert.Equal(HttpMethod.Get, envio.Metodo);
        Assert.Equal($"{BffFixture.EmisorDeClientes}/.well-known/openid-configuration", envio.Uri.ToString());
    }

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_ConMetadatos_PideElDocumentoDeMetadatos()
    {
        // Arrange
        const string metadatos = "http://keycloak:8080/realms/clientes/.well-known/openid-configuration";
        var e = Crear((_, _) => Json(HttpStatusCode.OK, "{}"), metadatos);

        // Act
        await e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);

        // Assert
        Assert.Equal(metadatos, Assert.Single(e.Manejador.Envios).Uri.ToString());
    }

    [Fact]
    public async Task F004_CL05_EstaDisponibleAsync_SinRespuestaEnTresSegundos_DevuelveFalse()
    {
        // Arrange: Keycloak "cuelga": la peticion no termina hasta que se cancela.
        var empezo = new TaskCompletionSource();
        var e = Crear(async (_, ct) =>
        {
            empezo.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act
        var consulta = e.Cliente.EstaDisponibleAsync(AreaDeSesion.Clientes, Ayudas.Ct);
        await empezo.Task.WaitAsync(TimeSpan.FromSeconds(5), Ayudas.Ct);
        e.Reloj.Advance(TimeSpan.FromSeconds(3));
        var disponible = await consulta.WaitAsync(TimeSpan.FromSeconds(10), Ayudas.Ct);

        // Assert
        Assert.False(disponible);
    }

    // ---- RNF-01 / RNF-04: nunca registra tokens ni cuerpos ----

    [Fact]
    public async Task F004_RNF04_ClienteDeIdentidad_NoRegistraTokensNiCuerpos()
    {
        // Arrange: fallos de renovacion y de cierre con un cuerpo que contiene un texto reconocible.
        var e = Crear((_, _) => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant","detalle":"CUERPO-SECRETO-DE-KEYCLOAK"}"""));

        // Act
        await Assert.ThrowsAsync<RenovacionRechazadaException>(() => e.Cliente.RenovarAsync(AreaDeSesion.Clientes, "refresh-secreto-uno", Ayudas.Ct));
        await e.Cliente.CerrarSesionAsync(AreaDeSesion.Clientes, "refresh-secreto-dos", Ayudas.Ct);

        // Assert
        var texto = string.Join("\n", e.Logs.Registros.Select(r => $"{r.Mensaje} {r.Excepcion}"));
        Assert.DoesNotContain("refresh-secreto-uno", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secreto-dos", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("CUERPO-SECRETO-DE-KEYCLOAK", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto-de-pruebas-clientes", texto, StringComparison.Ordinal);
    }
}
