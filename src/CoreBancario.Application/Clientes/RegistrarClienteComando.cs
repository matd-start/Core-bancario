namespace CoreBancario.Application.Clientes;

public sealed record RegistrarClienteComando(string? TipoDocumento, string? NumeroDocumento, string? Nombres,
    string? Apellidos, string? Correo, string? Telefono);
