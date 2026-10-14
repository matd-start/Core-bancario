namespace CoreBancario.Application.AccesosDigitales;

public sealed record RegistroDeAccesoDto(Guid SolicitudId, Guid ClienteId, string Accion, DateTimeOffset Fecha);
