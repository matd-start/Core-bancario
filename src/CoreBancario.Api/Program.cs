using System.Text.Json.Serialization;
using CoreBancario.Api.AccesosDigitales;
using CoreBancario.Api.Autenticacion;
using CoreBancario.Api.Clientes;
using CoreBancario.Api.Cuentas;
using CoreBancario.Api.DocumentacionOpenApi;
using CoreBancario.Api.Errores;
using CoreBancario.Application;
using CoreBancario.Infrastructure;
using CoreBancario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(opciones =>
{
    opciones.AddDocumentTransformer<EsquemaDeSeguridadEnOpenApi>();
    opciones.AddDocumentTransformer<CodigosDeErrorEnOpenApi>();
    opciones.AddDocumentTransformer<ExtensionesDeErrorEnOpenApi>();
    opciones.AddOperationTransformer<RequisitoDeSeguridadEnOpenApi>();
});

// Por defecto ASP.NET acepta "5" (texto) donde va un número, y el contrato lo publica como number | string. Estricto:
// un entero es solo un número, en lo que se acepta y en lo que se documenta (RNF-13 de la 004).
builder.Services.ConfigureHttpJsonOptions(opciones => opciones.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorDeErrores>();

// Un cuerpo mal formado lanza BadHttpRequestException en vez de responder 400 sin formato:
// así pasa por ManejadorDeErrores y sale con el mismo contrato de errores que todo lo demás.
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = true);

builder.Services.AddAutenticacionConKeycloak(builder.Configuration, builder.Environment);
builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // El contrato en desarrollo se sirve sin sesión; fuera de Development no se expone (ADR-0019).
    app.MapOpenApi().AllowAnonymous();

    // Solo en desarrollo: migrar al arrancar con varias instancias es mala práctica (D-11 decidirá producción).
    using var alcance = app.Services.CreateScope();
    await alcance.ServiceProvider.GetRequiredService<CoreBancarioDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();

// Después de UseExceptionHandler: los errores de las capas siguientes salen con el contrato de errores.
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapEndpointsDeClientes();
app.MapEndpointsDeCuentas();
app.MapEndpointsDeMisCuentas();
app.MapEndpointsDeAccesoDigital();

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas.
public partial class Program;
