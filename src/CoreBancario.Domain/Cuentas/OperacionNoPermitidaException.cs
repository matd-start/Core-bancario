namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-05: una cuenta Bloqueada rechaza débitos; una Cerrada rechaza todo.</summary>
public sealed class OperacionNoPermitidaException : ReglaDeNegocioException
{
    public OperacionNoPermitidaException(EstadoCuenta estado, TipoMovimiento operacion)
        : base($"Una cuenta {estado} no admite la operación {operacion}.") { }
}
