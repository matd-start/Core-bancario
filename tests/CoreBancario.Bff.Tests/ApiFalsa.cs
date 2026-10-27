using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

namespace CoreBancario.Bff.Tests;

/// <summary>Una peticion que el proxy del BFF reenvio a la API: metodo, ruta con consulta, encabezados y cuerpo.</summary>
public sealed record PeticionALaApi(string Metodo, string RutaYConsulta, IReadOnlyDictionary<string, string[]> Encabezados, string Cuerpo)
{
    public string? Encabezado(string nombre) =>
        Encabezados.FirstOrDefault(e => string.Equals(e.Key, nombre, StringComparison.OrdinalIgnoreCase)).Value is { } valores
            ? string.Join(",", valores)
            : null;
}

/// <summary>
/// La API detras del proxy en las pruebas del BFF (plan, seccion 7): registra cada peticion reenviada y responde lo que
/// diga la prueba (por defecto 200 con "{}").
/// </summary>
public sealed class ApiFalsa
{
    private readonly ConcurrentQueue<PeticionALaApi> _peticiones = new();

    public IReadOnlyList<PeticionALaApi> Peticiones => [.. _peticiones];

    public HttpStatusCode Estado { get; set; } = HttpStatusCode.OK;

    public string Cuerpo { get; set; } = "{}";

    /// <summary>Encabezados de la respuesta (por ejemplo, un Set-Cookie que el BFF debe quitar).</summary>
    public List<(string Nombre, string Valor)> EncabezadosDeRespuesta { get; } = [];

    public HttpMessageInvoker CrearInvoker() => new(new Manejador(this));

    private sealed class Manejador(ApiFalsa api) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var encabezados = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray());
            if (request.Content is not null)
            {
                foreach (var encabezado in request.Content.Headers)
                    encabezados[encabezado.Key] = [.. encabezado.Value];
            }

            var cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            api._peticiones.Enqueue(new PeticionALaApi(request.Method.Method, request.RequestUri!.PathAndQuery, encabezados, cuerpo));

            var respuesta = new HttpResponseMessage(api.Estado)
            {
                Content = new StringContent(api.Cuerpo, Encoding.UTF8, "application/json"),
            };
            foreach (var (nombre, valor) in api.EncabezadosDeRespuesta)
                respuesta.Headers.TryAddWithoutValidation(nombre, valor);

            return respuesta;
        }
    }
}

/// <summary>Reemplaza a IForwarderHttpClientFactory de YARP: todo lo que reenvia el proxy va a la ApiFalsa.</summary>
public sealed class FabricaHaciaLaApiFalsa(ApiFalsa api) : IForwarderHttpClientFactory
{
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => api.CrearInvoker();
}
