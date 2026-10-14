using System.Security.Claims;
using CoreBancario.Api.Autenticacion;
using CoreBancario.Api.OpenApi;
using CoreBancario.Application.Cuentas;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.Cuentas;

public static class EndpointsDeMisCuentas
{
    /// <summary>
    /// Grupo /mis-cuentas con RequireAuthorization(Politicas.Cliente) y DocumentarErroresComunes. El titular sale del
    /// token (usuario.Sub()), nunca de la ruta ni del cuerpo (RN-19); el ClienteId no aparece en la URL.
    /// </summary>
    public static IEndpointRouteBuilder MapEndpointsDeMisCuentas(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var grupo = app.MapGroup("/mis-cuentas")
            .RequireAuthorization(Politicas.Cliente)
            .DocumentarErroresComunes();

        grupo.MapGet("", async Task<Ok<IReadOnlyList<CuentaDto>>> (
                ClaimsPrincipal usuario, ListarMisCuentasHandler handler, CancellationToken cancellationToken) =>
            {
                var cuentas = await handler.EjecutarAsync(new ListarMisCuentasConsulta(usuario.Sub()), cancellationToken);
                return TypedResults.Ok(cuentas);
            })
            .WithName("ListarMisCuentas")
            .WithSummary("Lista las cuentas del cliente autenticado, incluidas las cerradas.");

        grupo.MapGet("/{cuentaId:guid}", async Task<Ok<CuentaDto>> (
                Guid cuentaId, ClaimsPrincipal usuario, ObtenerMiCuentaHandler handler, CancellationToken cancellationToken) =>
            {
                var cuenta = await handler.EjecutarAsync(new ObtenerMiCuentaConsulta(cuentaId, usuario.Sub()), cancellationToken);
                return TypedResults.Ok(cuenta);
            })
            .WithName("ObtenerMiCuenta")
            .WithSummary("Devuelve el detalle de una cuenta del cliente autenticado; una cuenta ajena responde igual que una inexistente.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
