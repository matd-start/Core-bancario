using System.Globalization;
using System.Security.Claims;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using Microsoft.AspNetCore.Authentication;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// Sesiones sin Keycloak (plan, seccion 7): expone POST /__pruebas/sesion, que hace SignInAsync en el esquema de cookie
/// con un principal y tokens de prueba (un access token sin firma con realm_access). Asi se emite la cookie real con el
/// almacen real. Solo se registra en ConfigureTestServices: nunca existe fuera de las pruebas.
/// Parametros de la consulta: area (clientes|empleados), roles (separados por coma), usuario, vence (segundos de vida del
/// access token), inicioHace (segundos desde el inicio de sesion).
/// </summary>
public sealed class FiltroDeSesionDePruebas : IStartupFilter
{
    public const string Ruta = "/__pruebas/sesion";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (contexto, siguiente) =>
            {
                if (contexto.Request.Path == Ruta && HttpMethods.IsPost(contexto.Request.Method))
                    await IniciarSesionDePruebaAsync(contexto);
                else
                    await siguiente();
            });
            next(app);
        };

    private static async Task IniciarSesionDePruebaAsync(HttpContext contexto)
    {
        var consulta = contexto.Request.Query;
        var segmento = consulta["area"].FirstOrDefault() ?? "clientes";
        var area = segmento == "empleados" ? AreaDeSesion.Empleados : AreaDeSesion.Clientes;
        var roles = consulta.TryGetValue("roles", out var texto)
            ? texto.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
            : [area == AreaDeSesion.Clientes ? "cliente" : "operador"];
        var usuario = consulta["usuario"].FirstOrDefault() ?? "cc1234567";
        var vence = int.Parse(consulta["vence"].FirstOrDefault() ?? "270", CultureInfo.InvariantCulture);
        var inicioHace = int.Parse(consulta["inicioHace"].FirstOrDefault() ?? "0", CultureInfo.InvariantCulture);

        var reloj = contexto.RequestServices.GetRequiredService<TimeProvider>();
        var ahora = reloj.GetUtcNow();
        var inicio = ahora - TimeSpan.FromSeconds(inicioHace);
        var sub = $"sub-{Guid.NewGuid():N}";
        var accessToken = Ayudas.CrearJwtSinFirma(roles);
        var refreshToken = $"refresh-{Guid.NewGuid():N}";
        var idToken = $"id-{Guid.NewGuid():N}";

        var identidad = new ClaimsIdentity(
        [
            new Claim("sub", sub),
            new Claim("preferred_username", usuario),
            new Claim("sid", $"sid-{Guid.NewGuid():N}"),
            new Claim("auth_time", inicio.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        ], "pruebas", "preferred_username", "role");

        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Area] = (area == AreaDeSesion.Empleados ? "empleados" : "clientes");
        propiedades.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "id_token", Value = idToken },
            new AuthenticationToken
            {
                Name = "expires_at",
                Value = (ahora + TimeSpan.FromSeconds(vence)).ToString("o", CultureInfo.InvariantCulture),
            },
        ]);

        await contexto.SignInAsync(ConfiguracionDeSesion.EsquemaDeCookie, new ClaimsPrincipal(identidad), propiedades);
        await contexto.Response.WriteAsJsonAsync(new { accessToken, refreshToken, idToken, sub, usuario });
    }
}
