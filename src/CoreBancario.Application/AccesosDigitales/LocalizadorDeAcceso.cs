using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.AccesosDigitales;

public sealed class LocalizadorDeAcceso(IAccesoDigitalRepositorio accesos, IProveedorDeIdentidad proveedor)
{
    /// <summary>
    /// Con vínculo: ObtenerAsync(vinculo.Sub). Sin vínculo: BuscarPorNombreDeUsuarioAsync(AccesoDigital.NombreDeUsuarioDe(cliente.Documento)),
    /// que encuentra al usuario que dejó un habilitar interrumpido, para ADOPTARLO en vez de crearlo dos veces (ADR-0017).
    /// </summary>
    /// <exception cref="InvalidOperationException">Hay vínculo pero el usuario ya no existe en el proveedor (inconsistencia; 500).</exception>
    public async Task<AccesoLocalizado> LocalizarAsync(Cliente cliente, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        var vinculo = await accesos.ObtenerPorClienteAsync(cliente.Id, cancellationToken);
        if (vinculo is null)
        {
            var huerfano = await proveedor.BuscarPorNombreDeUsuarioAsync(
                AccesoDigital.NombreDeUsuarioDe(cliente.Documento), cancellationToken);
            return new AccesoLocalizado(null, huerfano);
        }

        var usuario = await proveedor.ObtenerAsync(vinculo.Sub, cancellationToken)
            ?? throw new InvalidOperationException(
                $"El cliente {cliente.Id} tiene un vínculo de acceso, pero su usuario ya no existe en el sistema de identidad.");

        return new AccesoLocalizado(vinculo, usuario);
    }
}
