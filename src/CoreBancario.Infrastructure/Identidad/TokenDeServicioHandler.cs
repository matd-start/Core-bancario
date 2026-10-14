using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBancario.Application.AccesosDigitales;
using Microsoft.Extensions.Options;

namespace CoreBancario.Infrastructure.Identidad;

/// <summary>
/// DelegatingHandler: obtiene con client credentials el token de la cuenta de servicio y lo reutiliza hasta 30 s antes
/// de que venza. Si el endpoint de token responde con un error, lanza ErrorDelProveedorDeIdentidadException; si no
/// responde, deja pasar la HttpRequestException o la TaskCanceledException para que el adaptador las traduzca a
/// "sin respuesta" (ADR-0017).
/// </summary>
public sealed class TokenDeServicioHandler(IHttpClientFactory fabrica, IOptions<OpcionesDeKeycloak> opciones, TimeProvider reloj)
    : DelegatingHandler
{
    /// <summary>Nombre del HttpClient (sin este handler) con el que se pide el token, para no pedirlo a sí mismo.</summary>
    public const string NombreDelClienteDeToken = "keycloak-token";

    private static readonly TimeSpan MargenAntesDeVencer = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _candado = new(1, 1);
    private string? _token;
    private DateTimeOffset _renovarDesde;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await ObtenerTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> ObtenerTokenAsync(CancellationToken cancellationToken)
    {
        await _candado.WaitAsync(cancellationToken);
        try
        {
            if (_token is null || reloj.GetUtcNow() >= _renovarDesde)
                await PedirTokenAsync(cancellationToken);

            return _token!;
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task PedirTokenAsync(CancellationToken cancellationToken)
    {
        var configuracion = opciones.Value;
        var urlBase = configuracion.UrlBase
            ?? throw new InvalidOperationException($"Falta la configuración '{OpcionesDeKeycloak.Seccion}:UrlBase'.");
        var direccion = new Uri(urlBase, $"realms/{configuracion.Realm}/protocol/openid-connect/token");

        using var peticion = new HttpRequestMessage(HttpMethod.Post, direccion)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuracion.ClientId,
                ["client_secret"] = configuracion.ClientSecret,
            }),
        };

        using var cliente = fabrica.CreateClient(NombreDelClienteDeToken);
        using var respuesta = await cliente.SendAsync(peticion, cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            // Sin el cuerpo de la respuesta: puede traer datos internos.
            throw new ErrorDelProveedorDeIdentidadException(
                $"HTTP {(int)respuesta.StatusCode} al obtener el token de la cuenta de servicio");
        }

        string? token;
        int? segundosDeVida;
        try
        {
            var cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
            token = (string?)cuerpo?["access_token"];
            segundosDeVida = (int?)cuerpo?["expires_in"];
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        {
            throw new ErrorDelProveedorDeIdentidadException(
                "Respuesta ilegible al obtener el token de la cuenta de servicio", error);
        }

        if (string.IsNullOrEmpty(token) || segundosDeVida is null)
        {
            throw new ErrorDelProveedorDeIdentidadException(
                "Respuesta incompleta al obtener el token de la cuenta de servicio");
        }

        _token = token;
        _renovarDesde = reloj.GetUtcNow().AddSeconds(segundosDeVida.Value) - MargenAntesDeVencer;
    }
}
