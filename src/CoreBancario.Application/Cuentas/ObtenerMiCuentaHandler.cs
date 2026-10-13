// SDD: esqueleto creado por test-writer
using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class ObtenerMiCuentaHandler
{
    private readonly ICuentasDelTitular _cuentas;

    public ObtenerMiCuentaHandler(ICuentasDelTitular cuentas) => _cuentas = cuentas;

    /// <exception cref="RecursoNoEncontradoException">
    /// Inexistente o ajena, con EXACTAMENTE el mismo mensaje que ObtenerCuentaHandler: "No existe una cuenta con ese identificador." (CA-15).
    /// </exception>
    public Task<CuentaDto> EjecutarAsync(ObtenerMiCuentaConsulta consulta, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
