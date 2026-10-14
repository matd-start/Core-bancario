using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.AccesosDigitales;

public sealed class HabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IGeneradorDeClaveTemporal generador, IAccesoDigitalRepositorio accesos,
    IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>Secuencia exacta en la sección 5 del plan ("Intención primero").</summary>
    /// <exception cref="RecursoNoEncontradoException">CL-12 (sin escribir nada ni llamar al proveedor).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException">Solicitud pendiente.</exception>
    /// <exception cref="ErrorDelProveedorDeIdentidadException">Solicitud con resultado Fallido.</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">Carrera con otro operador (índice único).</exception>
    public async Task<AccesoHabilitadoDto> EjecutarAsync(HabilitarAccesoComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var cliente = await clientes.ObtenerAsync(comando.ClienteId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe un cliente con ese identificador.");

        var acceso = await localizador.LocalizarAsync(cliente, cancellationToken);
        await reconciliador.ReconciliarAsync(cliente.Id, acceso.Usuario, cancellationToken);

        // Intención primero: la solicitud queda confirmada en la base ANTES de llamar al proveedor (ADR-0017).
        var solicitud = SolicitudDeAcceso.ParaHabilitar(
            cliente.Id, comando.Operador, acceso.Estado, reloj.AhoraEnMicrosegundos());
        registro.AgregarSolicitud(solicitud);
        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);

        // Desde aquí se usa CancellationToken.None: con la intención confirmada, un operador que cancela
        // no debe dejar la operación a medias. El tiempo de espera del HttpClient acota la duración.
        var clave = generador.Generar();
        var sub = await AplicarAsync(cliente, acceso, solicitud, clave);

        if (acceso.Vinculo is null)
            accesos.Agregar(AccesoDigital.Vincular(cliente.Id, sub, reloj.AhoraEnMicrosegundos()));

        registro.AgregarResultado(solicitud.Completar(reloj.AhoraEnMicrosegundos()));
        await unidadDeTrabajo.GuardarCambiosAsync(CancellationToken.None);

        return new AccesoHabilitadoDto(solicitud.Id, cliente.Id, solicitud.Accion.ToString(), clave.Valor, solicitud.Fecha);
    }

    /// <summary>Crea al usuario o reinicia su acceso. Un error del proveedor cierra la solicitud como Fallida; sin respuesta queda pendiente.</summary>
    private async Task<string> AplicarAsync(Cliente cliente, AccesoLocalizado acceso, SolicitudDeAcceso solicitud, ClaveTemporal clave)
    {
        try
        {
            if (acceso.Usuario is null)
            {
                return await proveedor.CrearAsync(
                    AccesoDigital.NombreDeUsuarioDe(cliente.Documento), clave, solicitud.Id, CancellationToken.None);
            }

            await proveedor.ReiniciarAccesoAsync(acceso.Usuario.Sub, clave, solicitud.Id, CancellationToken.None);
            return acceso.Usuario.Sub;
        }
        catch (ErrorDelProveedorDeIdentidadException error)
        {
            registro.AgregarResultado(solicitud.Fallar(reloj.AhoraEnMicrosegundos(), error.Motivo));
            await unidadDeTrabajo.GuardarCambiosAsync(CancellationToken.None);
            throw;
        }
    }
}
