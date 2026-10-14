using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>
/// Distingue lo agregado de lo guardado, como una unidad de trabajo real: Agregar deja el vínculo "sin guardar" y solo
/// cuando la unidad de trabajo confirma pasa a Vinculos. ObtenerPorClienteAsync lee solo lo guardado (una consulta SQL
/// no ve lo que aún no se guardó).
/// </summary>
public sealed class AccesoDigitalRepositorioEnMemoria : IAccesoDigitalRepositorio
{
    private readonly List<AccesoDigital> _sinGuardar = [];
    private readonly List<AccesoDigital> _guardados = [];

    public AccesoDigitalRepositorioEnMemoria(UnidadDeTrabajoEspia unidadDeTrabajo) =>
        unidadDeTrabajo.Confirmado += Confirmar;

    /// <summary>Vínculos ya guardados.</summary>
    public IReadOnlyList<AccesoDigital> Vinculos => _guardados;

    /// <summary>Vínculos agregados que todavía no se guardaron.</summary>
    public IReadOnlyList<AccesoDigital> VinculosSinGuardar => _sinGuardar;

    /// <summary>Prepara un vínculo que ya existía antes de la operación.</summary>
    public void Sembrar(AccesoDigital acceso) => _guardados.Add(acceso);

    public void Agregar(AccesoDigital acceso) => _sinGuardar.Add(acceso);

    public Task<AccesoDigital?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken cancellationToken) =>
        Task.FromResult(_guardados.FirstOrDefault(a => a.ClienteId == clienteId));

    private void Confirmar()
    {
        _guardados.AddRange(_sinGuardar);
        _sinGuardar.Clear();
    }
}
