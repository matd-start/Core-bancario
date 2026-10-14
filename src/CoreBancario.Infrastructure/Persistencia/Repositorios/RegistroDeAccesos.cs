using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class RegistroDeAccesos(CoreBancarioDbContext db) : IRegistroDeAccesos
{
    public void AgregarSolicitud(SolicitudDeAcceso solicitud) => db.SolicitudesDeAcceso.Add(solicitud);

    public void AgregarResultado(ResultadoDeSolicitud resultado) => db.ResultadosDeAcceso.Add(resultado);

    public async Task<IReadOnlyList<SolicitudDeAcceso>> ListarPendientesAsync(
        Guid clienteId, CancellationToken cancellationToken) =>
        await db.SolicitudesDeAcceso
            .AsNoTracking()
            .Where(s => s.ClienteId == clienteId
                        && !db.ResultadosDeAcceso.Any(r => r.SolicitudId == s.Id))
            .OrderBy(s => s.Fecha)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
}
