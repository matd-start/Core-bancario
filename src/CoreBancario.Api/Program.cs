using CoreBancario.Application;
using CoreBancario.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// SDD: esqueleto creado por test-writer (T20 completa Program.cs: errores, endpoints y migraciones)
builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.Run();

// SDD: esqueleto creado por test-writer. Necesario para WebApplicationFactory<Program>.
public partial class Program;
