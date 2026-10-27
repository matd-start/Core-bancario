// SDD: esqueleto creado por test-writer. T03 lo reemplaza por el Program real y T09 lo deja en el orden del plan (3.3).
using CoreBancario.Bff.InicioDeSesion;

var builder = WebApplication.CreateBuilder(args);

// En el esqueleto esta llamada lanza NotImplementedException: el host no arranca hasta que T07 la implemente.
builder.Services.AddSesionDelNavegador(builder.Configuration, builder.Environment);

var app = builder.Build();
app.Run();

// Plan 3.3: el Program del BFF queda internal para que no choque con el Program publico de la API.
internal partial class Program;
