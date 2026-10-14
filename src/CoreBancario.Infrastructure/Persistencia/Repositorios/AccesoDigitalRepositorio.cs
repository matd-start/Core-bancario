using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class AccesoDigitalRepositorio(CoreBancarioDbContext db) : IAccesoDigitalRepositorio
{
    public void Agregar(AccesoDigital acceso) => throw new NotImplementedException();

    public Task<AccesoDigital?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
