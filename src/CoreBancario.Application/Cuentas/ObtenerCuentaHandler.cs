using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class ObtenerCuentaHandler(ICuentaRepositorio cuentas)
{
    // Compartido con ObtenerMiCuentaHandler: una cuenta ajena y una inexistente deben dar EXACTAMENTE el mismo mensaje (CA-15).
    internal const string MensajeDeCuentaNoEncontrada = "No existe una cuenta con ese identificador.";

    /// <exception cref="RecursoNoEncontradoException">CL-18.</exception>
    public async Task<CuentaDto> EjecutarAsync(ObtenerCuentaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var cuenta = await cuentas.ObtenerAsync(consulta.CuentaId, cancellationToken)
            ?? throw new RecursoNoEncontradoException(MensajeDeCuentaNoEncontrada);

        return CuentaDto.Desde(cuenta);
    }
}
