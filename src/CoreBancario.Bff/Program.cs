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
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();

    // Quién puede decir "vengo de https" o "mi IP es X": Bff:RedesDeConfianza (lista de redes CIDR, por ejemplo la de compose).
    // Sin valor se confía en cualquier origen. Es un supuesto, no una protección: el BFF no publica puertos y solo lo alcanza
    // el contenedor web (nginx), así que ese es el único que puede enviar estos encabezados. Si algún día se publica el 5180,
    // hay que configurar esta lista.
    var redes = builder.Configuration.GetSection("Bff:RedesDeConfianza").Get<string[]>() ?? [];
    foreach (var red in redes)
        opciones.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(red));
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
