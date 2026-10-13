// SDD: esqueleto creado por test-writer
namespace CoreBancario.Api.Autenticacion;

public static class SelectorDeEsquema
{
    /// <summary>
    /// Lee el "iss" del Bearer SIN validarlo, solo para elegir quién lo valida.
    /// iss == Empleados.Emisor → Empleados. Cualquier otro caso (sin cabecera, ilegible, otro emisor) → Clientes,
    /// que lo rechazará y registrará el motivo. Nunca lanza.
    /// </summary>
    public static string Elegir(HttpContext contexto, OpcionesDeAutenticacion opciones) =>
        throw new NotImplementedException();
}
