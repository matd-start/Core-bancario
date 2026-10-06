using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

public sealed record ClienteDto(Guid Id, string TipoDocumento, string NumeroDocumento, string Nombres,
    string Apellidos, string Correo, string Telefono, DateTimeOffset FechaRegistro)
{
    public static ClienteDto Desde(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        return new ClienteDto(
            cliente.Id,
            cliente.Documento.Tipo.ToString(),
            cliente.Documento.Numero,
            cliente.Nombres.Valor,
            cliente.Apellidos.Valor,
            cliente.Correo.Valor,
            cliente.Telefono.Valor,
            cliente.FechaRegistro);
    }
}
