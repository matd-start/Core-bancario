namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-04: todo monto de una operación es mayor que cero.</summary>
public sealed class MontoNoPositivoException : ReglaDeNegocioException
{
    public MontoNoPositivoException() : base("El monto de una operación debe ser mayor que cero.") { }
}
