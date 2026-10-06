using CoreBancario.Api.Clientes;
using CoreBancario.Api.Cuentas;
using CoreBancario.Api.Errores;
using CoreBancario.Application;
using CoreBancario.Infrastructure;
using CoreBancario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorDeErrores>();

// Un cuerpo mal formado lanza BadHttpRequestException en vez de responder 400 sin formato:
// así pasa por ManejadorDeErrores y sale con el mismo contrato de errores que todo lo demás.
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = true);

builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Solo en desarrollo: migrar al arrancar con varias instancias es mala práctica (D-11 decidirá producción).
    using var alcance = app.Services.CreateScope();
    await alcance.ServiceProvider.GetRequiredService<CoreBancarioDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapEndpointsDeClientes();
app.MapEndpointsDeCuentas();

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas.
public partial class Program;
