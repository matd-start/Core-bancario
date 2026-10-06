using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class ObtenerCuentaHandler(ICuentaRepositorio cuentas)
{
    /// <exception cref="RecursoNoEncontradoException">CL-18.</exception>
    public async Task<CuentaDto> EjecutarAsync(ObtenerCuentaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var cuenta = await cuentas.ObtenerAsync(consulta.CuentaId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe una cuenta con ese identificador.");

        return CuentaDto.Desde(cuenta);
    }
}
