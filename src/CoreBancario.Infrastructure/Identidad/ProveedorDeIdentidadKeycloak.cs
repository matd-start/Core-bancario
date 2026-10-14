using CoreBancario.Application.AccesosDigitales;
using Microsoft.Extensions.Options;

namespace CoreBancario.Infrastructure.Identidad;

/// <summary>
/// Cliente tipado de la Admin REST API (plan, sección 4, "Llamadas a Keycloak"). Traduce:
///   respuesta 2xx → éxito; GET de usuario con 404 → null;
///   cualquier otra respuesta → ErrorDelProveedorDeIdentidadException("HTTP {estado} al {paso}") (sin el cuerpo);
///   HttpRequestException sin respuesta, o TaskCanceledException por tiempo de espera (no por el token de quien
///   llama) → ProveedorDeIdentidadSinRespuestaException.
/// Compara nombres de usuario en minúsculas: Keycloak los guarda así.
/// </summary>
#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class ProveedorDeIdentidadKeycloak(HttpClient http, IOptions<OpcionesDeKeycloak> opciones) : IProveedorDeIdentidad
{
    public Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
