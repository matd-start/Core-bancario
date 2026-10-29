using System.Collections.Concurrent;
using CoreBancario.Application.AccesosDigitales;

namespace CoreBancario.Api.Tests;

/// <summary>Cómo falla el sistema de identidad falso en las operaciones que cambian algo.</summary>
public enum FallaDelProveedor
{
    Ninguna,

    /// <summary>Responde con un error: no aplica nada y lanza ErrorDelProveedorDeIdentidadException (502).</summary>
    ConError,

    /// <summary>No responde: no aplica nada y lanza ProveedorDeIdentidadSinRespuestaException (503).</summary>
    SinRespuesta,

    /// <summary>Aplica el cambio con su marca y luego no responde (503): el caso que reconcilia el reintento.</summary>
    AplicaYNoResponde,
}

/// <summary>
/// El sistema de identidad de la API en las pruebas (ADR-0018, RNF-08: las demás pruebas de la API no arrancan Keycloak).
/// Guarda usuarios por sub, seguro para varios hilos porque la API de pruebas atiende peticiones en paralelo. Un duplicado
/// pequeño del fake de Application.Tests: copiar un fake pequeño entre dos proyectos es aceptable (DRY a la tercera).
/// </summary>
public sealed class ProveedorDeIdentidadFalso(FallaDelProveedor falla = FallaDelProveedor.Ninguna) : IProveedorDeIdentidad
{
    /// <summary>Se puede cambiar entre dos peticiones: por ejemplo, para que el reintento sí tenga respuesta.</summary>
    public FallaDelProveedor Falla { get; set; } = falla;

    /// <summary>
    /// Cómo fallan las LECTURAS (RF-16, CL-20): ConError lanza ErrorDelProveedorDeIdentidadException (502), SinRespuesta
    /// lanza ProveedorDeIdentidadSinRespuestaException (503). AplicaYNoResponde no tiene sentido al leer: se trata como SinRespuesta.
    /// </summary>
    public FallaDelProveedor FallaAlLeer { get; set; } = FallaDelProveedor.Ninguna;

    public sealed class UsuarioFalso(string sub, string nombreDeUsuario)
    {
        public string Sub { get; } = sub;

        public string NombreDeUsuario { get; } = nombreDeUsuario.ToLowerInvariant();

        public bool Habilitado { get; set; } = true;

        public bool OtpInscrito { get; set; }

        public string? ClaveTemporal { get; set; }

        public Guid? Marca { get; set; }

        public int SesionesCerradas { get; set; }

        /// <summary>true mientras tenga UPDATE_PASSWORD o CONFIGURE_TOTP pendientes (RF-16). Crear y reiniciar lo ponen en true.</summary>
        public bool PrimerIngresoPendiente { get; set; }
    }

    private readonly ConcurrentDictionary<string, UsuarioFalso> _usuarios = new();

    public IEnumerable<UsuarioFalso> Usuarios => _usuarios.Values;

    public UsuarioFalso? PorNombre(string nombreDeUsuario) =>
        _usuarios.Values.SingleOrDefault(u => u.NombreDeUsuario == nombreDeUsuario.ToLowerInvariant());

    /// <summary>Prepara un usuario que ya existía antes de la operación (por ejemplo, ya con el segundo factor inscrito).</summary>
    public UsuarioFalso Sembrar(string nombreDeUsuario, bool habilitado = true, bool otpInscrito = false,
        bool primerIngresoPendiente = false)
    {
        var usuario = new UsuarioFalso(Guid.NewGuid().ToString(), nombreDeUsuario)
        {
            Habilitado = habilitado,
            OtpInscrito = otpInscrito,
            PrimerIngresoPendiente = primerIngresoPendiente,
        };
        _usuarios[usuario.Sub] = usuario;
        return usuario;
    }

    public Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken)
    {
        FallarSiNoSePuedeLeer();
        return Task.FromResult(Representar(PorNombre(nombreDeUsuario)));
    }

    public Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken)
    {
        FallarSiNoSePuedeLeer();
        return Task.FromResult(Representar(_usuarios.GetValueOrDefault(sub)));
    }

    public Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        var sub = Guid.NewGuid().ToString();
        Cambiar(() => _usuarios[sub] = new UsuarioFalso(sub, nombreDeUsuario)
        {
            ClaveTemporal = claveTemporal.Valor,
            Marca = marca,
            PrimerIngresoPendiente = true,
        });
        return Task.FromResult(sub);
    }

    public Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        Cambiar(() =>
        {
            var usuario = _usuarios[sub];
            usuario.ClaveTemporal = claveTemporal.Valor;
            usuario.OtpInscrito = false;
            usuario.SesionesCerradas++;
            usuario.Habilitado = true;
            usuario.PrimerIngresoPendiente = true;
            usuario.Marca = marca;
        });
        return Task.CompletedTask;
    }

    public Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken)
    {
        Cambiar(() =>
        {
            var usuario = _usuarios[sub];
            usuario.SesionesCerradas++;
            usuario.Habilitado = false;
            usuario.Marca = marca;
        });
        return Task.CompletedTask;
    }

    private void FallarSiNoSePuedeLeer()
    {
        switch (FallaAlLeer)
        {
            case FallaDelProveedor.ConError:
                throw new ErrorDelProveedorDeIdentidadException("HTTP 500 al leer");
            case FallaDelProveedor.SinRespuesta:
            case FallaDelProveedor.AplicaYNoResponde:
                throw new ProveedorDeIdentidadSinRespuestaException();
        }
    }

    private void Cambiar(Action aplicar)
    {
        switch (Falla)
        {
            case FallaDelProveedor.ConError:
                throw new ErrorDelProveedorDeIdentidadException("HTTP 500 al aplicar el cambio");
            case FallaDelProveedor.SinRespuesta:
                throw new ProveedorDeIdentidadSinRespuestaException();
            case FallaDelProveedor.AplicaYNoResponde:
                aplicar();
                throw new ProveedorDeIdentidadSinRespuestaException();
            default:
                aplicar();
                break;
        }
    }

    private static UsuarioDeIdentidad? Representar(UsuarioFalso? usuario) =>
        usuario is null ? null : new UsuarioDeIdentidad(usuario.Sub, usuario.Habilitado, usuario.Marca, usuario.PrimerIngresoPendiente);
}
