using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

/// <summary>
/// Nombres o apellidos. Se quitan espacios al inicio y al final y se normaliza a Unicode NFC (string.Normalize()).
/// Válido: de 1 a 100 caracteres; solo letras (char.IsLetter, incluye tildes y ñ), espacios, apóstrofo (') y guion (-);
/// con al menos una letra (CL-06).
/// </summary>
public sealed record NombreDePersona
{
    private const int LongitudMaxima = 100;

    public string Valor { get; }

    private NombreDePersona(string valor) => Valor = valor;

    public static bool TryCrear(string? valor, [NotNullWhen(true)] out NombreDePersona? nombre)
    {
        nombre = null;

        if (valor is null)
            return false;

        string normalizado;
        try
        {
            normalizado = valor.Trim().Normalize();
        }
        catch (ArgumentException)
        {
            // Texto con caracteres Unicode inválidos (por ejemplo un sustituto suelto): no es un nombre.
            return false;
        }

        if (normalizado.Length is 0 or > LongitudMaxima)
            return false;

        if (!normalizado.All(c => char.IsLetter(c) || c is ' ' or '\'' or '-'))
            return false;

        if (!normalizado.Any(char.IsLetter))
            return false;

        nombre = new NombreDePersona(normalizado);
        return true;
    }

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static NombreDePersona Crear(string valor)
    {
        if (!TryCrear(valor, out var nombre))
            throw new ArgumentException("El nombre no cumple el formato esperado.", nameof(valor));

        return nombre;
    }

    public override string ToString() => Valor;
}
