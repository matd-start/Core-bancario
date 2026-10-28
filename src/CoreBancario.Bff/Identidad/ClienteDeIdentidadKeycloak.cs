using System.Text.Json;
using CoreBancario.Bff.Configuracion;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CoreBancario.Bff.Identidad;

/// <summary>
/// Implementación con HttpClient tipado (10 s de espera). Los endpoints salen del ConfigurationManager del esquema OIDC
/// del área (respeta Metadatos). Nunca registra tokens ni cuerpos de respuesta.
/// </summary>
public sealed class ClienteDeIdentidadKeycloak(HttpClient http, IOptionsMonitor<OpenIdConnectOptions> oidc,
    IOptions<OpcionesDelBff> opciones, TimeProvider reloj, ILogger<ClienteDeIdentidadKeycloak> log) : IClienteDeIdentidad
{
    /// <summary>CL-05: más de esto sin respuesta y se avisa al usuario en vez de dejarlo esperando.</summary>
    private static readonly TimeSpan EsperaDelDescubrimiento = TimeSpan.FromSeconds(3);

    public async Task<TokensRenovados> RenovarAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken)
    {
        var configuracion = await ObtenerConfiguracionAsync(area, cancellationToken);
        var credenciales = opciones.Value.De(area);
        using var cuerpo = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = credenciales.ClientId,
            ["client_secret"] = credenciales.ClientSecret,
            ["refresh_token"] = refreshToken,
        });

        using var respuesta = await EnviarAsync(configuracion.TokenEndpoint, cuerpo, area, cancellationToken);
        var texto = await LeerCuerpoAsync(respuesta, area, cancellationToken);

        if (respuesta.IsSuccessStatusCode)
            return LeerTokens(texto, area);

        if ((int)respuesta.StatusCode == StatusCodes.Status400BadRequest && EsInvalidGrant(texto))
            throw new RenovacionRechazadaException();

        log.LogWarning("Keycloak ({Area}) rechazó la renovación con el estado {Estado}.", AreasDeSesion.Segmento(area), (int)respuesta.StatusCode);
        throw new IdentidadSinRespuestaException();
    }

    public async Task CerrarSesionAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var configuracion = await ObtenerConfiguracionAsync(area, cancellationToken);
            var credenciales = opciones.Value.De(area);
            using var cuerpo = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = credenciales.ClientId,
                ["client_secret"] = credenciales.ClientSecret,
                ["refresh_token"] = refreshToken,
            });

            using var respuesta = await EnviarAsync(DireccionDeCierre(configuracion), cuerpo, area, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
                log.LogWarning("Keycloak ({Area}) no cerró la sesión: estado {Estado}.", AreasDeSesion.Segmento(area), (int)respuesta.StatusCode);
        }
        catch (IdentidadSinRespuestaException)
        {
            // El cierre es de mejor esfuerzo: la sesión del BFF se borra igual y prompt=login protege el siguiente ingreso (ADR-0021).
            log.LogWarning("Keycloak ({Area}) no respondió al cerrar la sesión.", AreasDeSesion.Segmento(area));
        }
    }

    public async Task<bool> EstaDisponibleAsync(AreaDeSesion area, CancellationToken cancellationToken)
    {
        var opcionesDelArea = opciones.Value.De(area);
        var direccion = opcionesDelArea.Metadatos ?? $"{opcionesDelArea.Emisor.TrimEnd('/')}/.well-known/openid-configuration";

        // El tiempo de espera usa el TimeProvider inyectado: así la prueba lo cumple sin esperar 3 s reales.
        using var espera = new CancellationTokenSource(EsperaDelDescubrimiento, reloj);
        using var vinculado = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, espera.Token);
        try
        {
            using var respuesta = await http.GetAsync(direccion, HttpCompletionOption.ResponseHeadersRead, vinculado.Token);
            return respuesta.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            log.LogWarning("Keycloak ({Area}) no está disponible: {TipoDeError}.", AreasDeSesion.Segmento(area), ex.GetType().Name);
            return false;
        }
    }

    private async Task<OpenIdConnectConfiguration> ObtenerConfiguracionAsync(AreaDeSesion area, CancellationToken cancellationToken)
    {
        try
        {
            var manager = oidc.Get(AreasDeSesion.EsquemaOidc(area)).ConfigurationManager
                ?? throw new InvalidOperationException("El esquema OIDC no tiene ConfigurationManager.");
            return await manager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            log.LogWarning("No se pudo leer el descubrimiento OIDC de {Area}: {TipoDeError}.", AreasDeSesion.Segmento(area), ex.GetType().Name);
            throw new IdentidadSinRespuestaException(ex);
        }
    }

    /// <summary>
    /// Keycloak publica end_session_endpoint con la dirección del navegador (KC_HOSTNAME), porque es de front-channel;
    /// dentro de un contenedor esa dirección no llega a Keycloak. token_endpoint sí sale por la dirección trasera, y en
    /// Keycloak los dos comparten ruta (…/protocol/openid-connect/token y …/logout). Sin esa ruta, el publicado.
    /// </summary>
    private static string? DireccionDeCierre(OpenIdConnectConfiguration configuracion)
    {
        const string rutaDeToken = "/protocol/openid-connect/token";
        var token = configuracion.TokenEndpoint;
        return token is not null && token.EndsWith(rutaDeToken, StringComparison.Ordinal)
            ? $"{token[..^"token".Length]}logout"
            : configuracion.EndSessionEndpoint;
    }

    private async Task<HttpResponseMessage> EnviarAsync(string? direccion, HttpContent cuerpo, AreaDeSesion area,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(direccion))
            throw new IdentidadSinRespuestaException();

        try
        {
            return await http.PostAsync(direccion, cuerpo, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Red caída o tiempo de espera del HttpClient (no la cancelación de quien llama).
            log.LogWarning("Keycloak ({Area}) no respondió: {TipoDeError}.", AreasDeSesion.Segmento(area), ex.GetType().Name);
            throw new IdentidadSinRespuestaException(ex);
        }
    }

    private async Task<string> LeerCuerpoAsync(HttpResponseMessage respuesta, AreaDeSesion area, CancellationToken cancellationToken)
    {
        try
        {
            return await respuesta.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            log.LogWarning("Keycloak ({Area}) cortó la respuesta: {TipoDeError}.", AreasDeSesion.Segmento(area), ex.GetType().Name);
            throw new IdentidadSinRespuestaException(ex);
        }
    }

    private static bool EsInvalidGrant(string texto)
    {
        try
        {
            using var documento = JsonDocument.Parse(texto);
            return documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && error.GetString() == "invalid_grant";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private TokensRenovados LeerTokens(string texto, AreaDeSesion area)
    {
        try
        {
            using var documento = JsonDocument.Parse(texto);
            var raiz = documento.RootElement;
            var access = raiz.GetProperty("access_token").GetString();
            var refresh = raiz.GetProperty("refresh_token").GetString();
            var segundos = raiz.GetProperty("expires_in").GetInt32();
            var id = raiz.TryGetProperty("id_token", out var idToken) && idToken.ValueKind == JsonValueKind.String
                ? idToken.GetString()
                : null;

            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
                throw new JsonException("Faltan tokens en la respuesta.");

            return new TokensRenovados(access, refresh, id, reloj.GetUtcNow() + TimeSpan.FromSeconds(segundos));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            log.LogWarning("Keycloak ({Area}) devolvió una renovación ilegible.", AreasDeSesion.Segmento(area));
            throw new IdentidadSinRespuestaException(ex);
        }
    }
}
