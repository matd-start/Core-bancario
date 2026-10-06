namespace CoreBancario.Application.Comun;

/// <summary>
/// Éxito con un valor, o datos inválidos con TODOS los errores por campo (RNF-03, CL-04; ADR-0008 y ADR-0010).
/// Solo representa "datos inválidos"; los demás errores siguen siendo excepciones.
/// </summary>
public sealed class Resultado<T>
{
    private static readonly IReadOnlyDictionary<string, string[]> SinErrores = new Dictionary<string, string[]>();

    private readonly T? _valor;

    private Resultado(T? valor, IReadOnlyDictionary<string, string[]> errores, bool esExito)
    {
        _valor = valor;
        Errores = errores;
        EsExito = esExito;
    }

    public bool EsExito { get; }

    /// <exception cref="InvalidOperationException">Se lee con EsExito == false.</exception>
    public T Valor =>
        EsExito && _valor is T valor
            ? valor
            : throw new InvalidOperationException("El resultado es inválido: no tiene valor. Revise Errores.");

    /// <summary>Campo (clave camelCase del contrato) → mensajes. Vacío si EsExito.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    public static Resultado<T> Exito(T valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return new Resultado<T>(valor, SinErrores, esExito: true);
    }

    /// <exception cref="ArgumentException">errores está vacío.</exception>
    public static Resultado<T> Invalido(IReadOnlyDictionary<string, string[]> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        if (errores.Count == 0)
            throw new ArgumentException("Un resultado inválido necesita al menos un error.", nameof(errores));

        return new Resultado<T>(default, errores, esExito: false);
    }
}
