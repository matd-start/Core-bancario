using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>
/// Todo lo que necesitan los handlers de habilitar y deshabilitar, cableado con fakes (ADR-0017). Un escenario por
/// prueba. Los repositorios y el registro "confirman" sus filas cuando la unidad de trabajo guarda, así las pruebas
/// distinguen lo agregado de lo guardado.
/// </summary>
internal sealed class EscenarioDeAcceso
{
    public const string Operador = "operador-de-pruebas";

    public EscenarioDeAcceso(UnidadDeTrabajoEspia? unidad = null, params ClaveTemporal[] claves)
    {
        Unidad = unidad ?? new UnidadDeTrabajoEspia();
        Vinculos = new AccesoDigitalRepositorioEnMemoria(Unidad);
        Registro = new RegistroDeAccesosEnMemoria(Unidad);
        Generador = new GeneradorDeClaveDeSecuencia(claves.Length > 0 ? claves : [Clave("A")]);
        Clientes.Agregar(Cliente);

        var localizador = new LocalizadorDeAcceso(Vinculos, Proveedor);
        var reconciliador = new ReconciliadorDeSolicitudes(Registro, Unidad, Datos.Reloj);
        Habilitar = new HabilitarAccesoHandler(Clientes, localizador, reconciliador, Proveedor, Generador, Vinculos,
            Registro, Unidad, Datos.Reloj);
        Deshabilitar = new DeshabilitarAccesoHandler(Clientes, localizador, reconciliador, Proveedor, Vinculos,
            Registro, Unidad, Datos.Reloj);
        ObtenerEstado = new ObtenerEstadoDeAccesoHandler(Clientes, localizador);
    }

    public UnidadDeTrabajoEspia Unidad { get; }

    public ClienteRepositorioEnMemoria Clientes { get; } = new();

    public AccesoDigitalRepositorioEnMemoria Vinculos { get; }

    public RegistroDeAccesosEnMemoria Registro { get; }

    public ProveedorDeIdentidadEnMemoria Proveedor { get; } = new();

    public GeneradorDeClaveDeSecuencia Generador { get; }

    public HabilitarAccesoHandler Habilitar { get; }

    public DeshabilitarAccesoHandler Deshabilitar { get; }

    public ObtenerEstadoDeAccesoHandler ObtenerEstado { get; }

    public ObtenerEstadoDeAccesoConsulta ConsultaDeEstado() => new(Cliente.Id);

    /// <summary>El cliente registrado sobre el que opera el escenario.</summary>
    public Cliente Cliente { get; } = Datos.Cliente();

    /// <summary>Con el que Keycloak conoce al cliente (RN-17): tipo y número normalizados.</summary>
    public string NombreDeUsuario => AccesoDigital.NombreDeUsuarioDe(Cliente.Documento);

    public HabilitarAccesoComando ComandoDeHabilitar(string operador = Operador) => new(Cliente.Id, operador);

    public DeshabilitarAccesoComando ComandoDeDeshabilitar(string operador = Operador) => new(Cliente.Id, operador);

    /// <summary>Un usuario que ya existía en el proveedor, con o sin su vínculo guardado en la base.</summary>
    public ProveedorDeIdentidadEnMemoria.UsuarioSimulado SembrarUsuario(
        bool habilitado = true, bool otpInscrito = false, bool conVinculo = true, Guid? marca = null,
        bool primerIngresoPendiente = false)
    {
        var usuario = Proveedor.Sembrar(NombreDeUsuario, habilitado, otpInscrito, marca, claveTemporal: "clave-anterior-1234",
            primerIngresoPendiente: primerIngresoPendiente);
        if (conVinculo)
            Vinculos.Sembrar(AccesoDigital.Vincular(Cliente.Id, usuario.Sub, Datos.Ahora.AddDays(-1)));

        return usuario;
    }

    /// <summary>Una clave temporal válida (16 caracteres) que se distingue por su sufijo.</summary>
    public static ClaveTemporal Clave(string sufijo) => new($"Clave-temporal-{sufijo}".PadRight(ClaveTemporal.LongitudMinima, 'x'));
}
