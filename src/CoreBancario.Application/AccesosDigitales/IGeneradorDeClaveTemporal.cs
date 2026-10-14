namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Fuente de claves temporales aleatorias. Puerto por CA-02 (las pruebas fijan la secuencia) y porque el azar es infraestructura.</summary>
public interface IGeneradorDeClaveTemporal
{
    ClaveTemporal Generar();
}
