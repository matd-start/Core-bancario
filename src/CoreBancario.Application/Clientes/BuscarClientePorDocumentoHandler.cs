using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Clientes;

public sealed class BuscarClientePorDocumentoHandler(IClienteRepositorio clientes, Cuentas.ICuentaRepositorio cuentas)
{
    /// <exception cref="RecursoNoEncontradoException">Documento no registrado (CL-18).</exception>
    // SDD: esqueleto creado por test-writer
    public Task<Resultado<FichaClienteDto>> EjecutarAsync(BuscarClientePorDocumentoConsulta consulta, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
