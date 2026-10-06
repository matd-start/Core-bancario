using CoreBancario.Application.Comun;
using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Persistencia;

public sealed class CoreBancarioDbContext(DbContextOptions<CoreBancarioDbContext> options) : DbContext(options), IUnidadDeTrabajo
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    /// <summary>SaveChangesAsync con traduccion de errores de la base (plan, seccion 5).</summary>
    // SDD: esqueleto creado por test-writer
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
