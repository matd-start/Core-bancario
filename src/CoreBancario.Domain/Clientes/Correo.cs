using System.Diagnostics.CodeAnalysis;

namespace CoreBancario.Domain.Clientes;

/// <summary>
/// Se quitan espacios al inicio y al final. Válido: como máximo 254 caracteres; exactamente una '@'; parte local
/// y dominio no vacíos; ningún espacio en blanco ni carácter de control dentro. No es único (sección 9 de la spec).
/// </summary>
public sealed record Correo
{
    private const int LongitudMaxima = 254;

    public string Valor { get; }

    private Correo(string valor) => Valor = valor;

    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Correo? correo)
    {
        correo = null;

        var recortado = valor?.Trim();
        if (string.IsNullOrEmpty(recortado) || recortado.Length > LongitudMaxima)
            return false;

        if (recortado.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            return false;

        var arroba = recortado.IndexOf('@');
        if (arroba <= 0 || arroba == recortado.Length - 1 || arroba != recortado.LastIndexOf('@'))
            return false;

        correo = new Correo(recortado);
        return true;
    }

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static Correo Crear(string valor)
    {
        if (!TryCrear(valor, out var correo))
            throw new ArgumentException("El correo no cumple el formato usuario@dominio.", nameof(valor));

        return correo;
    }

    public override string ToString() => Valor;
}
