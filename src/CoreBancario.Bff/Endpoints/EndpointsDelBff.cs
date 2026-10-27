using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Errores;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CoreBancario.Bff.Endpoints;

/// <summary>Las cinco rutas /bff de la sección 3.1 del plan, con WithName y metadatos de respuesta. La clave sale de Items[Clave].</summary>
public static class EndpointsDelBff
{
    private const string AccionCambiarClave = "UPDATE_PASSWORD";

    public static IEndpointRouteBuilder MapEndpointsDelBff(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/bff");

        // Navegaciones (GET): las abre el navegador con un enlace, así que no pueden llevar el encabezado anti-CSRF.
        grupo.MapGet("/iniciar-sesion/{area}", IniciarSesionAsync)
            .WithName("IniciarSesion")
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapGet("/cambiar-clave", CambiarClaveAsync)
            .WithName("CambiarClave")
            .Produces(StatusCodes.Status302Found);

        // Consulta: no cuenta como actividad (RN-25).
        grupo.MapGet("/sesion", ObtenerSesionAsync)
            .WithName("ObtenerSesion")
            .Produces<SesionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Estas dos cambian el estado de la sesión: solo POST. Un GET responde 405 y no hace nada.
        grupo.MapPost("/actividad", RegistrarActividadAsync)
            .WithName("RegistrarActividad")
            .Produces<SesionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/salir", CerrarSesionAsync)
            .WithName("CerrarSesion")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    /// <summary>Orden de las comprobaciones: área válida, sesión de otra área, sesión de la misma área, Keycloak disponible, desafío.</summary>
    private static async Task<IResult> IniciarSesionAsync(string area, [FromQuery] string? regreso, HttpContext contexto,
        ServicioDeSesion servicio, IClienteDeIdentidad identidad)
    {
        if (!AreasDeSesion.TryParse(area, out var areaPedida))
            return Results.NotFound();

        var sesion = await SesionActualAsync(contexto, servicio);
        if (sesion is not null && sesion.Area != AreasDeSesion.Segmento(areaPedida))
            return Results.Redirect(Avisos.UrlDe(areaPedida, Avisos.OtraSesionAbierta));

        var destino = RutaDeRegreso.Normalizar(areaPedida, regreso);
        if (sesion is not null)
            return Results.Redirect(destino);

        if (!await identidad.EstaDisponibleAsync(areaPedida, contexto.RequestAborted))
            return Results.Redirect(Avisos.UrlDe(areaPedida, Avisos.IdentidadNoDisponible));

        // El destino viaja en el state protegido del flujo OIDC, nunca en una cookie legible ni en la URL de Keycloak (CL-08).
        var propiedades = new AuthenticationProperties { RedirectUri = destino };
        return Results.Challenge(propiedades, [AreasDeSesion.EsquemaOidc(areaPedida)]);
    }

    private static async Task<IResult> CambiarClaveAsync(HttpContext contexto, ServicioDeSesion servicio)
    {
        var sesion = await SesionActualAsync(contexto, servicio);
        if (sesion is null || sesion.Area != AreasDeSesion.Segmento(AreaDeSesion.Clientes))
            return Results.Redirect(AreasDeSesion.PrefijoDeLaSpa(AreaDeSesion.Clientes));

        var propiedades = new AuthenticationProperties { RedirectUri = AreasDeSesion.InicioDeLaSpa(AreaDeSesion.Clientes) };
        propiedades.Items[PropiedadesDeSesion.Accion] = AccionCambiarClave;
        return Results.Challenge(propiedades, [AreasDeSesion.EsquemaOidc(AreaDeSesion.Clientes)]);
    }

    private static Task<IResult> ObtenerSesionAsync(HttpContext contexto, ServicioDeSesion servicio) =>
        ConSesionAsync(contexto, async clave => TypedResults.Ok(await servicio.ConsultarAsync(clave, contexto.RequestAborted)));

    private static Task<IResult> RegistrarActividadAsync(HttpContext contexto, ServicioDeSesion servicio) =>
        ConSesionAsync(contexto, async clave => TypedResults.Ok(await servicio.RegistrarActividadAsync(clave, contexto.RequestAborted)));

    private static async Task<IResult> CerrarSesionAsync(HttpContext contexto, ServicioDeSesion servicio)
    {
        var clave = await PropiedadesDeSesion.LeerClaveAsync(contexto);
        await servicio.CerrarAsync(clave, contexto.RequestAborted);
        await contexto.SignOutAsync(ConfiguracionDeSesion.EsquemaDeCookie);
        return Results.NoContent();
    }

    /// <summary>La sesión de esta petición sin contar como actividad, o null si no hay o ya terminó.</summary>
    private static async Task<SesionDto?> SesionActualAsync(HttpContext contexto, ServicioDeSesion servicio)
    {
        var clave = await PropiedadesDeSesion.LeerClaveAsync(contexto);
        if (clave is null)
            return null;

        try
        {
            return await servicio.ConsultarAsync(clave, contexto.RequestAborted);
        }
        catch (SesionTerminadaException)
        {
            return null;
        }
    }

    /// <summary>Traduce la terminación de la sesión a 401 con su código.</summary>
    private static async Task<IResult> ConSesionAsync(HttpContext contexto, Func<string?, Task<IResult>> operacion)
    {
        try
        {
            return await operacion(await PropiedadesDeSesion.LeerClaveAsync(contexto));
        }
        catch (SesionTerminadaException ex)
        {
            return RespuestasDelBff.SesionTerminada(ex.Motivo);
        }
    }
}
