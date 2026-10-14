using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

/// <summary>EF Core, AsNoTracking: cuentas c WHERE (c.Id = @id) AND EXISTS (accesos_digitales a WHERE a.cliente_id = c.cliente_id AND a.sub = @sub).</summary>
public sealed class CuentasDelTitular(CoreBancarioDbContext db) : ICuentasDelTitular
{
    public async Task<IReadOnlyList<Cuenta>> ListarAsync(string sub, CancellationToken cancellationToken) =>
        await DelTitular(sub)
            .OrderBy(c => c.FechaApertura)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub, CancellationToken cancellationToken) =>
        DelTitular(sub).FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken);

    // El filtro de dueño va DENTRO de la consulta (EXISTS sobre el vínculo): una cuenta ajena y una inexistente
    // son indistinguibles para quien llama (ADR-0016). Sin seguimiento: es una lectura.
    private IQueryable<Cuenta> DelTitular(string sub) =>
        db.Cuentas
            .AsNoTracking()
            .Where(c => db.AccesosDigitales.Any(a => a.ClienteId == c.ClienteId && a.Sub == sub));
}
