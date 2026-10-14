namespace CoreBancario.Application.AccesosDigitales;

/// <summary>
/// Clave de un solo uso para el primer ingreso (sección 4 de la spec). Clase y no record a propósito: ToString NUNCA
/// devuelve el valor, para que un log estructurado o una interpolación descuidada no la filtre (RNF-03).
/// </summary>
// SDD: esqueleto creado por test-writer
public sealed class ClaveTemporal
{
    public const int LongitudMinima = 16;

    public string Valor { get; }

    /// <exception cref="ArgumentException">valor es null o tiene menos de 16 caracteres.</exception>
    public ClaveTemporal(string valor) => throw new NotImplementedException();

    /// <returns>"[clave temporal oculta]".</returns>
    public override string ToString() => throw new NotImplementedException();
}
