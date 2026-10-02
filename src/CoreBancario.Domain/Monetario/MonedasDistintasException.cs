namespace CoreBancario.Domain.Monetario;

/// <summary>RN-03: no se mezclan monedas sin conversión.</summary>
public sealed class MonedasDistintasException : ReglaDeNegocioException
{
    public MonedasDistintasException(Moneda esperada, Moneda recibida)
        : base($"No se pueden mezclar monedas sin conversión: se esperaba {esperada} y se recibió {recibida}.") { }
}
