// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

public sealed class TokenSinSujetoException : Exception
{
    public TokenSinSujetoException() : base("El token no trae el claim sub.") { }
}
