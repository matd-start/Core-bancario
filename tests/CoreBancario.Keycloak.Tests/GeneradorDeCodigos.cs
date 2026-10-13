using OtpNet;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// El telefono del usuario: calcula el codigo TOTP (RFC 6238) con el secreto que Keycloak muestra al inscribir el
/// segundo factor. Keycloak no acepta el mismo codigo dos veces (otpPolicyCodeReusable = false): si el siguiente
/// codigo pedido cae en la misma ventana de 30 s que el anterior, espera a que cambie.
/// </summary>
public sealed class GeneradorDeCodigos(string secretoBase32)
{
    private readonly Totp _totp = new(Base32Encoding.ToBytes(secretoBase32));
    private string? _ultimo;

    public async Task<string> SiguienteAsync()
    {
        var codigo = _totp.ComputeTotp();
        if (codigo == _ultimo)
        {
            await Task.Delay(TimeSpan.FromSeconds(_totp.RemainingSeconds()) + TimeSpan.FromMilliseconds(1500));
            codigo = _totp.ComputeTotp();
        }

        _ultimo = codigo;
        return codigo;
    }

    /// <summary>Un codigo de 6 digitos que no es valido en la ventana anterior, la actual ni la siguiente.</summary>
    public string CodigoIncorrecto()
    {
        var ahora = DateTime.UtcNow;
        var validos = new[] { -30, 0, 30 }.Select(s => _totp.ComputeTotp(ahora.AddSeconds(s))).ToHashSet();
        return Enumerable.Range(0, 1_000_000)
            .Select(n => n.ToString("D6", System.Globalization.CultureInfo.InvariantCulture))
            .First(c => !validos.Contains(c));
    }
}
