using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

public interface IClienteRepositorio
{
    void Agregar(Cliente cliente);
    Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken cancellationToken);
    Task<Cliente?> ObtenerPorDocumentoAsync(Documento documento, CancellationToken cancellationToken);
}
