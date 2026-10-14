using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class AccesoDigitalRepositorio(CoreBancarioDbContext db) : IAccesoDigitalRepositorio
{
    public void Agregar(AccesoDigital acceso) => db.AccesosDigitales.Add(acceso);

    public Task<AccesoDigital?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken cancellationToken) =>
        db.AccesosDigitales.AsNoTracking().FirstOrDefaultAsync(a => a.ClienteId == clienteId, cancellationToken);
}
