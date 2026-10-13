// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

public static class Politicas
{
    public const string Cliente = "Cliente";        // RequireRole(Roles.Cliente)
    public const string Operador = "Operador";      // RequireRole(Roles.Operador)
}
