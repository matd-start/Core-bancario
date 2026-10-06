namespace CoreBancario.Application.Comun;

public interface IUnidadDeTrabajo
{
    /// <summary>Guarda todos los cambios pendientes en una sola transacción (RNF-06).</summary>
    /// <exception cref="Clientes.DocumentoDuplicadoException">Violación del índice único del documento (CL-01, CL-03).</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">La fila cambió desde que se leyó (xmin, CL-15) o choque improbable del número de cuenta (CL-11).</exception>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
