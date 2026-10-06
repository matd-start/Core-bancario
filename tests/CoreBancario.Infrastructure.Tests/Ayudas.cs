using System.Globalization;
using System.Security.Cryptography;
using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using CoreBancario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// Datos de prueba unicos: cada prueba trabaja con sus propios documentos y numeros de cuenta, de modo que
/// puedan correr en paralelo contra la misma base sin limpiarla. Nunca se usan literales como "CC 1234567".
/// </summary>
internal static class Ayudas
{
    // Instante redondo: timestamptz guarda microsegundos y .NET maneja ticks de 100 ns.
    public static readonly DateTimeOffset Instante = new(2026, 10, 5, 14, 3, 11, TimeSpan.Zero);

    /// <summary>CC de 7 a 9 digitos que no empieza por 0.</summary>
    public static string NumeroDeDocumentoUnico() =>
        RandomNumberGenerator.GetInt32(1_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);

    /// <summary>Numero de cuenta valido (10 digitos, Luhn) calculado aqui, sin usar el codigo bajo prueba.</summary>
    public static string NumeroDeCuentaUnico()
    {
        var cuerpo = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);
        return cuerpo + DigitoLuhn(cuerpo);
    }

    private static int DigitoLuhn(string cuerpo)
    {
        var suma = 0;
        for (var i = 0; i < cuerpo.Length; i++)
        {
            var digito = cuerpo[cuerpo.Length - 1 - i] - '0';
            if (i % 2 == 0)
            {
                digito *= 2;
                if (digito > 9) digito -= 9;
            }
            suma += digito;
        }
        return (10 - suma % 10) % 10;
    }

    public static Cliente NuevoCliente(TipoDocumento tipo = TipoDocumento.CC, string? numeroDocumento = null) =>
        Cliente.Registrar(
            Documento.Crear(tipo, numeroDocumento ?? NumeroDeDocumentoUnico()),
            NombreDePersona.Crear("María José"),
            NombreDePersona.Crear("Núñez"),
            Correo.Crear("maria@example.com"),
            Telefono.Crear("+573001234567"),
            Instante);

    public static Cuenta NuevaCuenta(Guid clienteId, Moneda? moneda = null, string? numero = null,
        DateTimeOffset? apertura = null) =>
        Cuenta.Abrir(NumeroDeCuenta.Crear(numero ?? NumeroDeCuentaUnico()), clienteId, moneda ?? Moneda.COP,
            apertura ?? Instante);

    /// <summary>Guarda un cliente nuevo y una cuenta suya (con el saldo indicado) y devuelve la cuenta guardada.</summary>
    public static async Task<Cuenta> GuardarCuentaAsync(PostgresFixture bd, Moneda? moneda = null, Dinero? saldo = null)
    {
        var cliente = NuevoCliente();
        var cuenta = NuevaCuenta(cliente.Id, moneda);
        if (saldo is not null)
            cuenta.Acreditar(saldo, Instante);

        await using var db = bd.CrearContexto();
        db.Clientes.Add(cliente);
        db.Cuentas.Add(cuenta);
        await db.GuardarCambiosAsync(TestContext.Current.CancellationToken);
        return cuenta;
    }

    public static async Task<Cuenta> LeerCuentaAsync(CoreBancarioDbContext db, Guid cuentaId) =>
        await db.Cuentas.SingleAsync(c => c.Id == cuentaId, TestContext.Current.CancellationToken);
}
