namespace CoreBancario.Application.AccesosDigitales;

/// <summary>
/// Puerto hacia el sistema de identidad (Keycloak, realm clientes), solo para gestionar accesos (RF-13). Cada operación
/// que cambia algo escribe la marca (Id de la solicitud) en su ÚLTIMO paso: si la marca coincide, se aplicó entera (ADR-0017).
/// Todas las operaciones pueden lanzar:
///   ProveedorDeIdentidadSinRespuestaException: no hubo respuesta (tiempo de espera, conexión); el cambio pudo aplicarse o no.
///   ErrorDelProveedorDeIdentidadException: respondió con un estado de error; la operación no se completó (pudo quedar a medias).
/// </summary>
public interface IProveedorDeIdentidad
{
    /// <summary>Búsqueda exacta, sin distinguir mayúsculas. null si no existe.</summary>
    Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken);

    /// <summary>null si no existe.</summary>
    Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken);

    /// <summary>
    /// Crea el usuario en UNA sola petición: habilitado, con la clave temporal (temporary = true), las acciones
    /// obligatorias UPDATE_PASSWORD y CONFIGURE_TOTP y la marca. Devuelve el sub asignado.
    /// </summary>
    Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken);

    /// <summary>
    /// Habilitar un acceso existente o restablecerlo (RN-20), en este orden: clave temporal nueva → borrar las
    /// credenciales OTP → cerrar todas sus sesiones → habilitado + UPDATE_PASSWORD + CONFIGURE_TOTP + marca.
    /// </summary>
    Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken);

    /// <summary>En este orden: cerrar todas sus sesiones → deshabilitado + marca.</summary>
    Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken);
}
