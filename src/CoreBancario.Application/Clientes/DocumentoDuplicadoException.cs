namespace CoreBancario.Application.Clientes;

/// <summary>409 (RNF-03): ya existe un cliente con ese tipo y número normalizado (RN-17, CL-01, CL-03, CL-08).</summary>
public sealed class DocumentoDuplicadoException : Exception
{
    public DocumentoDuplicadoException(Exception? causa = null)
        : base("Ya existe un cliente con ese tipo y número de documento.", causa) { }
}
