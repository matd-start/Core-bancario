// SDD: esqueleto creado por test-writer
namespace CoreBancario.Application.Cuentas;

public sealed class ListarMisCuentasHandler
{
    private readonly ICuentasDelTitular _cuentas;

    public ListarMisCuentasHandler(ICuentasDelTitular cuentas) => _cuentas = cuentas;

    public Task<IReadOnlyList<CuentaDto>> EjecutarAsync(ListarMisCuentasConsulta consulta, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
