// SDD: esqueleto creado por test-writer
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

/// <summary>EF Core, AsNoTracking: cuentas c WHERE (c.Id = @id) AND EXISTS (accesos_digitales a WHERE a.cliente_id = c.cliente_id AND a.sub = @sub).</summary>
public sealed class CuentasDelTitular : ICuentasDelTitular
{
    private readonly CoreBancarioDbContext _db;

    public CuentasDelTitular(CoreBancarioDbContext db) => _db = db;

    public Task<IReadOnlyList<Cuenta>> ListarAsync(string sub, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
