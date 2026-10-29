using CoreBancario.Application.Clientes;

namespace CoreBancario.Application.AccesosDigitales;

// SDD: esqueleto creado por test-writer
#pragma warning disable CS9113 // Los parámetros se usan cuando se implemente (T21).
/// <summary>RF-16. Solo lectura: no escribe ni reconcilia. El estado sale de LocalizadorDeAcceso (Keycloak es la fuente, ADR-0017).</summary>
public sealed class ObtenerEstadoDeAccesoHandler(IClienteRepositorio clientes, LocalizadorDeAcceso localizador)
{
    public Task<EstadoDeAccesoDto> EjecutarAsync(ObtenerEstadoDeAccesoConsulta consulta, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
