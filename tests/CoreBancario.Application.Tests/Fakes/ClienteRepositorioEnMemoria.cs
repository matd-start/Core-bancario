using CoreBancario.Application.Clientes;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Tests.Fakes;

public sealed class ClienteRepositorioEnMemoria : IClienteRepositorio
{
    public List<Cliente> Clientes { get; } = [];

    public void Agregar(Cliente cliente) => Clientes.Add(cliente);

    public Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken cancellationToken) =>
        Task.FromResult(Clientes.FirstOrDefault(c => c.Id == clienteId));

    public Task<Cliente?> ObtenerPorDocumentoAsync(Documento documento, CancellationToken cancellationToken) =>
        Task.FromResult(Clientes.FirstOrDefault(c => c.Documento == documento));
}
