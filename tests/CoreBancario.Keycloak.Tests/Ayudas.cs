using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CoreBancario.Keycloak.Tests;

internal static partial class Ayudas
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sube desde el directorio de las pruebas hasta encontrar CoreBancario.slnx.</summary>
    public static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "CoreBancario.slnx")))
            directorio = directorio.Parent;

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontro CoreBancario.slnx subiendo desde el directorio de las pruebas.");
    }

    /// <summary>CC de 7 a 9 digitos que no empieza por 0. Cada prueba usa su propio documento.</summary>
    public static string NumeroDeDocumentoUnico() =>
        RandomNumberGenerator.GetInt32(1_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);

    /// <summary>Clave valida segun la politica de los realms (12 a 64 caracteres, sin el usuario).</summary>
    public static string ClaveValida() => $"Clave-{Guid.NewGuid():N}"[..22];

    /// <summary>Clave temporal como la que entregara la API (16 caracteres al azar).</summary>
    public static string ClaveTemporal() => $"Temp-{Guid.NewGuid():N}"[..21];

    /// <summary>Decodifica el cuerpo (payload) de un JWT sin validarlo: las pruebas solo quieren leer sus claims.</summary>
    public static JsonNode CuerpoDelJwt(string jwt)
    {
        var cuerpo = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        cuerpo = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        return JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cuerpo)))!;
    }

    public static string SinEspaciosRepetidos(string texto) => EspaciosRepetidos().Replace(texto, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRepetidos();
}
