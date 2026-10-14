using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Registro de accesos (RN-20): solo inserciones. No hay métodos para editar ni borrar.</summary>
public interface IRegistroDeAccesos
{
    void AgregarSolicitud(SolicitudDeAcceso solicitud);

    void AgregarResultado(ResultadoDeSolicitud resultado);

    /// <summary>Solicitudes del cliente que todavía no tienen resultado, de la más antigua a la más reciente.</summary>
    Task<IReadOnlyList<SolicitudDeAcceso>> ListarPendientesAsync(Guid clienteId, CancellationToken cancellationToken);
}
