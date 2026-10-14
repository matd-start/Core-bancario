using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class RegistroDeAccesos(CoreBancarioDbContext db) : IRegistroDeAccesos
{
    public void AgregarSolicitud(SolicitudDeAcceso solicitud) => throw new NotImplementedException();

    public void AgregarResultado(ResultadoDeSolicitud resultado) => throw new NotImplementedException();

    public Task<IReadOnlyList<SolicitudDeAcceso>> ListarPendientesAsync(Guid clienteId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
