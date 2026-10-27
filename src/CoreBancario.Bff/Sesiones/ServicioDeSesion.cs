using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Sesiones;

/// <summary>ADR-0021. Todas las operaciones evalúan PoliticaDeSesion primero: si venció, CerrarAsync y lanza SesionTerminadaException.</summary>
public sealed class ServicioDeSesion(AlmacenDeSesiones almacen, IClienteDeIdentidad identidad,
    IOptions<OpcionesDelBff> opciones, TimeProvider reloj, ILogger<ServicioDeSesion> log)
{
    /// <summary>Si al access token le queda menos, el proxy lo renueva antes de reenviar.</summary>
    public static readonly TimeSpan MargenDeRenovacion = TimeSpan.FromSeconds(30);

    /// <summary>NO cuenta como actividad (RN-25).</summary>
    /// <exception cref="SesionTerminadaException">Sin sesión (SinSesion) o vencida.</exception>
    public async Task<SesionDto> ConsultarAsync(string? clave, CancellationToken cancellationToken)
    {
        var sesion = await ObtenerVigenteAsync(clave, cancellationToken);
        return ADto(sesion);
    }

    /// <summary>
    /// Latido. Con candado: renueva los tokens (alinea la inactividad de Keycloak) y después registra UltimaActividad = ahora.
    /// RenovacionRechazadaException → cierra y lanza SesionTerminadaException(Revocada) (CL-12).
    /// IdentidadSinRespuestaException → registra la actividad igualmente y responde.
    /// </summary>
    public async Task<SesionDto> RegistrarActividadAsync(string? clave, CancellationToken cancellationToken)
    {
        await ObtenerVigenteAsync(clave, cancellationToken);

        return await almacen.ConCandadoAsync(clave!, async () =>
        {
            // Dentro del candado se vuelve a mirar: otra petición pudo cerrar la sesión mientras esperábamos.
            var sesion = await ObtenerVigenteAsync(clave, cancellationToken);
            try
            {
                await RenovarAsync(sesion, cancellationToken);
            }
            catch (IdentidadSinRespuestaException)
            {
                // Keycloak caído no debe castigar a quien sí está usando la app: la actividad cuenta igual (RN-25).
                log.LogWarning("Keycloak no respondió al renovar en el latido; se registra la actividad igualmente.");
            }

            sesion.RegistrarActividad(reloj.GetUtcNow());
            return ADto(sesion);
        }, cancellationToken);
    }

    /// <summary>
    /// Para el proxy. NUNCA registra actividad. Renueva (con candado y volviendo a mirar dentro de él) solo si
    /// AccessTokenVence - ahora &lt; MargenDeRenovacion.
    /// </summary>
    /// <exception cref="SesionTerminadaException">Sin sesión, vencida o renovación rechazada (Revocada).</exception>
    /// <exception cref="IdentidadSinRespuestaException">Hacía falta renovar y Keycloak no respondió (503; la sesión sigue).</exception>
    public async Task<string> ObtenerAccessTokenAsync(string? clave, CancellationToken cancellationToken)
    {
        var sesion = await ObtenerVigenteAsync(clave, cancellationToken);
        if (!NecesitaRenovar(sesion))
            return sesion.Tokens.AccessToken;

        return await almacen.ConCandadoAsync(clave!, async () =>
        {
            // Si otra petición ya renovó mientras esperábamos el candado, no se renueva dos veces (el refresh token rota).
            var actual = await ObtenerVigenteAsync(clave, cancellationToken);
            if (NecesitaRenovar(actual))
                await RenovarAsync(actual, cancellationToken);

            return actual.Tokens.AccessToken;
        }, cancellationToken);
    }

    /// <summary>
    /// Cierre en Keycloak por el canal trasero (mejor esfuerzo) y borrado del almacén. Idempotente: sin sesión no hace
    /// nada. Usado por /bff/salir, por el vencimiento, por la revocación y por RN-26. No toma el candado: se llama también
    /// desde dentro de él.
    /// </summary>
    public async Task CerrarAsync(string? clave, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(clave))
            return;

        var sesion = almacen.ObtenerParaEvaluar(clave);
        if (sesion is null)
            return;

        // Primero se borra: aunque Keycloak no responda, la sesión del BFF ya no sirve.
        almacen.Eliminar(clave);
        await identidad.CerrarSesionAsync(sesion.Area, sesion.Tokens.RefreshToken, cancellationToken);
    }

    private async Task<SesionDelNavegador> ObtenerVigenteAsync(string? clave, CancellationToken cancellationToken)
    {
        var sesion = string.IsNullOrEmpty(clave) ? null : almacen.ObtenerParaEvaluar(clave);
        if (sesion is null)
            throw new SesionTerminadaException(MotivoDeCierre.SinSesion);

        var motivo = PoliticaDeSesion.Evaluar(sesion, opciones.Value.De(sesion.Area), reloj.GetUtcNow()) switch
        {
            EstadoDeSesion.VencidaPorInactividad => MotivoDeCierre.Inactividad,
            EstadoDeSesion.VencidaPorVidaMaxima => MotivoDeCierre.VidaMaxima,
            _ => (MotivoDeCierre?)null,
        };
        if (motivo is null)
            return sesion;

        await CerrarAsync(clave, cancellationToken);
        throw new SesionTerminadaException(motivo.Value);
    }

    private bool NecesitaRenovar(SesionDelNavegador sesion) =>
        sesion.Tokens.AccessTokenVence - reloj.GetUtcNow() < MargenDeRenovacion;

    /// <summary>Renueva y guarda los tokens nuevos. Si Keycloak rechaza el refresh token, la sesión ya no existe allí (CL-12).</summary>
    private async Task RenovarAsync(SesionDelNavegador sesion, CancellationToken cancellationToken)
    {
        try
        {
            var nuevos = await identidad.RenovarAsync(sesion.Area, sesion.Tokens.RefreshToken, cancellationToken);
            sesion.ReemplazarTokens(new TokensDeLaSesion(
                nuevos.AccessToken, nuevos.RefreshToken, nuevos.IdToken ?? sesion.Tokens.IdToken, nuevos.AccessTokenVence));
        }
        catch (RenovacionRechazadaException)
        {
            // No hay nada que cerrar en Keycloak: se borra solo la sesión del BFF.
            almacen.Eliminar(sesion.Clave);
            log.LogInformation("Keycloak rechazó la renovación de una sesión de {Area}: se cierra como revocada.",
                AreasDeSesion.Segmento(sesion.Area));
            throw new SesionTerminadaException(MotivoDeCierre.Revocada);
        }
    }

    private SesionDto ADto(SesionDelNavegador sesion)
    {
        var plazos = PoliticaDeSesion.Plazos(sesion, opciones.Value.De(sesion.Area), reloj.GetUtcNow());
        return new SesionDto(
            AreasDeSesion.Segmento(sesion.Area),
            sesion.Usuario,
            sesion.Rol?.ToString(),
            (int)plazos.HastaInactividad.TotalSeconds,
            (int)plazos.HastaVidaMaxima.TotalSeconds);
    }
}
