using CoreBancario.Application.Comun;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>
/// Cuenta las llamadas a GuardarCambiosAsync y puede simular que la base rechaza el guardado. Avisa (Confirmado) cada
/// vez que un guardado SE COMPLETA: los fakes de los repositorios de la 003b marcan en ese momento sus filas como
/// confirmadas, así las pruebas distinguen "agregado" de "guardado" (ADR-0017).
/// </summary>
public sealed class UnidadDeTrabajoEspia : IUnidadDeTrabajo
{
    private readonly List<CancellationToken> _tokens = [];

    public int Llamadas { get; private set; }

    /// <summary>Si se indica, los guardados rechazan con esta excepción (todos, o solo el de GuardadoQueFalla).</summary>
    public Exception? ExcepcionAlGuardar { get; init; }

    /// <summary>Número (desde 1) del único guardado que falla. null = fallan todos cuando hay ExcepcionAlGuardar.</summary>
    public int? GuardadoQueFalla { get; init; }

    /// <summary>El CancellationToken que recibió cada guardado, en orden.</summary>
    public IReadOnlyList<CancellationToken> Tokens => _tokens;

    /// <summary>Se dispara tras cada guardado completado (no tras uno rechazado).</summary>
    public event Action? Confirmado;

    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        Llamadas++;
        _tokens.Add(cancellationToken);

        var falla = ExcepcionAlGuardar is not null && (GuardadoQueFalla is null || GuardadoQueFalla == Llamadas);
        if (falla)
            return Task.FromException(ExcepcionAlGuardar!);

        Confirmado?.Invoke();
        return Task.CompletedTask;
    }
}
