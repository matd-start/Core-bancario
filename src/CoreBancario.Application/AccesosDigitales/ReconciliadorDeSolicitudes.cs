using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

public sealed class ReconciliadorDeSolicitudes(IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    private const string MotivoDeConfirmada = "Confirmada al reintentar";
    private const string MotivoDeNoAplicada = "No se aplicó; cerrada al reintentar";

    /// <summary>
    /// Reconciliación (a) de ADR-0017. Cada solicitud pendiente del cliente se cierra: Completado (motivo "Confirmada al
    /// reintentar") si usuario?.UltimaSolicitud == solicitud.Id; si no, Fallido (motivo "No se aplicó; cerrada al reintentar").
    /// Un solo GuardarCambiosAsync si cerró alguna; ninguno si no había pendientes. Devuelve los resultados escritos.
    /// </summary>
    public async Task<IReadOnlyList<ResultadoDeSolicitud>> ReconciliarAsync(
        Guid clienteId, UsuarioDeIdentidad? usuario, CancellationToken cancellationToken)
    {
        var pendientes = await registro.ListarPendientesAsync(clienteId, cancellationToken);
        if (pendientes.Count == 0)
            return [];

        var ahora = reloj.AhoraEnMicrosegundos();
        var resultados = new List<ResultadoDeSolicitud>(pendientes.Count);
        foreach (var solicitud in pendientes)
        {
            var seAplico = usuario?.UltimaSolicitud == solicitud.Id;
            var resultado = seAplico
                ? solicitud.Completar(ahora, MotivoDeConfirmada)
                : solicitud.Fallar(ahora, MotivoDeNoAplicada);

            registro.AgregarResultado(resultado);
            resultados.Add(resultado);
        }

        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);
        return resultados;
    }
}
