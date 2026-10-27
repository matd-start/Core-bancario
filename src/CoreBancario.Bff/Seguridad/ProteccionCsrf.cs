namespace CoreBancario.Bff.Seguridad;

/// <summary>RNF-02 (ADR-0020). Protege /api/** y /bff/**, salvo las exenciones del plan. 403 "peticion-no-confiable".</summary>
public sealed class ProteccionCsrf
{
    public const string Encabezado = "X-CSRF";
    public const string Valor = "1";

    // SDD: esqueleto creado por test-writer
    public ProteccionCsrf(RequestDelegate siguiente)
    {
    }

    public Task InvokeAsync(HttpContext contexto) => throw new NotImplementedException();
}
