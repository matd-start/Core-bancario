using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CoreBancario.Api.Clientes;
using CoreBancario.Api.Cuentas;
using CoreBancario.Application.Clientes;
using CoreBancario.Application.Cuentas;

namespace CoreBancario.Api.Tests;

/// <summary>Cuerpo de error (ProblemDetails de RFC 9457 con la extension "codigo" del contrato).</summary>
public sealed record Problema(int? Status, string? Title, string? Detail, string? Codigo,
    Dictionary<string, string[]>? Errors);

/// <summary>
/// Datos de prueba unicos: cada prueba trabaja con sus propios documentos y numeros de cuenta, de modo que
/// puedan correr en paralelo contra la misma base sin limpiarla. Nunca se usan literales como "CC 1234567".
/// </summary>
internal static class Ayudas
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>CC de 7 a 9 digitos que no empieza por 0.</summary>
    public static string NumeroDeDocumentoUnico() =>
        RandomNumberGenerator.GetInt32(1_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);

    /// <summary>"1234567" pasa a "1.234.567": la misma transformacion que en los ejemplos de la spec.</summary>
    public static string ConPuntos(string numero)
    {
        var resultado = new System.Text.StringBuilder();
        for (var i = 0; i < numero.Length; i++)
        {
            if (i > 0 && (numero.Length - i) % 3 == 0)
                resultado.Append('.');
            resultado.Append(numero[i]);
        }
        return resultado.ToString();
    }

    /// <summary>Numero de cuenta valido (10 digitos, Luhn) calculado aqui, sin usar el codigo bajo prueba.</summary>
    public static string NumeroDeCuentaUnico()
    {
        var cuerpo = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);
        return cuerpo + DigitoLuhn(cuerpo);
    }

    /// <summary>Oraculo independiente: 10 digitos, no empieza por 0 y el ultimo es el verificador Luhn.</summary>
    public static bool EsNumeroDeCuentaValido(string numero) =>
        numero.Length == 10
        && numero.All(char.IsAsciiDigit)
        && numero[0] != '0'
        && numero[^1] - '0' == DigitoLuhn(numero[..9]);

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

    public static RegistrarClienteRequest SolicitudDeRegistro(string? tipo = "CC", string? numeroDocumento = null) =>
        new(tipo, numeroDocumento ?? NumeroDeDocumentoUnico(), "María José", "Núñez", "maria@example.com",
            "+57 300 123 4567");

    public static async Task<ClienteDto> RegistrarClienteAsync(HttpClient http, string? tipo = "CC",
        string? numeroDocumento = null)
    {
        var respuesta = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro(tipo, numeroDocumento), Ct);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ClienteDto>(Ct))!;
    }

    public static async Task<CuentaDto> AbrirCuentaAsync(HttpClient http, Guid clienteId, string moneda = "COP")
    {
        var respuesta = await http.PostAsJsonAsync($"/clientes/{clienteId}/cuentas", new AbrirCuentaRequest(moneda), Ct);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct))!;
    }

    public static async Task<CuentaDto> ConsultarCuentaAsync(HttpClient http, Guid cuentaId)
    {
        var respuesta = await http.GetAsync($"/cuentas/{cuentaId}", Ct);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct))!;
    }

    public static Task<HttpResponseMessage> BuscarPorDocumentoAsync(HttpClient http, string tipo, string numero) =>
        http.GetAsync(
            $"/clientes/por-documento?tipoDocumento={Uri.EscapeDataString(tipo)}&numeroDocumento={Uri.EscapeDataString(numero)}",
            Ct);

    public static async Task<Problema> LeerProblemaAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<Problema>(Ct))!;
}
