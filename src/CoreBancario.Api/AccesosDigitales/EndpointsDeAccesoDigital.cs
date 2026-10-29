using System.Security.Claims;
using CoreBancario.Api.Autenticacion;
using CoreBancario.Api.OpenApi;
using CoreBancario.Application.AccesosDigitales;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.AccesosDigitales;

public static class EndpointsDeAccesoDigital
{
    /// <summary>
    /// Grupo con RequireAuthorization(Politicas.Operador) y DocumentarErroresComunes; RegistroDeOperacionDelOperador va en cada POST:
    /// GET /clientes/{clienteId:guid}/acceso-digital (RF-16) y POST /clientes/{clienteId:guid}/acceso-digital/habilitar y .../deshabilitar.
    /// El operador sale del token (usuario.Sub()), nunca de la ruta ni del cuerpo (RN-19).
    /// </summary>
    public static IEndpointRouteBuilder MapEndpointsDeAccesoDigital(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/clientes/{clienteId:guid}/acceso-digital")
            .RequireAuthorization(Politicas.Operador)
            .DocumentarErroresComunes();

        // Consulta de solo lectura: sin filtro de registro de operación (el GET no deja rastro de escritura).
        grupo.MapGet("/", async Task<Ok<EstadoDeAccesoDto>> (
                Guid clienteId, ObtenerEstadoDeAccesoHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.EjecutarAsync(new ObtenerEstadoDeAccesoConsulta(clienteId), cancellationToken)))
            .WithName("ObtenerEstadoDeAccesoDigital")
            .WithSummary("Consulta el estado del acceso digital de un cliente (SinAcceso, Habilitado o Deshabilitado).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/habilitar", async Task<Ok<AccesoHabilitadoDto>> (
                Guid clienteId, ClaimsPrincipal usuario, HttpContext http,
                HabilitarAccesoHandler handler, CancellationToken cancellationToken) =>
            {
                var acceso = await handler.EjecutarAsync(new HabilitarAccesoComando(clienteId, usuario.Sub()), cancellationToken);

                // La respuesta lleva la clave temporal: ni un proxy ni el navegador deben guardarla (RNF-03).
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.Ok(acceso);
            })
            .AddEndpointFilter<RegistroDeOperacionDelOperador>()
            .WithName("HabilitarAccesoDigital")
            .WithSummary("Habilita el acceso digital de un cliente (o lo restablece si ya lo tenía) y devuelve su clave temporal.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/deshabilitar", async Task<Ok<RegistroDeAccesoDto>> (
                Guid clienteId, ClaimsPrincipal usuario, DeshabilitarAccesoHandler handler, CancellationToken cancellationToken) =>
            {
                var registro = await handler.EjecutarAsync(new DeshabilitarAccesoComando(clienteId, usuario.Sub()), cancellationToken);
                return TypedResults.Ok(registro);
            })
            .AddEndpointFilter<RegistroDeOperacionDelOperador>()
            .WithName("DeshabilitarAccesoDigital")
            .WithSummary("Deshabilita el acceso digital de un cliente y cierra sus sesiones.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
