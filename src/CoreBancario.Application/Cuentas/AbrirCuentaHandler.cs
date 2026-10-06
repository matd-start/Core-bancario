using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Cuentas;

public sealed class AbrirCuentaHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas,
    IGeneradorDeNumeroDeCuenta generador, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    public const int MaximoDeIntentos = 5;

    /// <exception cref="RecursoNoEncontradoException">CL-09.</exception>
    /// <exception cref="InvalidOperationException">Los MaximoDeIntentos numeros generados ya existian.</exception>
    // SDD: esqueleto creado por test-writer
    public Task<Resultado<CuentaDto>> EjecutarAsync(AbrirCuentaComando comando, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
