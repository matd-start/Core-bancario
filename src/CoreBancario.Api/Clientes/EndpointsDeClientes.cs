using CoreBancario.Api.Errores;
using CoreBancario.Application.Clientes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CoreBancario.Api.Clientes;

public static class EndpointsDeClientes
{
    /// <summary>
    /// Registra las tres rutas de /clientes con WithName, WithSummary y metadatos de respuesta (RNF-05).
    /// Los endpoints solo traducen: arman el comando, llaman al handler y convierten el resultado en HTTP.
    /// </summary>
    public static IEndpointRouteBuilder MapEndpointsDeClientes(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/clientes", async Task<Results<Created<ClienteDto>, ValidationProblem>> (
                RegistrarClienteRequest solicitud, RegistrarClienteHandler handler, CancellationToken cancellationToken) =>
            {
                var comando = new RegistrarClienteComando(
                    solicitud.TipoDocumento, solicitud.NumeroDocumento, solicitud.Nombres,
                    solicitud.Apellidos, solicitud.Correo, solicitud.Telefono);

                var resultado = await handler.EjecutarAsync(comando, cancellationToken);

                return resultado.EsExito
                    ? TypedResults.Created($"/clientes/{resultado.Valor.Id}", resultado.Valor)
                    : RespuestasDeError.DatosInvalidos(resultado.Errores);
            })
            .WithName("RegistrarCliente")
            .WithSummary("Registra un cliente (persona natural) con su documento, nombres, correo y teléfono.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/clientes/{clienteId:guid}", async Task<Ok<FichaClienteDto>> (
                Guid clienteId, ObtenerClienteHandler handler, CancellationToken cancellationToken) =>
            {
                var ficha = await handler.EjecutarAsync(new ObtenerClienteConsulta(clienteId), cancellationToken);
                return TypedResults.Ok(ficha);
            })
            .WithName("ObtenerCliente")
            .WithSummary("Devuelve la ficha de un cliente con sus cuentas.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/clientes/por-documento", async Task<Results<Ok<FichaClienteDto>, ValidationProblem>> (
                string? tipoDocumento, string? numeroDocumento,
                BuscarClientePorDocumentoHandler handler, CancellationToken cancellationToken) =>
            {
                var resultado = await handler.EjecutarAsync(
                    new BuscarClientePorDocumentoConsulta(tipoDocumento, numeroDocumento), cancellationToken);

                return resultado.EsExito
                    ? TypedResults.Ok(resultado.Valor)
                    : RespuestasDeError.DatosInvalidos(resultado.Errores);
            })
            .WithName("BuscarClientePorDocumento")
            .WithSummary("Busca un cliente por tipo y número de documento (acepta puntos y guiones).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
