using CoreBancario.Application.Clientes;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class ClienteRepositorio(CoreBancarioDbContext db) : IClienteRepositorio
{
    // SDD: esqueleto creado por test-writer
    public void Agregar(Cliente cliente) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken cancellationToken) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public Task<Cliente?> ObtenerPorDocumentoAsync(Documento documento, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
