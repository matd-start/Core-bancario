using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;

namespace CoreBancario.Bff.Tests;

public enum ModoDeIdentidad
{
    /// <summary>Renueva con tokens nuevos.</summary>
    Acepta,

    /// <summary>400 invalid_grant: RenovacionRechazadaException.</summary>
    Rechaza,

    /// <summary>Sin respuesta (red o 5xx): IdentidadSinRespuestaException.</summary>
    NoResponde,
}

/// <summary>
/// El IClienteDeIdentidad de las pruebas del BFF (plan, seccion 7): acepta, rechaza o no responde, puede estar no
/// disponible y cuenta las renovaciones y los cierres. Guarda los refresh tokens que recibio.
/// </summary>
public sealed class ClienteDeIdentidadFalso(TimeProvider reloj) : IClienteDeIdentidad
{
    private readonly object _candado = new();
    private readonly List<string> _refreshRenovados = [];
    private readonly List<string> _refreshCerrados = [];

    public ModoDeIdentidad Modo { get; set; } = ModoDeIdentidad.Acepta;

    public bool Disponible { get; set; } = true;

    public TimeSpan VidaDelAccessToken { get; set; } = TimeSpan.FromSeconds(270);

    /// <summary>Espera real dentro de RenovarAsync, para provocar renovaciones simultaneas.</summary>
    public TimeSpan Demora { get; set; } = TimeSpan.Zero;

    public int Renovaciones { get { lock (_candado) return _refreshRenovados.Count; } }

    public int Cierres { get { lock (_candado) return _refreshCerrados.Count; } }

    public IReadOnlyList<string> RefreshTokensRenovados { get { lock (_candado) return [.. _refreshRenovados]; } }

    public IReadOnlyList<string> RefreshTokensCerrados { get { lock (_candado) return [.. _refreshCerrados]; } }

    public async Task<TokensRenovados> RenovarAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken)
    {
        int numero;
        lock (_candado)
        {
            _refreshRenovados.Add(refreshToken);
            numero = _refreshRenovados.Count;
        }

        if (Demora > TimeSpan.Zero)
            await Task.Delay(Demora, cancellationToken);

        return Modo switch
        {
            ModoDeIdentidad.Rechaza => throw new RenovacionRechazadaException(),
            ModoDeIdentidad.NoResponde => throw new IdentidadSinRespuestaException(),
            _ => new TokensRenovados($"access-renovado-{numero}", $"refresh-renovado-{numero}", null,
                reloj.GetUtcNow() + VidaDelAccessToken),
        };
    }

    public Task CerrarSesionAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken)
    {
        lock (_candado)
            _refreshCerrados.Add(refreshToken);

        return Task.CompletedTask;
    }

    public Task<bool> EstaDisponibleAsync(AreaDeSesion area, CancellationToken cancellationToken) =>
        Task.FromResult(Disponible);
}
