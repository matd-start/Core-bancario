using CoreBancario.Bff.Configuracion;

namespace CoreBancario.Bff.Identidad;

/// <summary>
/// Puerto hacia los endpoints OIDC de Keycloak que usa el BFF fuera del flujo del navegador. Es una interfaz porque es
/// una frontera con un sistema externo y las pruebas del BFF necesitan provocar rechazos y silencios.
/// </summary>
public interface IClienteDeIdentidad
{
    /// <summary>POST token_endpoint: grant_type=refresh_token, client_id y client_secret del área.</summary>
    /// <exception cref="RenovacionRechazadaException"/>
    /// <exception cref="IdentidadSinRespuestaException"/>
    Task<TokensRenovados> RenovarAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken);

    /// <summary>POST al logout del realm por el canal trasero (derivado de token_endpoint) con client_id, client_secret y refresh_token. Nunca lanza.</summary>
    Task CerrarSesionAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken);

    /// <summary>GET al documento de descubrimiento del área con 3 s de espera máxima (CL-05). Nunca lanza.</summary>
    Task<bool> EstaDisponibleAsync(AreaDeSesion area, CancellationToken cancellationToken);
}
