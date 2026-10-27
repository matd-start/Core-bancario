using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.InicioDeSesion;

public static class ConfiguracionDeSesion
{
    public const string EsquemaDeCookie = "Sesion";

    /// <summary>"__Host-cb-sesion" fuera de Development; "cb-sesion" en Development (HTTP).</summary>
    public static string NombreDeLaCookie(IHostEnvironment entorno)
    {
        ArgumentNullException.ThrowIfNull(entorno);
        return entorno.IsDevelopment() ? "cb-sesion" : "__Host-cb-sesion";
    }

    /// <summary>
    /// AddAuthentication(Sesion) + AddCookie(Sesion: HttpOnly, SameSite Strict, SecurePolicy Always fuera de Development,
    /// sin Expires, SlidingExpiration = false, SessionStore = AlmacenDeSesiones, OnRedirectToLogin/OnRedirectToAccessDenied
    /// → 401/403 sin redirigir) + dos AddOpenIdConnect (uno por área): Authority = Emisor, MetadataAddress = Metadatos,
    /// ClientId/ClientSecret, ResponseType code, ResponseMode query, UsePkce, SaveTokens, Scope solo "openid",
    /// MapInboundClaims = false, sin UserInfo, CallbackPath = AreasDeSesion.RutaDeRegresoOidc(area),
    /// PushedAuthorizationBehavior.Disable, ValidIssuer = Emisor, cookies de correlación y nonce HttpOnly y SameSite Lax,
    /// RequireHttpsMetadata fuera de Development, Events = EventosDeOidc. Registra AlmacenDeSesiones (singleton),
    /// ServicioDeSesion (scoped), IClienteDeIdentidad (AddHttpClient) y TryAddSingleton(TimeProvider.System).
    /// </summary>
    public static IServiceCollection AddSesionDelNavegador(this IServiceCollection services, IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentNullException.ThrowIfNull(entorno);

        // Las opciones se enlazan de forma perezosa (al resolverlas): el host arranca sin secretos, como pide la
        // generación del contrato OpenAPI al compilar (ADR-0019), y las pruebas fijan la configuración antes de la primera petición.
        services.Configure<OpcionesDelBff>(configuracion.GetSection(OpcionesDelBff.Seccion));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AlmacenDeSesiones>();
        services.AddScoped<ServicioDeSesion>();
        services.AddHttpClient<IClienteDeIdentidad, ClienteDeIdentidadKeycloak>(cliente => cliente.Timeout = TimeSpan.FromSeconds(10));

        services.AddAuthentication(EsquemaDeCookie)
            .AddCookie(EsquemaDeCookie, _ => { })
            .AddOpenIdConnect(AreasDeSesion.EsquemaOidc(AreaDeSesion.Clientes), _ => { })
            .AddOpenIdConnect(AreasDeSesion.EsquemaOidc(AreaDeSesion.Empleados), _ => { });

        ConfigurarCookie(services, entorno);
        ConfigurarOidc(services, AreaDeSesion.Clientes, entorno);
        ConfigurarOidc(services, AreaDeSesion.Empleados, entorno);

        return services;
    }

    private static void ConfigurarCookie(IServiceCollection services, IHostEnvironment entorno) =>
        services.AddOptions<CookieAuthenticationOptions>(EsquemaDeCookie).Configure<AlmacenDeSesiones>((cookie, almacen) =>
        {
            cookie.Cookie.Name = NombreDeLaCookie(entorno);
            cookie.Cookie.Path = "/";
            cookie.Cookie.HttpOnly = true;
            // Strict: ningún sitio externo puede hacer que el navegador envíe esta cookie (RNF-02).
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = PoliticaDeSeguridad(entorno);
            // Sin Expires: es una cookie de sesión del navegador. La vida de la sesión la decide el BFF (RN-22).
            cookie.SlidingExpiration = false;
            cookie.SessionStore = almacen;
            // Una petición sin sesión recibe 401/403, nunca una redirección a una página de login.
            cookie.Events.OnRedirectToLogin = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            cookie.Events.OnRedirectToAccessDenied = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

    private static void ConfigurarOidc(IServiceCollection services, AreaDeSesion area, IHostEnvironment entorno) =>
        services.AddOptions<OpenIdConnectOptions>(AreasDeSesion.EsquemaOidc(area)).Configure<IOptions<OpcionesDelBff>>((oidc, opciones) =>
        {
            var deLaArea = opciones.Value.De(area);

            oidc.SignInScheme = EsquemaDeCookie;
            oidc.SignOutScheme = EsquemaDeCookie;
            oidc.Authority = deLaArea.Emisor;
            // En compose el "iss" es el que ve el navegador, pero el descubrimiento se baja por la red interna (ADR-0020).
            if (deLaArea.Metadatos is not null)
                oidc.MetadataAddress = deLaArea.Metadatos;

            oidc.RequireHttpsMetadata = !entorno.IsDevelopment();
            oidc.ClientId = deLaArea.ClientId;
            oidc.ClientSecret = deLaArea.ClientSecret;
            oidc.ResponseType = "code";
            // query y sin PAR: el regreso es un GET simple y las pruebas pueden leer la petición de autorización (plan, decisión 7).
            oidc.ResponseMode = "query";
            oidc.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
            oidc.UsePkce = true;
            oidc.SaveTokens = true;
            oidc.GetClaimsFromUserInfoEndpoint = false;
            oidc.MapInboundClaims = false;
            oidc.Scope.Clear();
            oidc.Scope.Add("openid");
            oidc.CallbackPath = AreasDeSesion.RutaDeRegresoOidc(area);
            oidc.BackchannelTimeout = TimeSpan.FromSeconds(10);
            oidc.TokenValidationParameters.ValidIssuer = deLaArea.Emisor;
            oidc.TokenValidationParameters.NameClaimType = "preferred_username";

            // Lax: el regreso desde Keycloak es una navegación GET de nivel superior, que Lax sí deja pasar.
            foreach (var galleta in new[] { oidc.CorrelationCookie, oidc.NonceCookie })
            {
                galleta.HttpOnly = true;
                galleta.SameSite = SameSiteMode.Lax;
                galleta.SecurePolicy = PoliticaDeSeguridad(entorno);
            }

            oidc.Events = new EventosDeOidc(area);
        });

    private static CookieSecurePolicy PoliticaDeSeguridad(IHostEnvironment entorno) =>
        entorno.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
}
