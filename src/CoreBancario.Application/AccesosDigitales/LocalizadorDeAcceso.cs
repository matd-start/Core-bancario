using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.AccesosDigitales;

#pragma warning disable CS9113 // Parámetros sin leer: es un esqueleto sin cuerpo.
// SDD: esqueleto creado por test-writer
public sealed class LocalizadorDeAcceso(IAccesoDigitalRepositorio accesos, IProveedorDeIdentidad proveedor)
{
    /// <summary>
    /// Con vínculo: ObtenerAsync(vinculo.Sub). Sin vínculo: BuscarPorNombreDeUsuarioAsync(AccesoDigital.NombreDeUsuarioDe(cliente.Documento)),
    /// que encuentra al usuario que dejó un habilitar interrumpido, para ADOPTARLO en vez de crearlo dos veces (ADR-0017).
    /// </summary>
    /// <exception cref="InvalidOperationException">Hay vínculo pero el usuario ya no existe en el proveedor (inconsistencia; 500).</exception>
    public Task<AccesoLocalizado> LocalizarAsync(Cliente cliente, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
