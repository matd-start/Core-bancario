using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Cuentas;

public sealed class AbrirCuentaHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas,
    IGeneradorDeNumeroDeCuenta generador, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    public const int MaximoDeIntentos = 5;

    /// <summary>
    /// 1) Moneda: Moneda.TryDesdeCodigo; si falla → Invalido con la clave "moneda" (CL-10), sin tocar la base.
    /// 2) Cliente: ObtenerAsync; si no existe → RecursoNoEncontradoException (CL-09).
    /// 3) Número: hasta MaximoDeIntentos veces, Generar y ExisteNumeroAsync; el primero libre se usa (CL-11).
    /// 4) Cuenta.Abrir(numero, clienteId, moneda, reloj.AhoraEnMicrosegundos()) + Agregar + GuardarCambiosAsync.
    /// </summary>
    /// <exception cref="RecursoNoEncontradoException">CL-09.</exception>
    /// <exception cref="InvalidOperationException">Los MaximoDeIntentos números generados ya existían (espacio agotado: 500).</exception>
    public async Task<Resultado<CuentaDto>> EjecutarAsync(AbrirCuentaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!Moneda.TryDesdeCodigo(comando.Moneda, out var moneda))
        {
            return Resultado<CuentaDto>.Invalido(new Dictionary<string, string[]>
            {
                ["moneda"] = ["La moneda debe ser COP o USD."],
            });
        }

        if (await clientes.ObtenerAsync(comando.ClienteId, cancellationToken) is null)
            throw new RecursoNoEncontradoException("No existe un cliente con ese identificador.");

        var numero = await GenerarNumeroLibreAsync(cancellationToken);

        var cuenta = Cuenta.Abrir(numero, comando.ClienteId, moneda, reloj.AhoraEnMicrosegundos());

        cuentas.Agregar(cuenta);
        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);

        return Resultado<CuentaDto>.Exito(CuentaDto.Desde(cuenta));
    }

    // Consulta previa en vez de reintentar tras un fallo del índice: más simple (plan, decisión 4).
    // El índice único ux_cuentas_numero sigue siendo la garantía final.
    private async Task<NumeroDeCuenta> GenerarNumeroLibreAsync(CancellationToken cancellationToken)
    {
        for (var intento = 0; intento < MaximoDeIntentos; intento++)
        {
            var candidato = generador.Generar();
            if (!await cuentas.ExisteNumeroAsync(candidato, cancellationToken))
                return candidato;
        }

        throw new InvalidOperationException(
            $"No se encontró un número de cuenta libre tras {MaximoDeIntentos} intentos.");
    }
}
