namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-14 (y RN-06): transición de estado no permitida, incluida la que repite el estado.</summary>
public sealed class TransicionNoPermitidaException : ReglaDeNegocioException
{
    public TransicionNoPermitidaException(EstadoCuenta desde, EstadoCuenta hacia)
        : base($"Transición no permitida: de {desde} a {hacia}.") { }
}
