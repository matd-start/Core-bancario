using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CoreBancario.Domain.Clientes;

/// <summary>
/// E.164. Normalizar: quitar espacios en blanco, '-', '(' y ')'. Válido tras normalizar: '+' seguido de 8 a 15
/// dígitos ASCII (CL-07). "+57 (300) 123-4567" → "+573001234567".
/// </summary>
public sealed record Telefono
{
    public string Valor { get; }

    private Telefono(string valor) => Valor = valor;

    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Telefono? telefono)
    {
        telefono = null;

        if (valor is null)
            return false;

        var constructor = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (!char.IsWhiteSpace(caracter) && caracter is not ('-' or '(' or ')'))
                constructor.Append(caracter);
        }

        var normalizado = constructor.ToString();
        if (!normalizado.StartsWith('+'))
            return false;

        var cantidadDeDigitos = normalizado.Length - 1;
        if (cantidadDeDigitos is < 8 or > 15)
            return false;

        for (var i = 1; i < normalizado.Length; i++)
        {
            if (!char.IsAsciiDigit(normalizado[i]))
                return false;
        }

        telefono = new Telefono(normalizado);
        return true;
    }

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static Telefono Crear(string valor)
    {
        if (!TryCrear(valor, out var telefono))
            throw new ArgumentException("El teléfono debe tener formato internacional: + y de 8 a 15 dígitos.", nameof(valor));

        return telefono;
    }

    public override string ToString() => Valor;
}
