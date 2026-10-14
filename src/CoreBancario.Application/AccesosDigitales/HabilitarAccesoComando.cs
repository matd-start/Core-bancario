namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Operador: el sub del token del operador (RN-19), nunca un dato de la petición.</summary>
public sealed record HabilitarAccesoComando(Guid ClienteId, string Operador);
