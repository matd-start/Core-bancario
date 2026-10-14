using System.Net;
using System.Text;

namespace CoreBancario.Infrastructure.Tests.Identidad;

/// <summary>Una petición que llegó al manejador falso, con su cuerpo ya leído (el HttpClient lo desecha después).</summary>
internal sealed record PeticionCapturada(HttpMethod Metodo, string Ruta, string Consulta, string? Cuerpo, string? Autorizacion = null);

/// <summary>
/// Un servidor HTTP falso sin red (ADR-0018, nivel de integración de Infrastructure): guarda cada petición y responde
/// lo que diga el guion. El guion puede lanzar (HttpRequestException, TaskCanceledException) para simular fallos de red.
/// </summary>
internal sealed class ManejadorHttpDeGuion(Func<PeticionCapturada, HttpResponseMessage> guion) : HttpMessageHandler
{
    private readonly List<PeticionCapturada> _peticiones = [];

    public IReadOnlyList<PeticionCapturada> Peticiones => _peticiones;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var peticion = new PeticionCapturada(request.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, cuerpo, request.Headers.Authorization?.ToString());
        _peticiones.Add(peticion);
        return guion(peticion);
    }

    public static HttpResponseMessage Json(HttpStatusCode estado, string json) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Vacia(HttpStatusCode estado) => new(estado);
}
