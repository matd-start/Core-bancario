using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Tests;

internal static class Ayudas
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sube desde el directorio de las pruebas hasta encontrar CoreBancario.slnx.</summary>
    public static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "CoreBancario.slnx")))
            directorio = directorio.Parent;

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontro CoreBancario.slnx subiendo desde el directorio de las pruebas.");
    }

    /// <summary>
    /// Un access token SIN FIRMA (alg none) con realm_access.roles: es lo que el BFF lee para el rol de la interfaz sin
    /// validarlo (plan 3.1). Cada llamada produce un token distinto.
    /// </summary>
    public static string CrearJwtSinFirma(IEnumerable<string> roles)
    {
        var encabezado = new JsonObject { ["alg"] = "none", ["typ"] = "JWT" };
        var cuerpo = new JsonObject
        {
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["realm_access"] = new JsonObject
            {
                ["roles"] = new JsonArray([.. roles.Select(r => (JsonNode)JsonValue.Create(r)!)]),
            },
        };
        return $"{Base64Url(encabezado.ToJsonString())}.{Base64Url(cuerpo.ToJsonString())}.";
    }

    public static string CrearJwtSinFirma(params string[] roles) => CrearJwtSinFirma((IEnumerable<string>)roles);

    private static string Base64Url(string texto) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>El destino de una redireccion como "/ruta?consulta", sea el Location absoluto o relativo.</summary>
    public static string Destino(HttpResponseMessage respuesta)
    {
        var ubicacion = respuesta.Headers.Location ?? throw new InvalidOperationException("La respuesta no trae Location.");
        return ubicacion.IsAbsoluteUri ? ubicacion.PathAndQuery : ubicacion.OriginalString;
    }

    public static string TextoDeLaCookie(HttpResponseMessage respuesta) =>
        respuesta.Headers.TryGetValues("Set-Cookie", out var cabeceras) ? string.Join(" | ", cabeceras) : "";
}

/// <summary>Datos de prueba de la sesion del BFF (sin Keycloak).</summary>
internal static class Construir
{
    public static readonly DateTimeOffset Origen = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    public static readonly TimeSpan VidaDelAccessToken = TimeSpan.FromSeconds(270);

    public static OpcionesDeArea OpcionesDeClientes() => new()
    {
        Emisor = "http://localhost:8080/realms/clientes",
        ClientId = "core-bancario-web",
        ClientSecret = "secreto-de-pruebas-clientes",
        Inactividad = TimeSpan.FromMinutes(5),
        VidaMaxima = TimeSpan.FromMinutes(30),
    };

    public static OpcionesDeArea OpcionesDeEmpleados() => new()
    {
        Emisor = "http://localhost:8080/realms/empleados",
        ClientId = "core-bancario-web",
        ClientSecret = "secreto-de-pruebas-empleados",
        Inactividad = TimeSpan.FromMinutes(15),
        VidaMaxima = TimeSpan.FromHours(9),
    };

    public static OpcionesDelBff Opciones() => new()
    {
        Clientes = OpcionesDeClientes(),
        Empleados = OpcionesDeEmpleados(),
    };

    public static IOptions<OpcionesDelBff> OpcionesEnvueltas() => Options.Create(Opciones());

    public static TokensDeLaSesion Tokens(DateTimeOffset vence, string sufijo = "1") =>
        new($"access-{sufijo}", $"refresh-{sufijo}", $"id-{sufijo}", vence);

    public static SesionDelNavegador Sesion(
        AreaDeSesion area = AreaDeSesion.Clientes,
        DateTimeOffset? inicio = null,
        DateTimeOffset? ultimaActividad = null,
        TokensDeLaSesion? tokens = null,
        RolDeSesion? rol = RolDeSesion.Cliente,
        string clave = "clave-1",
        string sid = "sid-1")
    {
        var desde = inicio ?? Origen;
        return new SesionDelNavegador(clave, area, "sub-1", "cc1234567", sid, rol, desde, ultimaActividad ?? desde,
            tokens ?? Tokens(desde + VidaDelAccessToken), new ClaimsPrincipal(new ClaimsIdentity("pruebas")));
    }

    /// <summary>El ticket que entrega el regreso OIDC a la cookie: tokens de SaveTokens, area y claims del usuario.</summary>
    public static AuthenticationTicket Ticket(
        string sub, string sid, string accessToken, string refreshToken, string idToken,
        DateTimeOffset inicio, DateTimeOffset vence, string area = "clientes")
    {
        var identidad = new ClaimsIdentity(
        [
            new Claim("sub", sub),
            new Claim("preferred_username", "cc1234567"),
            new Claim("sid", sid),
            new Claim("auth_time", inicio.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ], "pruebas", "preferred_username", "role");
        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Area] = area;
        propiedades.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "id_token", Value = idToken },
            new AuthenticationToken { Name = "expires_at", Value = vence.ToString("o", System.Globalization.CultureInfo.InvariantCulture) },
        ]);
        return new AuthenticationTicket(new ClaimsPrincipal(identidad), propiedades, ConfiguracionDeSesion.EsquemaDeCookie);
    }
}
