using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Sesiones;

/// <summary>ADR-0021. Todas las operaciones evalúan PoliticaDeSesion primero: si venció, CerrarAsync y lanza SesionTerminadaException.</summary>
public sealed class ServicioDeSesion
{
    /// <summary>Si al access token le queda menos, el proxy lo renueva antes de reenviar.</summary>
    public static readonly TimeSpan MargenDeRenovacion = TimeSpan.FromSeconds(30);

    // SDD: esqueleto creado por test-writer
    public ServicioDeSesion(AlmacenDeSesiones almacen, IClienteDeIdentidad identidad, IOptions<OpcionesDelBff> opciones,
        TimeProvider reloj, ILogger<ServicioDeSesion> log)
    {
    }

    /// <summary>NO cuenta como actividad (RN-25).</summary>
    /// <exception cref="SesionTerminadaException">Sin sesión (SinSesion) o vencida.</exception>
    public Task<SesionDto> ConsultarAsync(string? clave, CancellationToken cancellationToken) => throw new NotImplementedException();

    /// <summary>Latido: renueva los tokens y después registra UltimaActividad = ahora (con candado).</summary>
    public Task<SesionDto> RegistrarActividadAsync(string? clave, CancellationToken cancellationToken) => throw new NotImplementedException();

    /// <summary>Para el proxy. NUNCA registra actividad. Renueva solo si AccessTokenVence - ahora &lt; MargenDeRenovacion.</summary>
    /// <exception cref="SesionTerminadaException">Sin sesión, vencida o renovación rechazada (Revocada).</exception>
    /// <exception cref="IdentidadSinRespuestaException">Hacía falta renovar y Keycloak no respondió.</exception>
    public Task<string> ObtenerAccessTokenAsync(string? clave, CancellationToken cancellationToken) => throw new NotImplementedException();

    /// <summary>Cierre en Keycloak por el canal trasero (mejor esfuerzo) y borrado del almacén. Idempotente.</summary>
    public Task CerrarAsync(string? clave, CancellationToken cancellationToken) => throw new NotImplementedException();
}
