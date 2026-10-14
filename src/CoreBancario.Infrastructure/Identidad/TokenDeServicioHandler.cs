using Microsoft.Extensions.Options;

namespace CoreBancario.Infrastructure.Identidad;

/// <summary>DelegatingHandler: obtiene con client credentials el token de la cuenta de servicio y lo reutiliza hasta 30 s antes de que venza.</summary>
#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class TokenDeServicioHandler(IHttpClientFactory fabrica, IOptions<OpcionesDeKeycloak> opciones, TimeProvider reloj) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
