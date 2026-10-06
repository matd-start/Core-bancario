using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CoreBancario.Domain.Clientes;

/// <summary>
/// RN-17. Tipo + número normalizado. Normalizar: quitar espacios en blanco, '.' y '-', y pasar a mayúsculas
/// invariantes. Formato tras normalizar: CC y CE, solo dígitos ASCII, de 3 a 10, sin empezar por '0';
/// PA, letras y dígitos ASCII, de 5 a 15. Igualdad de record: (Tipo, Numero) ya normalizado.
/// </summary>
public sealed record Documento
{
    public TipoDocumento Tipo { get; }
    public string Numero { get; }

    private Documento(TipoDocumento tipo, string numero)
    {
        Tipo = tipo;
        Numero = numero;
    }

    /// <summary>Normaliza y valida. false si numero es null o no cumple el formato de su tipo, o si tipo no está definido.</summary>
    public static bool TryCrear(TipoDocumento tipo, string? numero, [NotNullWhen(true)] out Documento? documento)
    {
        documento = null;

        if (numero is null || !Enum.IsDefined(tipo))
            return false;

        var normalizado = Normalizar(numero);
        if (!TieneFormatoValido(tipo, normalizado))
            return false;

        documento = new Documento(tipo, normalizado);
        return true;
    }

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false (error de programación: quien llama debió validar).</exception>
    public static Documento Crear(TipoDocumento tipo, string numero)
    {
        if (!TryCrear(tipo, numero, out var documento))
            throw new ArgumentException($"El número no cumple el formato de un documento {tipo}.", nameof(numero));

        return documento;
    }

    /// <returns>"CC 1234567".</returns>
    public override string ToString() => $"{Tipo} {Numero}";

    // Solo se pasan a mayúsculas los caracteres ASCII: así ningún carácter no ASCII puede "convertirse" en
    // una letra válida al subir a mayúsculas.
    private static string Normalizar(string numero)
    {
        var constructor = new StringBuilder(numero.Length);
        foreach (var caracter in numero)
        {
            if (char.IsWhiteSpace(caracter) || caracter is '.' or '-')
                continue;

            constructor.Append(char.IsAscii(caracter) ? char.ToUpperInvariant(caracter) : caracter);
        }

        return constructor.ToString();
    }

    private static bool TieneFormatoValido(TipoDocumento tipo, string normalizado) => tipo switch
    {
        TipoDocumento.CC or TipoDocumento.CE =>
            normalizado.Length is >= 3 and <= 10
            && normalizado[0] != '0'
            && normalizado.All(char.IsAsciiDigit),
        TipoDocumento.PA =>
            normalizado.Length is >= 5 and <= 15
            && normalizado.All(char.IsAsciiLetterOrDigit),
        _ => false,
    };
}
