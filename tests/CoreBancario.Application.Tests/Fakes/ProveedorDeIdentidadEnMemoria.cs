using CoreBancario.Application.AccesosDigitales;

namespace CoreBancario.Application.Tests.Fakes;

/// <summary>
/// Un sistema de identidad en memoria (ADR-0017). Guarda usuarios por sub, registra las llamadas y ofrece tres modos de
/// falla para las operaciones que CAMBIAN algo (crear, reiniciar, deshabilitar):
///   ResponderConError(): no aplica nada y lanza ErrorDelProveedorDeIdentidadException (respondió con un estado de error).
///   NoResponder(): no aplica nada y lanza ProveedorDeIdentidadSinRespuestaException (el cambio no llegó).
///   AplicarYNoResponder(): APLICA el cambio con su marca y luego lanza SinRespuesta (llegó, pero no se supo): el caso que importa.
/// NoResponder(tambienAlLeer: true) hace que también fallen las lecturas (el proveedor está caído al localizar).
/// </summary>
public sealed class ProveedorDeIdentidadEnMemoria : IProveedorDeIdentidad
{
    private enum Modo
    {
        Normal,
        ConError,
        SinRespuesta,
        AplicarYNoResponder,
    }

    private readonly Dictionary<string, UsuarioSimulado> _usuarios = [];
    private readonly List<string> _llamadas = [];
    private readonly List<CancellationToken> _tokens = [];
    private Modo _modo = Modo.Normal;
    private bool _fallanLasLecturas;
    private bool _lecturasConError;

    /// <summary>Lo que el proveedor sabe de un usuario. NombreDeUsuario va en minúsculas, como lo guarda Keycloak.</summary>
    public sealed class UsuarioSimulado(string sub, string nombreDeUsuario)
    {
        public string Sub { get; } = sub;

        public string NombreDeUsuario { get; } = nombreDeUsuario.ToLowerInvariant();

        public bool Habilitado { get; set; } = true;

        public bool OtpInscrito { get; set; }

        /// <summary>La última clave temporal que se le puso. null si nunca tuvo una.</summary>
        public string? ClaveTemporal { get; set; }

        public int VecesQueSeCerraronSusSesiones { get; set; }

        /// <summary>La marca de ADR-0017: la Id de la última solicitud que lo cambió.</summary>
        public Guid? Marca { get; set; }

        public List<string> AccionesObligatorias { get; } = [];
    }

    /// <summary>Nombre de cada llamada, en orden: "BuscarPorNombreDeUsuarioAsync", "ObtenerAsync", "CrearAsync"…</summary>
    public IReadOnlyList<string> Llamadas => _llamadas;

    /// <summary>El CancellationToken de cada llamada que cambia algo, en orden.</summary>
    public IReadOnlyList<CancellationToken> TokensDeLosCambios => _tokens;

    /// <summary>Se ejecuta justo al empezar cada llamada que cambia algo, antes de aplicarla.</summary>
    public Action? AlEmpezarUnCambio { get; set; }

    public int Cambios => _llamadas.Count(l => l is nameof(CrearAsync) or nameof(ReiniciarAccesoAsync) or nameof(DeshabilitarAsync));

    public IReadOnlyCollection<UsuarioSimulado> Usuarios => _usuarios.Values;

    /// <summary>tambienAlLeer: true hace que las lecturas también respondan con error (ErrorDelProveedorDeIdentidadException).</summary>
    public void ResponderConError(bool tambienAlLeer = false)
    {
        _modo = Modo.ConError;
        _lecturasConError = tambienAlLeer;
    }

    public void NoResponder(bool tambienAlLeer = false)
    {
        _modo = Modo.SinRespuesta;
        _fallanLasLecturas = tambienAlLeer;
    }

    public void AplicarYNoResponder() => _modo = Modo.AplicarYNoResponder;

    public void Normalizar()
    {
        _modo = Modo.Normal;
        _fallanLasLecturas = false;
        _lecturasConError = false;
    }

    /// <summary>Prepara un usuario que ya existía antes de la operación.</summary>
    public UsuarioSimulado Sembrar(string nombreDeUsuario, bool habilitado = true, bool otpInscrito = false,
        Guid? marca = null, string? claveTemporal = null, bool primerIngresoPendiente = false)
    {
        var usuario = new UsuarioSimulado(Guid.NewGuid().ToString(), nombreDeUsuario)
        {
            Habilitado = habilitado,
            OtpInscrito = otpInscrito,
            Marca = marca,
            ClaveTemporal = claveTemporal,
        };
        if (primerIngresoPendiente)
            usuario.AccionesObligatorias.AddRange(["UPDATE_PASSWORD", "CONFIGURE_TOTP"]);

        _usuarios[usuario.Sub] = usuario;
        return usuario;
    }

    public UsuarioSimulado? PorNombre(string nombreDeUsuario) =>
        _usuarios.Values.SingleOrDefault(u => u.NombreDeUsuario == nombreDeUsuario.ToLowerInvariant());

    public Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken)
    {
        _llamadas.Add(nameof(BuscarPorNombreDeUsuarioAsync));
        FallarSiElProveedorEstaCaido();
        return Task.FromResult(Representar(PorNombre(nombreDeUsuario)));
    }

    public Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken)
    {
        _llamadas.Add(nameof(ObtenerAsync));
        FallarSiElProveedorEstaCaido();
        return Task.FromResult(Representar(_usuarios.GetValueOrDefault(sub)));
    }

    public Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        var sub = Guid.NewGuid().ToString();
        Cambiar(nameof(CrearAsync), cancellationToken, () =>
        {
            var usuario = new UsuarioSimulado(sub, nombreDeUsuario) { ClaveTemporal = claveTemporal.Valor, Marca = marca };
            usuario.AccionesObligatorias.AddRange(["UPDATE_PASSWORD", "CONFIGURE_TOTP"]);
            _usuarios[sub] = usuario;
        });
        return Task.FromResult(sub);
    }

    public Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        Cambiar(nameof(ReiniciarAccesoAsync), cancellationToken, () =>
        {
            var usuario = _usuarios[sub];
            usuario.ClaveTemporal = claveTemporal.Valor;
            usuario.OtpInscrito = false;
            usuario.VecesQueSeCerraronSusSesiones++;
            usuario.Habilitado = true;
            usuario.AccionesObligatorias.Clear();
            usuario.AccionesObligatorias.AddRange(["UPDATE_PASSWORD", "CONFIGURE_TOTP"]);
            usuario.Marca = marca;
        });
        return Task.CompletedTask;
    }

    public Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken)
    {
        Cambiar(nameof(DeshabilitarAsync), cancellationToken, () =>
        {
            var usuario = _usuarios[sub];
            usuario.VecesQueSeCerraronSusSesiones++;
            usuario.Habilitado = false;
            usuario.Marca = marca;
        });
        return Task.CompletedTask;
    }

    private void Cambiar(string operacion, CancellationToken cancellationToken, Action aplicar)
    {
        _llamadas.Add(operacion);
        _tokens.Add(cancellationToken);
        AlEmpezarUnCambio?.Invoke();

        switch (_modo)
        {
            case Modo.ConError:
                throw new ErrorDelProveedorDeIdentidadException($"HTTP 500 al {operacion}");
            case Modo.SinRespuesta:
                throw new ProveedorDeIdentidadSinRespuestaException();
            case Modo.AplicarYNoResponder:
                aplicar();
                throw new ProveedorDeIdentidadSinRespuestaException();
            default:
                aplicar();
                break;
        }
    }

    private void FallarSiElProveedorEstaCaido()
    {
        if (_fallanLasLecturas)
            throw new ProveedorDeIdentidadSinRespuestaException();

        if (_lecturasConError)
            throw new ErrorDelProveedorDeIdentidadException("HTTP 500 al leer");
    }

    private static UsuarioDeIdentidad? Representar(UsuarioSimulado? usuario) =>
        usuario is null ? null : new UsuarioDeIdentidad(usuario.Sub, usuario.Habilitado, usuario.Marca,
            usuario.AccionesObligatorias.Any(a => a is "UPDATE_PASSWORD" or "CONFIGURE_TOTP"));
}
