using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>Cuenta las llamadas a GuardarCambiosAsync y puede simular que la base rechaza el guardado.</summary>
public sealed class UnidadDeTrabajoEspia : IUnidadDeTrabajo
{
    public int Llamadas { get; private set; }

    public Exception? ExcepcionAlGuardar { get; init; }

    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        Llamadas++;
        return ExcepcionAlGuardar is null ? Task.CompletedTask : Task.FromException(ExcepcionAlGuardar);
    }
}
