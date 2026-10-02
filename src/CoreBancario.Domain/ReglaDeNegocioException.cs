namespace CoreBancario.Domain;

/// <summary>Base de todo error de dominio: una regla de negocio (RN-xx) rota. Ver ADR-0008.</summary>
public abstract class ReglaDeNegocioException : Exception
{
    protected ReglaDeNegocioException(string message) : base(message) { }
}
