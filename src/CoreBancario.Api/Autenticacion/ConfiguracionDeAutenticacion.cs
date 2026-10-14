using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoreBancario.Api.Autenticacion;

public static class ConfiguracionDeAutenticacion
{
    /// <summary>
    /// AddAuthentication(PorEmisor) + AddPolicyScheme(PorEmisor, ForwardDefaultSelector = SelectorDeEsquema.Elegir)
    /// + AddJwtBearer(Clientes) + AddJwtBearer(Empleados), políticas Cliente y Operador y FallbackPolicy autenticado.
    /// </summary>
    public static IServiceCollection AddAutenticacionConKeycloak(this IServiceCollection services,
        IConfiguration configuracion, IHostEnvironment entorno)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentNullException.ThrowIfNull(entorno);

        // La configuración se enlaza de forma perezosa (al resolver las opciones): así la API arranca aunque falte
        // la sección, y las pruebas pueden fijarla antes de la primera petición.
        services.Configure<OpcionesDeAutenticacion>(configuracion.GetSection(OpcionesDeAutenticacion.Seccion));

        services.AddAuthentication(EsquemasDeAutenticacion.PorEmisor)
            .AddPolicyScheme(EsquemasDeAutenticacion.PorEmisor, "Elige el esquema según el emisor del token", opciones =>
                opciones.ForwardDefaultSelector = contexto => SelectorDeEsquema.Elegir(
                    contexto, contexto.RequestServices.GetRequiredService<IOptions<OpcionesDeAutenticacion>>().Value))
            .AddJwtBearer(EsquemasDeAutenticacion.Clientes, _ => { })
            .AddJwtBearer(EsquemasDeAutenticacion.Empleados, _ => { });

        ConfigurarEsquema(services, EsquemasDeAutenticacion.Clientes, entorno,
            opciones => opciones.Clientes.Emisor, new EventosDeJwt(Roles.ClienteEnKeycloak, Roles.Cliente));
        ConfigurarEsquema(services, EsquemasDeAutenticacion.Empleados, entorno,
            opciones => opciones.Empleados.Emisor, new EventosDeJwt(Roles.OperadorEnKeycloak, Roles.Operador));

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Cliente, politica => politica.RequireAuthenticatedUser().RequireRole(Roles.Cliente))
            .AddPolicy(Politicas.Operador, politica => politica.RequireAuthenticatedUser().RequireRole(Roles.Operador))
            // Una ruta nueva sin política queda protegida por defecto, no abierta ("fallar de forma segura").
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static void ConfigurarEsquema(IServiceCollection services, string esquema, IHostEnvironment entorno,
        Func<OpcionesDeAutenticacion, string> emisorDe, EventosDeJwt eventos) =>
        services.AddOptions<JwtBearerOptions>(esquema).Configure<IOptions<OpcionesDeAutenticacion>>((jwt, opcionesDeAutenticacion) =>
        {
            var opciones = opcionesDeAutenticacion.Value;
            var emisor = emisorDe(opciones);

            jwt.Authority = emisor;
            jwt.RequireHttpsMetadata = !entorno.IsDevelopment();
            jwt.MapInboundClaims = false;
            jwt.IncludeErrorDetails = false;
            jwt.Events = eventos;
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = emisor,
                ValidAudience = opciones.Audiencia,
                ValidAlgorithms = [OpcionesDeAutenticacion.AlgoritmoPermitido],
                // Fija en el código y no configurable: junto con accessTokenLifespan = 270 s da la ventana de 5 min (RNF-01).
                ClockSkew = OpcionesDeAutenticacion.ToleranciaDeReloj,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                NameClaimType = "preferred_username",
            };
        });
}
