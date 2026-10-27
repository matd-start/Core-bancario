namespace CoreBancario.Bff.Seguridad;

/// <summary>En /bff/** y /api/**: Cache-Control: no-store y X-Content-Type-Options: nosniff (CL-11).</summary>
public sealed class EncabezadosDeSeguridad
{
    // SDD: esqueleto creado por test-writer
    public EncabezadosDeSeguridad(RequestDelegate siguiente)
    {
    }

    public Task InvokeAsync(HttpContext contexto) => throw new NotImplementedException();
}
