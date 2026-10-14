using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class ObtenerMiCuentaHandler(ICuentasDelTitular cuentas)
{
    /// <exception cref="RecursoNoEncontradoException">
    /// Inexistente o ajena, con EXACTAMENTE el mismo mensaje que ObtenerCuentaHandler: "No existe una cuenta con ese identificador." (CA-15).
    /// </exception>
    public async Task<CuentaDto> EjecutarAsync(ObtenerMiCuentaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var cuenta = await cuentas.ObtenerAsync(consulta.CuentaId, consulta.Sub, cancellationToken)
            ?? throw new RecursoNoEncontradoException(ObtenerCuentaHandler.MensajeDeCuentaNoEncontrada);

        return CuentaDto.Desde(cuenta);
    }
}
