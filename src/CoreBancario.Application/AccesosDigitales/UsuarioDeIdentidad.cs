namespace CoreBancario.Application.AccesosDigitales;

/// <summary>
/// Lo que la API necesita saber de un usuario del realm clientes. UltimaSolicitud: la marca de ADR-0017 (null si nunca se puso o no es un Guid).
/// PrimerIngresoPendiente: el usuario aún tiene UPDATE_PASSWORD o CONFIGURE_TOTP en sus acciones obligatorias (RF-16).
/// </summary>
public sealed record UsuarioDeIdentidad(string Sub, bool Habilitado, Guid? UltimaSolicitud, bool PrimerIngresoPendiente = false);
