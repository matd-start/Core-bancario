namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Estado: "SinAcceso" | "Habilitado" | "Deshabilitado". PrimerIngresoPendiente: null salvo si Habilitado.</summary>
public sealed record EstadoDeAccesoDto(Guid ClienteId, string Estado, bool? PrimerIngresoPendiente);
