namespace CoreBancario.Application.Cuentas;

public sealed class ObtenerCuentaHandler(ICuentaRepositorio cuentas)
{
    /// <exception cref="Comun.RecursoNoEncontradoException">CL-18.</exception>
    // SDD: esqueleto creado por test-writer
    public Task<CuentaDto> EjecutarAsync(ObtenerCuentaConsulta consulta, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
