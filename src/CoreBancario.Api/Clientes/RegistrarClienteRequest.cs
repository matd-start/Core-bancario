namespace CoreBancario.Api.Clientes;

public sealed record RegistrarClienteRequest(string? TipoDocumento, string? NumeroDocumento, string? Nombres,
    string? Apellidos, string? Correo, string? Telefono);
