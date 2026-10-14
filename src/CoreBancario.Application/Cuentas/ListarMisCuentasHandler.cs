namespace CoreBancario.Application.Cuentas;

public sealed class ListarMisCuentasHandler(ICuentasDelTitular cuentas)
{
    /// <summary>Todas las cuentas del titular, incluidas las Cerradas (CL-18). Vacía si no tiene o no hay vínculo (CA-17).</summary>
    public async Task<IReadOnlyList<CuentaDto>> EjecutarAsync(ListarMisCuentasConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var lista = await cuentas.ListarAsync(consulta.Sub, cancellationToken);

        return lista.Select(CuentaDto.Desde).ToList();
    }
}
