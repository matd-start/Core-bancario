namespace CoreBancario.Application.Clientes;

public sealed class ObtenerClienteHandler(IClienteRepositorio clientes, Cuentas.ICuentaRepositorio cuentas)
{
    /// <exception cref="Comun.RecursoNoEncontradoException">No existe el cliente (CL-18).</exception>
    // SDD: esqueleto creado por test-writer
    public Task<FichaClienteDto> EjecutarAsync(ObtenerClienteConsulta consulta, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
