using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoreBancario.Infrastructure.Persistencia;

public sealed class CoreBancarioDbContext(DbContextOptions<CoreBancarioDbContext> options) : DbContext(options), IUnidadDeTrabajo
{
    // Los nombres de los índices únicos se fijan a mano en las configuraciones porque aquí se usan para
    // distinguir qué regla se rompió al traducir el error de PostgreSQL.
    internal const string IndiceUnicoDeDocumento = "ux_clientes_documento";
    internal const string IndiceUnicoDeNumeroDeCuenta = "ux_cuentas_numero";

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    /// <summary>
    /// SaveChangesAsync con traducción de errores: la capa Application no conoce EF Core ni Npgsql, así que
    /// aquí los errores de la base se convierten en las excepciones de aplicación.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException excepcion)
        {
            throw new ConflictoDeConcurrenciaException(excepcion);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                  && postgres.ConstraintName == IndiceUnicoDeDocumento)
        {
            throw new DocumentoDuplicadoException(excepcion);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                  && postgres.ConstraintName == IndiceUnicoDeNumeroDeCuenta)
        {
            throw new ConflictoDeConcurrenciaException(excepcion);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreBancarioDbContext).Assembly);
}
