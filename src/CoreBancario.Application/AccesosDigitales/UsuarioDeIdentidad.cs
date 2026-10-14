namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Lo que la API necesita saber de un usuario del realm clientes. UltimaSolicitud: la marca de ADR-0017 (null si nunca se puso o no es un Guid).</summary>
public sealed record UsuarioDeIdentidad(string Sub, bool Habilitado, Guid? UltimaSolicitud);
