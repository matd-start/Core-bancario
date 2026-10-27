using CoreBancario.Bff.Configuracion;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Identidad;

/// <summary>
/// Implementación con HttpClient tipado (10 s de espera). Los endpoints salen del ConfigurationManager del esquema OIDC
/// del área (respeta Metadatos). Nunca registra tokens ni cuerpos de respuesta.
/// </summary>
public sealed class ClienteDeIdentidadKeycloak : IClienteDeIdentidad
{
    // SDD: esqueleto creado por test-writer
    public ClienteDeIdentidadKeycloak(HttpClient http, IOptionsMonitor<OpenIdConnectOptions> oidc,
        IOptions<OpcionesDelBff> opciones, TimeProvider reloj, ILogger<ClienteDeIdentidadKeycloak> log)
    {
    }

    public Task<TokensRenovados> RenovarAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task CerrarSesionAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<bool> EstaDisponibleAsync(AreaDeSesion area, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
