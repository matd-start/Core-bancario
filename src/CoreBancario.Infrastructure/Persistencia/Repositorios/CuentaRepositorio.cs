using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class CuentaRepositorio(CoreBancarioDbContext db) : ICuentaRepositorio
{
    public void Agregar(Cuenta cuenta) => db.Cuentas.Add(cuenta);

    // Con seguimiento de cambios: el cambio de estado modifica la entidad leída y luego guarda.
    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, CancellationToken cancellationToken) =>
        db.Cuentas.FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken);

    public async Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken) =>
        await db.Cuentas
            .AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .OrderBy(c => c.FechaApertura)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> ExisteNumeroAsync(NumeroDeCuenta numero, CancellationToken cancellationToken) =>
        db.Cuentas.AnyAsync(c => c.Numero == numero, cancellationToken);
}
