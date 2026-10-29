using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

/// <summary>RF-16. Solo lectura: no escribe ni reconcilia. El estado sale de LocalizadorDeAcceso (Keycloak es la fuente, ADR-0017).</summary>
public sealed class ObtenerEstadoDeAccesoHandler(IClienteRepositorio clientes, LocalizadorDeAcceso localizador)
{
    /// <exception cref="RecursoNoEncontradoException">Cliente inexistente (no se consulta al proveedor).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException"/>
    /// <exception cref="ErrorDelProveedorDeIdentidadException"/>
    public async Task<EstadoDeAccesoDto> EjecutarAsync(ObtenerEstadoDeAccesoConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var cliente = await clientes.ObtenerAsync(consulta.ClienteId, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe un cliente con ese identificador.");

        var acceso = await localizador.LocalizarAsync(cliente, cancellationToken);

        // El primer ingreso solo tiene sentido con el acceso Habilitado; en otro caso es null.
        bool? primerIngresoPendiente = acceso.Estado == EstadoDeAcceso.Habilitado
            ? acceso.Usuario?.PrimerIngresoPendiente
            : null;

        return new EstadoDeAccesoDto(cliente.Id, acceso.Estado.ToString(), primerIngresoPendiente);
    }
}
