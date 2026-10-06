using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

/// <summary>Fuente de números de cuenta no predecibles (RN-18). Interfaz por CL-11: las pruebas fuerzan una colisión.</summary>
public interface IGeneradorDeNumeroDeCuenta
{
    NumeroDeCuenta Generar();
}
