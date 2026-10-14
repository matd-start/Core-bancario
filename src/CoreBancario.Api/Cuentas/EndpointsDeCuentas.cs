using CoreBancario.Api.Autenticacion;
using CoreBancario.Api.Errores;
using CoreBancario.Api.OpenApi;
using CoreBancario.Application.Cuentas;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.Cuentas;

public static class EndpointsDeCuentas
{
    /// <summary>
    /// Registra POST /clientes/{clienteId}/cuentas y las rutas de /cuentas. Los cambios de estado son acciones
    /// con nombre (POST …/bloquear) y no un PATCH con el estado deseado: la intención queda explícita.
    /// Todo el grupo es del Operador (RN-19) y las operaciones que cambian datos dejan el rastro del operador (RNF-05).
    /// </summary>
    public static IEndpointRouteBuilder MapEndpointsDeCuentas(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup(string.Empty)
            .RequireAuthorization(Politicas.Operador)
            .DocumentarErroresComunes();

        grupo.MapPost("/clientes/{clienteId:guid}/cuentas", async Task<Results<Created<CuentaDto>, ValidationProblem>> (
                Guid clienteId, AbrirCuentaRequest solicitud, AbrirCuentaHandler handler, CancellationToken cancellationToken) =>
            {
                var resultado = await handler.EjecutarAsync(new AbrirCuentaComando(clienteId, solicitud.Moneda), cancellationToken);

                return resultado.EsExito
                    ? TypedResults.Created($"/cuentas/{resultado.Valor.Id}", resultado.Valor)
                    : RespuestasDeError.DatosInvalidos(resultado.Errores);
            })
            .WithName("AbrirCuenta")
            .WithSummary("Abre una cuenta activa, con saldo cero y número generado, a un cliente existente.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter<RegistroDeOperacionDelOperador>();

        grupo.MapGet("/cuentas/{cuentaId:guid}", async Task<Ok<CuentaDto>> (
                Guid cuentaId, ObtenerCuentaHandler handler, CancellationToken cancellationToken) =>
            {
                var cuenta = await handler.EjecutarAsync(new ObtenerCuentaConsulta(cuentaId), cancellationToken);
                return TypedResults.Ok(cuenta);
            })
            .WithName("ObtenerCuenta")
            .WithSummary("Devuelve el detalle de una cuenta: número, moneda, estado, saldo y fechas.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapearCambioDeEstado(grupo, "bloquear", "BloquearCuenta", "Bloquea una cuenta activa.", AccionDeEstado.Bloquear);
        MapearCambioDeEstado(grupo, "desbloquear", "DesbloquearCuenta", "Desbloquea una cuenta bloqueada.", AccionDeEstado.Desbloquear);
        MapearCambioDeEstado(grupo, "cerrar", "CerrarCuenta", "Cierra una cuenta activa con saldo cero.", AccionDeEstado.Cerrar);

        return app;
    }

    private static void MapearCambioDeEstado(
        IEndpointRouteBuilder grupo, string accion, string nombreDeOperacion, string resumen, AccionDeEstado accionDeEstado)
    {
        grupo.MapPost($"/cuentas/{{cuentaId:guid}}/{accion}", async Task<Ok<CuentaDto>> (
                Guid cuentaId, CambiarEstadoDeCuentaHandler handler, CancellationToken cancellationToken) =>
            {
                var cuenta = await handler.EjecutarAsync(new CambiarEstadoDeCuentaComando(cuentaId, accionDeEstado), cancellationToken);
                return TypedResults.Ok(cuenta);
            })
            .WithName(nombreDeOperacion)
            .WithSummary(resumen)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter<RegistroDeOperacionDelOperador>();
    }
}
