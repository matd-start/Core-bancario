using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Application.Tests;

/// <summary>Datos de prueba comunes. Todo se arma con la API publica del dominio.</summary>
internal static class Datos
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 5, 14, 3, 11, TimeSpan.Zero);

    public static RelojFijo Reloj => new(Ahora);

    public static Cliente Cliente(string numeroDocumento = "1234567", TipoDocumento tipo = TipoDocumento.CC) =>
        Domain.Clientes.Cliente.Registrar(
            Documento.Crear(tipo, numeroDocumento),
            NombreDePersona.Crear("María José"),
            NombreDePersona.Crear("Núñez"),
            Correo.Crear("maria@example.com"),
            Telefono.Crear("+573001234567"),
            Ahora);

    public static NumeroDeCuenta NumeroA => NumeroDeCuenta.Crear("1234567897");

    public static NumeroDeCuenta NumeroB => NumeroDeCuenta.Crear("9876543217");

    public static Cuenta Cuenta(Guid clienteId, NumeroDeCuenta? numero = null, Moneda? moneda = null,
        DateTimeOffset? apertura = null) =>
        Domain.Cuentas.Cuenta.Abrir(numero ?? NumeroA, clienteId, moneda ?? Moneda.COP, apertura ?? Ahora);
}
