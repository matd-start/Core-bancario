namespace CoreBancario.Application.Comun;

/// <summary>409 (RNF-03): otra operación modificó la misma fila (CL-15). Quien llama debe volver a leer y reintentar.</summary>
public sealed class ConflictoDeConcurrenciaException : Exception
{
    public ConflictoDeConcurrenciaException(Exception? causa = null)
        : base("La información cambió mientras se procesaba la operación. Vuelva a consultar e intente de nuevo.", causa) { }
}
