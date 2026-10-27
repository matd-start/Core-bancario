using System.Text.Json.Nodes;

namespace CoreBancario.Bff.Tests;

/// <summary>
/// El contrato OpenAPI del BFF se genera al compilar y se versiona en src/CoreBancario.Bff/openapi/core-bancario-bff.json
/// (como el de la API, ADR-0019). Estas pruebas leen el ARCHIVO versionado.
/// </summary>
public class ContratoDelBffTests
{
    private static readonly string[] MetodosHttp = ["get", "put", "post", "delete", "patch"];

    private static JsonNode Contrato()
    {
        var ruta = Path.Combine(Ayudas.RaizDelRepositorio(), "src", "CoreBancario.Bff", "openapi", "core-bancario-bff.json");
        Assert.True(File.Exists(ruta), $"Falta el contrato versionado del BFF: {ruta} (T09).");
        return JsonNode.Parse(File.ReadAllText(ruta))!;
    }

    private static IEnumerable<(string Ruta, string Metodo, string? Id)> Operaciones(JsonNode contrato) =>
        contrato["paths"]!.AsObject().SelectMany(ruta => ruta.Value!.AsObject()
            .Where(m => MetodosHttp.Contains(m.Key))
            .Select(m => (ruta.Key, m.Key, (string?)m.Value!["operationId"])));

    [Fact]
    public void F004_RNF12_ContratoDelBff_EnumeraSusCodigos()
    {
        // Arrange
        var contrato = Contrato();

        // Act
        var esquema = contrato["components"]?["schemas"]?["CodigoDeErrorDelBff"];
        var codigos = esquema?["enum"]?.AsArray().Select(c => (string)c!).Order().ToList();

        // Assert
        Assert.NotNull(esquema);
        Assert.Equal("string", (string?)esquema["type"]);
        Assert.Equal(
            new[]
            {
                "identidad-sin-respuesta", "peticion-no-confiable", "sesion-revocada", "sesion-vencida-por-inactividad",
                "sesion-vencida-por-vida-maxima", "sin-sesion",
            }.Order(),
            codigos);
    }

    [Fact]
    public void F004_RNF12_ContratoDelBff_DocumentaLasCincoOperacionesYNoElProxy()
    {
        // Arrange
        var contrato = Contrato();

        // Act
        var ids = Operaciones(contrato).Select(o => o.Id).Order().ToList();

        // Assert
        Assert.Equal(new[] { "CerrarSesion", "CambiarClave", "IniciarSesion", "ObtenerSesion", "RegistrarActividad" }.Order(), ids);
        Assert.DoesNotContain(Operaciones(contrato), o => o.Ruta.StartsWith("/api", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/bff/actividad")]
    [InlineData("/bff/salir")]
    public void F004_RNF02_ContratoDelBff_LasOperacionesQueCambianDatosSoloSonPost(string ruta)
    {
        // Arrange
        var contrato = Contrato();

        // Act
        var metodos = Operaciones(contrato).Where(o => o.Ruta == ruta).Select(o => o.Metodo).ToList();

        // Assert
        Assert.Equal(["post"], metodos);
    }

    [Fact]
    public void F004_RNF02_ContratoDelBff_LosGetSonSoloLasNavegacionesYLaConsultaDeSesion()
    {
        // Arrange
        var contrato = Contrato();

        // Act
        var getters = Operaciones(contrato).Where(o => o.Metodo == "get").Select(o => o.Id).Order().ToList();

        // Assert
        Assert.Equal(new[] { "CambiarClave", "IniciarSesion", "ObtenerSesion" }.Order(), getters);
    }
}
