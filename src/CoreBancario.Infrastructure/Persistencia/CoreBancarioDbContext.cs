using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;
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
    internal const string IndiceUnicoDeSubDeAcceso = "ux_accesos_digitales_sub";
    internal const string ClavePrimariaDeAcceso = "pk_accesos_digitales";
    internal const string ClavePrimariaDeResultadoDeAcceso = "pk_resultados_de_acceso";

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<AccesoDigital> AccesosDigitales => Set<AccesoDigital>();
    public DbSet<SolicitudDeAcceso> SolicitudesDeAcceso => Set<SolicitudDeAcceso>();
    public DbSet<ResultadoDeSolicitud> ResultadosDeAcceso => Set<ResultadoDeSolicitud>();

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
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                  && EsUnicidadDelRegistroDeAccesos(postgres.ConstraintName))
        {
            // Dos operadores sobre el mismo cliente (vínculo) o dos cierres de la misma solicitud (resultado): 409.
            throw new ConflictoDeConcurrenciaException(excepcion);
        }
    }

    private static bool EsUnicidadDelRegistroDeAccesos(string? restriccion) =>
        restriccion is ClavePrimariaDeAcceso or IndiceUnicoDeSubDeAcceso or ClavePrimariaDeResultadoDeAcceso;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreBancarioDbContext).Assembly);
}
