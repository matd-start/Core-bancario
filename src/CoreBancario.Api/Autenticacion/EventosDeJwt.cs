// SDD: esqueleto creado por test-writer
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CoreBancario.Api.Autenticacion;

/// <summary>
/// Eventos compartidos por los dos esquemas: TokenValidated (sub obligatorio y rol según el esquema),
/// Challenge (401 no-autenticado, registra el motivo sin el token) y Forbidden (403 prohibido).
/// </summary>
public sealed class EventosDeJwt : JwtBearerEvents
{
    private readonly string _rolEnKeycloak;
    private readonly string _rolDeLaApi;

    public EventosDeJwt(string rolEnKeycloak, string rolDeLaApi)
    {
        _rolEnKeycloak = rolEnKeycloak;
        _rolDeLaApi = rolDeLaApi;
    }

    public override Task TokenValidated(TokenValidatedContext context) => throw new NotImplementedException();

    public override Task Challenge(JwtBearerChallengeContext context) => throw new NotImplementedException();

    public override Task Forbidden(ForbiddenContext context) => throw new NotImplementedException();
}
