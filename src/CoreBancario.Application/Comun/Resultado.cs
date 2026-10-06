namespace CoreBancario.Application.Comun;

/// <summary>Éxito con un valor, o datos inválidos con TODOS los errores por campo (RNF-03, CL-04; ADR-0008 y ADR-0010).</summary>
public sealed class Resultado<T>
{
    // SDD: esqueleto creado por test-writer
    public bool EsExito => throw new NotImplementedException();

    /// <exception cref="InvalidOperationException">Se lee con EsExito == false.</exception>
    // SDD: esqueleto creado por test-writer
    public T Valor => throw new NotImplementedException();

    /// <summary>Campo (clave camelCase del contrato) → mensajes. Vacío si EsExito.</summary>
    // SDD: esqueleto creado por test-writer
    public IReadOnlyDictionary<string, string[]> Errores => throw new NotImplementedException();

    // SDD: esqueleto creado por test-writer
    public static Resultado<T> Exito(T valor) => throw new NotImplementedException();

    /// <exception cref="ArgumentException">errores está vacío.</exception>
    // SDD: esqueleto creado por test-writer
    public static Resultado<T> Invalido(IReadOnlyDictionary<string, string[]> errores) => throw new NotImplementedException();
}
