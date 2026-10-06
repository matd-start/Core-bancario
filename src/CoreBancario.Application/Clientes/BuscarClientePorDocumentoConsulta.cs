namespace CoreBancario.Application.Clientes;

public sealed record BuscarClientePorDocumentoConsulta(string? TipoDocumento, string? NumeroDocumento);
