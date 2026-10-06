using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;

namespace CoreBancario.Application.Clientes;

public sealed class ObtenerClienteHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas)
{
    /// <exception cref="RecursoNoEncontradoException">No existe el cliente (CL-18).</exception>
    public async Task<FichaClienteDto> EjecutarAsync(ObtenerClienteConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var cliente = await clientes.ObtenerAsync(consulta.ClienteId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe un cliente con ese identificador.");

        return await FichaClienteDtoFabrica.CrearAsync(cliente, cuentas, cancellationToken);
    }
}
