// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.OpenApi;

public static class DocumentacionDeErrores
{
    /// <summary>Declara en el OpenAPI las respuestas 401, 403 y 500 con ProblemDetails (RNF-06).</summary>
    public static TBuilder DocumentarErroresComunes<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        throw new NotImplementedException();
}
