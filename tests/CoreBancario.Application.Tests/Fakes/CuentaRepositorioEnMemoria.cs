using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>Devuelve las cuentas en el orden en que se agregaron: el orden de la ficha lo decide el handler.</summary>
public sealed class CuentaRepositorioEnMemoria : ICuentaRepositorio
{
    public List<Cuenta> Cuentas { get; } = [];

    public void Agregar(Cuenta cuenta) => Cuentas.Add(cuenta);

    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, CancellationToken cancellationToken) =>
        Task.FromResult(Cuentas.FirstOrDefault(c => c.Id == cuentaId));

    public Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Cuenta>>(Cuentas.Where(c => c.ClienteId == clienteId).ToList());

    public Task<bool> ExisteNumeroAsync(NumeroDeCuenta numero, CancellationToken cancellationToken) =>
        Task.FromResult(Cuentas.Any(c => c.Numero == numero));
}
