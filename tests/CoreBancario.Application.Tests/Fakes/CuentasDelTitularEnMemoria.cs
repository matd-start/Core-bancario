using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>
/// Cumple el contrato del puerto (ADR-0016): el filtro de dueño va DENTRO de la consulta. Una cuenta ajena y una
/// inexistente devuelven lo mismo (null) y un sub sin vínculo no tiene cuentas. Devuelve las cuentas en el orden
/// en que se agregaron: ordenar es responsabilidad del puerto real, no del handler.
/// </summary>
public sealed class CuentasDelTitularEnMemoria : ICuentasDelTitular
{
    private readonly Dictionary<string, Guid> _clientePorSub = [];

    public List<Cuenta> Cuentas { get; } = [];

    public void Vincular(string sub, Guid clienteId) => _clientePorSub[sub] = clienteId;

    public Task<IReadOnlyList<Cuenta>> ListarAsync(string sub, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Cuenta>>(
            _clientePorSub.TryGetValue(sub, out var clienteId)
                ? Cuentas.Where(c => c.ClienteId == clienteId).ToList()
                : []);

    public Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub, CancellationToken cancellationToken) =>
        Task.FromResult(
            _clientePorSub.TryGetValue(sub, out var clienteId)
                ? Cuentas.FirstOrDefault(c => c.Id == cuentaId && c.ClienteId == clienteId)
                : null);
}
