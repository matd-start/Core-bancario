using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoreBancario.Api.Tests;

/// <summary>Quien firma el token. Desconocido es un emisor que la API no configuro (firma valida, "iss" ajeno).</summary>
public enum EmisorDeToken
{
    Clientes,
    Empleados,
    Desconocido,
}

/// <summary>
/// ADR-0018: las pruebas de la API firman sus propios tokens con claves RSA de prueba (una por realm) que el
/// ApiFixture inyecta como configuracion OIDC fija, asi que no se necesita Keycloak. Los tokens llevan lo mismo
/// que los de Keycloak: iss, aud, sub, preferred_username y los roles en realm_access.roles.
/// </summary>
public static class EmisorDePruebas
{
    public const string Audiencia = "core-bancario-api";
    public const string RolDeCliente = "cliente";
    public const string RolDeOperador = "operador";

    /// <summary>Vida del access token en los realms (RNF-01): 270 s.</summary>
    public static readonly TimeSpan VidaPorDefecto = TimeSpan.FromSeconds(270);

    private static readonly RsaSecurityKey ClaveDeClientes = NuevaClave("clave-clientes");
    private static readonly RsaSecurityKey ClaveDeEmpleados = NuevaClave("clave-empleados");

    public static string UrlDe(EmisorDeToken emisor) => emisor switch
    {
        EmisorDeToken.Clientes => "https://pruebas.local/realms/clientes",
        EmisorDeToken.Empleados => "https://pruebas.local/realms/empleados",
        _ => "https://emisor-ajeno.local/realms/clientes",
    };

    /// <summary>La clave publica que el ApiFixture registra como clave de firma de cada esquema.</summary>
    public static RsaSecurityKey ClaveDe(EmisorDeToken emisor) =>
        emisor == EmisorDeToken.Empleados ? ClaveDeEmpleados : ClaveDeClientes;

    /// <summary>Firma con otra clave RSA, desconocida para la API.</summary>
    public static SigningCredentials FirmaDeOtraClave() =>
        new(NuevaClave("clave-ajena"), SecurityAlgorithms.RsaSha256);

    /// <summary>Firma simetrica HS256: un algoritmo que la API no admite (ValidAlgorithms = RS256).</summary>
    public static SigningCredentials FirmaHs256() =>
        new(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), SecurityAlgorithms.HmacSha256);

    /// <param name="emisor">Fija "iss" y la clave con la que se firma (salvo que se pase firma).</param>
    /// <param name="sub">null omite el claim "sub" (un token sin sujeto).</param>
    /// <param name="roles">Roles de realm, que Keycloak pone en realm_access.roles.</param>
    /// <param name="vida">Por defecto 270 s, como en los realms.</param>
    /// <param name="emitido">Por defecto, ahora. Fija "iat" y "nbf"; "exp" = emitido + vida.</param>
    /// <param name="claimsExtra">Claims adicionales de primer nivel (por ejemplo, un claim de rol que un mapper del realm pudiera añadir).</param>
    /// <param name="audiencia">Por defecto, la de la API.</param>
    public static string CrearToken(EmisorDeToken emisor, string? sub, string[] roles, TimeSpan? vida = null,
        DateTimeOffset? emitido = null, string? audiencia = null, SigningCredentials? firma = null, IReadOnlyDictionary<string, object>? claimsExtra = null)
    {
        var inicio = (emitido ?? DateTimeOffset.UtcNow).UtcDateTime;
        var claims = new Dictionary<string, object>
        {
            ["preferred_username"] = "usuario-de-pruebas",
            ["realm_access"] = new Dictionary<string, object> { ["roles"] = roles },
        };
        if (sub is not null)
            claims["sub"] = sub;
        foreach (var extra in claimsExtra ?? new Dictionary<string, object>())
            claims[extra.Key] = extra.Value;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = UrlDe(emisor),
            Audience = audiencia ?? Audiencia,
            IssuedAt = inicio,
            NotBefore = inicio,
            Expires = inicio + (vida ?? VidaPorDefecto),
            Claims = claims,
            SigningCredentials = firma ?? new SigningCredentials(ClaveDe(emisor), SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Token de un operador (realm empleados, rol operador).</summary>
    public static string CrearTokenDeOperador(string? sub = null) =>
        CrearToken(EmisorDeToken.Empleados, sub ?? "operador-de-pruebas", [RolDeOperador]);

    /// <summary>Token de un cliente (realm clientes, rol cliente).</summary>
    public static string CrearTokenDeCliente(string sub) =>
        CrearToken(EmisorDeToken.Clientes, sub, [RolDeCliente]);

    private static RsaSecurityKey NuevaClave(string kid) => new(RSA.Create(2048)) { KeyId = kid };
}
