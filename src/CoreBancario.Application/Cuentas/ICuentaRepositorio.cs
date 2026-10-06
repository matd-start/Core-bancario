using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

public interface ICuentaRepositorio
{
    void Agregar(Cuenta cuenta);

    /// <summary>Con seguimiento de cambios: lo usa también el cambio de estado.</summary>
    Task<Cuenta?> ObtenerAsync(Guid cuentaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken);

    Task<bool> ExisteNumeroAsync(NumeroDeCuenta numero, CancellationToken cancellationToken);
}
