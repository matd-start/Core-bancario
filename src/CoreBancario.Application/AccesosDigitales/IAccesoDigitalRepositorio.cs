using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

public interface IAccesoDigitalRepositorio
{
    void Agregar(AccesoDigital acceso);

    Task<AccesoDigital?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken cancellationToken);
}
