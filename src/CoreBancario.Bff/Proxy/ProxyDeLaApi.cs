namespace CoreBancario.Bff.Proxy;

/// <summary>/api/{**resto} hacia OpcionesDeLaApi.Url con YARP. Un paso previo obtiene el token o responde 401/503 sin reenviar.</summary>
public static class ProxyDeLaApi
{
    public const string Prefijo = "/api";

    // SDD: esqueleto creado por test-writer
    public static IEndpointRouteBuilder MapProxyDeLaApi(this IEndpointRouteBuilder app) => throw new NotImplementedException();
}
