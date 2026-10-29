namespace CoreBancario.Application.AccesosDigitales;

// SDD: esqueleto creado por test-writer
/// <summary>Estado: "SinAcceso" | "Habilitado" | "Deshabilitado". PrimerIngresoPendiente: null salvo si Habilitado.</summary>
public sealed record EstadoDeAccesoDto(Guid ClienteId, string Estado, bool? PrimerIngresoPendiente);
