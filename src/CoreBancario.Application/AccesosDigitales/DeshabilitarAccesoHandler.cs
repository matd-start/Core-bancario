using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

public sealed class DeshabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IAccesoDigitalRepositorio accesos, IRegistroDeAccesos registro,
    IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <exception cref="RecursoNoEncontradoException">Cliente inexistente.</exception>
    /// <exception cref="AccesoNoHabilitadoException">CL-14 (sin escribir la solicitud).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException"/>
    /// <exception cref="ErrorDelProveedorDeIdentidadException"/>
    /// <exception cref="ConflictoDeConcurrenciaException"/>
    public async Task<RegistroDeAccesoDto> EjecutarAsync(DeshabilitarAccesoComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var cliente = await clientes.ObtenerAsync(comando.ClienteId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe un cliente con ese identificador.");

        var acceso = await localizador.LocalizarAsync(cliente, cancellationToken);
        await reconciliador.ReconciliarAsync(cliente.Id, acceso.Usuario, cancellationToken);

        // Si el acceso no está habilitado, ParaDeshabilitar lanza antes de crear nada (CL-14, CA-04).
        var solicitud = SolicitudDeAcceso.ParaDeshabilitar(
            cliente.Id, comando.Operador, acceso.Estado, reloj.AhoraEnMicrosegundos());
        registro.AgregarSolicitud(solicitud);
        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);

        // Habilitado implica que el usuario existe: ParaDeshabilitar lanzó si no.
        var usuario = acceso.Usuario
            ?? throw new InvalidOperationException("Un acceso Habilitado debe tener usuario en el sistema de identidad.");
        await AplicarAsync(usuario, solicitud);

        // Un usuario adoptado (sin vínculo) queda vinculado, igual que al habilitar.
        if (acceso.Vinculo is null)
            accesos.Agregar(AccesoDigital.Vincular(cliente.Id, usuario.Sub, reloj.AhoraEnMicrosegundos()));

        registro.AgregarResultado(solicitud.Completar(reloj.AhoraEnMicrosegundos()));
        await unidadDeTrabajo.GuardarCambiosAsync(CancellationToken.None);

        return new RegistroDeAccesoDto(solicitud.Id, cliente.Id, solicitud.Accion.ToString(), solicitud.Fecha);
    }

    private async Task AplicarAsync(UsuarioDeIdentidad usuario, SolicitudDeAcceso solicitud)
    {
        try
        {
            await proveedor.DeshabilitarAsync(usuario.Sub, solicitud.Id, CancellationToken.None);
        }
        catch (ErrorDelProveedorDeIdentidadException error)
        {
            registro.AgregarResultado(solicitud.Fallar(reloj.AhoraEnMicrosegundos(), error.Motivo));
            await unidadDeTrabajo.GuardarCambiosAsync(CancellationToken.None);
            throw;
        }
    }
}
