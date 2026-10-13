// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

public static class Roles
{
    public const string Cliente = "Cliente";               // rol de la API (claim ClaimTypes.Role)
    public const string Operador = "Operador";
    public const string ClienteEnKeycloak = "cliente";     // rol de realm en clientes (realm_access.roles)
    public const string OperadorEnKeycloak = "operador";   // rol de realm en empleados
}
