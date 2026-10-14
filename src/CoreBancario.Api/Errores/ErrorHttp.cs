namespace CoreBancario.Api.Errores;

/// <summary>
/// Previsto: un 5xx que el catálogo conoce (por ejemplo un fallo del sistema de identidad). Se registra como Warning y
/// envía su mensaje fijo en "detail". Un 5xx no previsto es un error inesperado: Error y sin detail.
/// </summary>
public sealed record ErrorHttp(int Estado, string Codigo, string Titulo, bool Previsto = false);
