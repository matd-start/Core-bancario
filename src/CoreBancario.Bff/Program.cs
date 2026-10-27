using CoreBancario.Bff.Endpoints;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.OpenApi;
using CoreBancario.Bff.Proxy;
using CoreBancario.Bff.Seguridad;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSesionDelNavegador(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();
builder.Services.AddHttpForwarder();
builder.Services.AddOpenApi(opciones => opciones.AddDocumentTransformer<CodigosDelBffEnOpenApi>());
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    // Detrás de nginx: el BFF no tiene puertos publicados, así que su único cliente es el proxy de la red de compose.
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<EncabezadosDeSeguridad>();
app.UseMiddleware<ProteccionCsrf>();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpointsDelBff();
app.MapProxyDeLaApi();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.Run();

// El Program del BFF queda internal para que no choque con el Program público de la API (CS0433 en Keycloak.Tests).
internal partial class Program;
