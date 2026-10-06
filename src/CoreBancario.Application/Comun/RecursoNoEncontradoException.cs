namespace CoreBancario.Application.Comun;

/// <summary>404 (RNF-03): el cliente o la cuenta no existen (CL-09, CL-16, CL-18).</summary>
public sealed class RecursoNoEncontradoException : Exception
{
    public RecursoNoEncontradoException(string mensaje) : base(mensaje) { }
}
