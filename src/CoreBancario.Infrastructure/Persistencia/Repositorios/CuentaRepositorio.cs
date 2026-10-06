using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Infrastructure.Persistencia.Repositorios;

public sealed class CuentaRepositorio(CoreBancarioDbContext db) : ICuentaRepositorio
{
    // SDD: esqueleto creado por test-writer
    public void Agregar(Cuenta cuenta) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, CancellationToken cancellationToken) => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public Task<bool> ExisteNumeroAsync(NumeroDeCuenta numero, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
