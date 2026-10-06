using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Clientes;

public sealed class RegistrarClienteHandler(IClienteRepositorio clientes, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <exception cref="DocumentoDuplicadoException">CL-01, CL-03, CL-08.</exception>
    // SDD: esqueleto creado por test-writer
    public Task<Resultado<ClienteDto>> EjecutarAsync(RegistrarClienteComando comando, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
