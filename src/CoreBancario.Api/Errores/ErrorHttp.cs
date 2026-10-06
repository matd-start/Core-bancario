namespace CoreBancario.Api.Errores;

public sealed record ErrorHttp(int Estado, string Codigo, string Titulo);
