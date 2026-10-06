using CoreBancario.Application.Clientes;
using CoreBancario.Domain.Clientes;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class ClienteRepositorio(CoreBancarioDbContext db) : IClienteRepositorio
{
    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);

    public Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken cancellationToken) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);

    public Task<Cliente?> ObtenerPorDocumentoAsync(Documento documento, CancellationToken cancellationToken)
    {
        var tipo = documento.Tipo;
        var numero = documento.Numero;

        return db.Clientes.FirstOrDefaultAsync(
            c => c.Documento.Tipo == tipo && c.Documento.Numero == numero,
            cancellationToken);
    }
}
