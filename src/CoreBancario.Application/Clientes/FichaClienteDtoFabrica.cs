using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

/// <summary>Arma la ficha (cliente + cuentas) para los dos handlers de consulta de clientes.</summary>
internal static class FichaClienteDtoFabrica
{
    public static async Task<FichaClienteDto> CrearAsync(
        Cliente cliente, ICuentaRepositorio cuentas, CancellationToken cancellationToken)
    {
        var delCliente = await cuentas.ListarPorClienteAsync(cliente.Id, cancellationToken);

        var dtos = delCliente
            .OrderBy(c => c.FechaApertura)
            .ThenBy(c => c.Id)
            .Select(CuentaDto.Desde)
            .ToList();

        return new FichaClienteDto(ClienteDto.Desde(cliente), dtos);
    }
}
