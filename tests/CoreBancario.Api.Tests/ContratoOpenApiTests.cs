using System.Net;
using System.Text.Json.Nodes;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

/// <summary>
/// RNF-06, ADR-0019: el contrato OpenAPI se genera al compilar y se versiona en
/// src/CoreBancario.Api/openapi/core-bancario-api.json; fuera de Development no se sirve, asi que estas pruebas leen
/// el ARCHIVO versionado y no el endpoint.
/// </summary>
public class ContratoOpenApiTests(ApiFixture api)
{
    private static readonly string[] MetodosHttp = ["get", "put", "post", "delete", "patch"];

    private static JsonNode ContratoVersionado()
    {
        var ruta = Path.Combine(AyudasDeAutenticacion.RaizDelRepositorio(), "src", "CoreBancario.Api", "openapi", "core-bancario-api.json");
        return JsonNode.Parse(File.ReadAllText(ruta))!;
    }

    private static IEnumerable<(string Id, JsonNode Operacion)> Operaciones(JsonNode contrato) =>
        contrato["paths"]!.AsObject()
            .SelectMany(ruta => ruta.Value!.AsObject().Where(m => MetodosHttp.Contains(m.Key)))
            .Select(metodo => ((string?)metodo.Value!["operationId"] ?? "(sin operationId)", metodo.Value!));

    [Fact]
    public void F003_CA22_ContratoFueraDeDevelopment_DescribeElEsquemaDeAutenticacionBearer()
    {
        // Arrange
        var contrato = ContratoVersionado();

        // Act
        var bearer = contrato["components"]?["securitySchemes"]?["Bearer"];

        // Assert
        Assert.NotNull(bearer);
        Assert.Equal("http", (string?)bearer["type"]);
        Assert.Equal("bearer", (string?)bearer["scheme"]);
        Assert.Equal("JWT", (string?)bearer["bearerFormat"]);
    }

    [Fact]
    public void F003_CA22_ContratoFueraDeDevelopment_CadaOperacionProtegidaDeclara401_403_500()
    {
        // Arrange
        var contrato = ContratoVersionado();

        // Act
        var incompletas = Operaciones(contrato)
            .Where(o => new[] { "401", "403", "500" }.Any(codigo => o.Operacion["responses"]?[codigo] is null))
            .Select(o => o.Id)
            .ToList();

        // Assert
        Assert.NotEmpty(Operaciones(contrato));
        Assert.Empty(incompletas);
    }

    [Fact]
    public void F003_CA22_ContratoFueraDeDevelopment_CadaOperacionExigeElEsquemaBearer()
    {
        // Arrange
        var contrato = ContratoVersionado();

        // Act
        var sinRequisito = Operaciones(contrato)
            .Where(o => !(o.Operacion["security"]?.AsArray().Any(r => r!.AsObject().ContainsKey("Bearer")) ?? false))
            .Select(o => o.Id)
            .ToList();

        // Assert
        Assert.Empty(sinRequisito);
    }

    [Fact]
    public void F003_CA22_ContratoFueraDeDevelopment_LosErroresComunesSonProblemDetails()
    {
        // Arrange
        var contrato = ContratoVersionado();

        // Act
        var sinProblemDetails = Operaciones(contrato)
            .Where(o => new[] { "401", "403", "500" }.Any(codigo =>
                o.Operacion["responses"]?[codigo]?["content"]?["application/problem+json"] is null))
            .Select(o => o.Id)
            .ToList();

        // Assert
        Assert.Empty(sinProblemDetails);
    }

    [Fact]
    public void F003_CA22_ContratoFueraDeDevelopment_IncluyeLasOperacionesDeMisCuentas()
    {
        // Arrange
        var contrato = ContratoVersionado();

        // Act
        var ids = Operaciones(contrato).Select(o => o.Id).ToList();

        // Assert
        Assert.Contains("ListarMisCuentas", ids);
        Assert.Contains("ObtenerMiCuenta", ids);
    }

    [Fact]
    public async Task F003_RNF06_OpenApiEnDevelopment_SeSirveSinSesionYDescribeElEsquemaBearer()
    {
        // Arrange: /openapi/* es la excepcion a la politica por defecto "usuario autenticado".
        using var http = api.CrearClienteAnonimo();

        // Act
        var respuesta = await http.GetAsync("/openapi/v1.json", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var documento = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync(Ct))!;
        Assert.NotNull(documento["components"]?["securitySchemes"]?["Bearer"]);
    }
}
