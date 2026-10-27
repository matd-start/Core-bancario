using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Sesiones;

/// <summary>
/// Almacén EN MEMORIA de las sesiones (ADR-0020). Implementa ITicketStore para el manejador de cookies: la cookie solo
/// lleva la clave. Cada entrada se libera sola al cumplirse Inicio + VidaMaxima. Singleton.
/// </summary>
public sealed class AlmacenDeSesiones(TimeProvider reloj, IOptions<OpcionesDelBff> opciones) : ITicketStore
{
    /// <summary>
    /// Pasada la vida máxima la sesión deja de existir para <see cref="Obtener"/>, pero la entrada se conserva un minuto
    /// más para que el servicio pueda decir "vida máxima" y no "sin sesión" a una petición que llega justo después.
    /// </summary>
    private static readonly TimeSpan RetencionTrasLaVidaMaxima = TimeSpan.FromMinutes(1);

    private const string NombreDelTokenDeAcceso = "access_token";
    private const string NombreDelTokenDeRefresco = "refresh_token";
    private const string NombreDelTokenDeIdentidad = "id_token";
    private const string NombreDeLaCaducidad = "expires_at";

    private readonly ConcurrentDictionary<string, Entrada> _entradas = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _candados = new();

    private sealed class Entrada(SesionDelNavegador sesion, DateTimeOffset venceLaVidaMaxima)
    {
        public SesionDelNavegador Sesion { get; } = sesion;

        public DateTimeOffset VenceLaVidaMaxima { get; } = venceLaVidaMaxima;

        public ITimer? Temporizador { get; set; }
    }

    /// <summary>Clave opaca: 32 bytes aleatorios en Base64Url.</summary>
    public static string NuevaClave() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    // ── ITicketStore (lo usa el manejador de cookies) ──

    /// <summary>
    /// Construye la SesionDelNavegador desde el ticket del regreso OIDC (tokens de SaveTokens; área, inicio y última
    /// actividad de PropiedadesDeSesion; sub, preferred_username, sid y auth_time de los claims; rol con LectorDeRol).
    /// Los tokens NO quedan en el ticket que guarda.
    /// </summary>
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var clave = NuevaClave();
        Agregar(Construir(clave, ticket));
        return Task.FromResult(clave);
    }

    /// <summary>
    /// El manejador de cookies llama a Renew (y no a Store) cuando ya conoce la clave de la petición. Como TicketReceived
    /// descarta la sesión anterior antes de guardar la nueva, en un inicio de sesión normal esto no ocurre y se llama a Store con
    /// una clave nueva. Si aun así llega un ticket con tokens, solo reemplaza la sesión de la MISMA identidad (mismo sid y
    /// misma área): nunca guarda bajo una clave existente una sesión de otra persona. Los tickets de Retrieve no traen tokens.
    /// </summary>
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (ticket.Properties.GetTokenValue(NombreDelTokenDeAcceso) is null)
            return Task.CompletedTask;

        var nueva = Construir(key, ticket);
        if (ObtenerParaEvaluar(key) is { } actual && actual.Area == nueva.Area && actual.Sid == nueva.Sid)
            Agregar(nueva);

        return Task.CompletedTask;
    }

    /// <summary>Ticket sin tokens, con PropiedadesDeSesion.Clave = key en sus Items. null si no existe.</summary>
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        // ObtenerParaEvaluar y no Obtener: una sesión que acaba de pasar su vida máxima debe llegar al servicio para que
        // responda 401 con el motivo correcto, en vez de parecer una cookie desconocida.
        var sesion = ObtenerParaEvaluar(key);
        if (sesion is null)
            return Task.FromResult<AuthenticationTicket?>(null);

        var propiedades = new AuthenticationProperties();
        propiedades.Items[PropiedadesDeSesion.Area] = AreasDeSesion.Segmento(sesion.Area);
        propiedades.Items[PropiedadesDeSesion.Clave] = key;
        var ticket = new AuthenticationTicket(sesion.Principal, propiedades, ConfiguracionDeSesion.EsquemaDeCookie);
        return Task.FromResult<AuthenticationTicket?>(ticket);
    }

    public Task RemoveAsync(string key)
    {
        Eliminar(key);
        return Task.CompletedTask;
    }

    // ── Para el BFF ──

    public void Agregar(SesionDelNavegador sesion)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        var vence = sesion.Inicio + opciones.Value.De(sesion.Area).VidaMaxima;
        var tiempoRestante = vence + RetencionTrasLaVidaMaxima - reloj.GetUtcNow();
        if (tiempoRestante <= TimeSpan.Zero)
            return;

        var entrada = new Entrada(sesion, vence);
        entrada.Temporizador = reloj.CreateTimer(_ => Liberar(sesion.Clave, entrada), null, tiempoRestante, Timeout.InfiniteTimeSpan);
        if (_entradas.TryRemove(sesion.Clave, out var anterior))
            anterior.Temporizador?.Dispose();

        _entradas[sesion.Clave] = entrada;
    }

    /// <summary>La sesión, si existe y no ha llegado a su vida máxima. null en cualquier otro caso.</summary>
    public SesionDelNavegador? Obtener(string clave)
    {
        var entrada = Buscar(clave);
        return entrada is not null && reloj.GetUtcNow() < entrada.VenceLaVidaMaxima ? entrada.Sesion : null;
    }

    /// <summary>
    /// Como <see cref="Obtener"/>, pero también devuelve la sesión que acaba de pasar su vida máxima y aún no se libera.
    /// La usa ServicioDeSesion para poder distinguir "venció" de "no existe".
    /// </summary>
    public SesionDelNavegador? ObtenerParaEvaluar(string clave) => Buscar(clave)?.Sesion;

    public void Eliminar(string clave)
    {
        if (_entradas.TryRemove(clave, out var entrada))
            entrada.Temporizador?.Dispose();

        // Sin Dispose: otra petición puede tener o estar a punto de tomar este semáforo, y su Release lanzaría
        // ObjectDisposedException. Sin AvailableWaitHandle no retiene recursos no administrados: lo recupera el recolector.
        _candados.TryRemove(clave, out _);
    }

    /// <summary>Serializa las operaciones sobre UNA sesión (renovaciones con rotación, maxReuse 0).</summary>
    public async Task<T> ConCandadoAsync<T>(string clave, Func<Task<T>> operacion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        var candado = _candados.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(cancellationToken);
        try
        {
            return await operacion();
        }
        finally
        {
            candado.Release();
        }
    }

    private Entrada? Buscar(string clave) => _entradas.TryGetValue(clave, out var entrada) ? entrada : null;

    private void Liberar(string clave, Entrada entrada)
    {
        // Solo si la entrada sigue siendo ésta: una sesión nueva con la misma clave no se toca.
        if (_entradas.TryRemove(new KeyValuePair<string, Entrada>(clave, entrada)))
        {
            entrada.Temporizador?.Dispose();
            _candados.TryRemove(clave, out _);
        }
    }

    private SesionDelNavegador Construir(string clave, AuthenticationTicket ticket)
    {
        var propiedades = ticket.Properties;
        var principal = ticket.Principal;
        var ahora = reloj.GetUtcNow();

        if (!AreasDeSesion.TryParse(Item(propiedades, PropiedadesDeSesion.Area), out var area))
            throw new InvalidOperationException("El ticket de inicio de sesión no indica el área.");

        var access = propiedades.GetTokenValue(NombreDelTokenDeAcceso) ?? "";
        var tokens = new TokensDeLaSesion(
            access,
            propiedades.GetTokenValue(NombreDelTokenDeRefresco) ?? "",
            propiedades.GetTokenValue(NombreDelTokenDeIdentidad) ?? "",
            LeerFecha(propiedades.GetTokenValue(NombreDeLaCaducidad)) ?? ahora);

        var inicio = LeerFecha(Item(propiedades, PropiedadesDeSesion.Inicio)) ?? InicioSegunAuthTime(principal, ahora);

        return new SesionDelNavegador(
            clave,
            area,
            principal.FindFirst("sub")?.Value ?? "",
            principal.FindFirst("preferred_username")?.Value ?? "",
            principal.FindFirst("sid")?.Value ?? "",
            LectorDeRol.Desde(area, access),
            inicio,
            ahora > inicio ? ahora : inicio,
            tokens,
            principal);
    }

    private static string? Item(AuthenticationProperties propiedades, string nombre) =>
        propiedades.Items.TryGetValue(nombre, out var valor) ? valor : null;

    private static DateTimeOffset? LeerFecha(string? texto) =>
        DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha) ? fecha : null;

    /// <summary>
    /// auth_time tiene resolución de segundos: un inicio de sesión que acaba de ocurrir (dentro del último segundo, o con
    /// un reloj de Keycloak ligeramente adelantado) empieza "ahora", no hasta un segundo antes por el redondeo.
    /// </summary>
    private static DateTimeOffset InicioSegunAuthTime(System.Security.Claims.ClaimsPrincipal principal, DateTimeOffset ahora)
    {
        if (!long.TryParse(principal.FindFirst("auth_time")?.Value, CultureInfo.InvariantCulture, out var segundos))
            return ahora;

        var autenticacion = DateTimeOffset.FromUnixTimeSeconds(segundos);
        return ahora - autenticacion < TimeSpan.FromSeconds(1) ? ahora : autenticacion;
    }
}
