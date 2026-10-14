namespace CoreBancario.Api.Autenticacion;

public static class EsquemasDeAutenticacion
{
    public const string PorEmisor = "Bearer";       // esquema de política (por defecto): elige uno de los dos siguientes
    public const string Clientes = "Clientes";      // JwtBearer del realm clientes
    public const string Empleados = "Empleados";    // JwtBearer del realm empleados
}
