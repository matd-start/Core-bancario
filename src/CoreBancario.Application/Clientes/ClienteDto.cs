using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

public sealed record ClienteDto(Guid Id, string TipoDocumento, string NumeroDocumento, string Nombres,
    string Apellidos, string Correo, string Telefono, DateTimeOffset FechaRegistro)
{
    // SDD: esqueleto creado por test-writer
    public static ClienteDto Desde(Cliente cliente) => throw new NotImplementedException();
}
