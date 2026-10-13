using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

/// <summary>
/// Operaciones protegidas de la 003a y utilidades para llamarlas con un token (valido o roto).
/// Las rutas llevan el marcador {id}: se sustituye por un Guid nuevo al llamar, para que los datos de la teoria
/// sean estables al descubrir las pruebas.
/// </summary>
internal static class AyudasDeAutenticacion
{
    public const string SinToken = "sin-token";
    public const string Vencido = "vencido";
    public const string AunNoValido = "aun-no-valido";
    public const string FirmaDeOtraClave = "firma-de-otra-clave";
    public const string OtraAudiencia = "otra-audiencia";
    public const string OtroEmisor = "otro-emisor";
    public const string Hs256 = "hs256";
    public const string SinSub = "sin-sub";
    public const string MalFormado = "mal-formado";

    /// <summary>Las ocho operaciones de la 002: solo el rol Operador.</summary>
    public static readonly (string Metodo, string Ruta)[] OperacionesDelOperador =
    [
        ("POST", "/clientes"),
        ("GET", "/clientes/{id}"),
        ("GET", "/clientes/por-documento?tipoDocumento=CC&numeroDocumento=1234567"),
        ("POST", "/clientes/{id}/cuentas"),
        ("GET", "/cuentas/{id}"),
        ("POST", "/cuentas/{id}/bloquear"),
        ("POST", "/cuentas/{id}/desbloquear"),
        ("POST", "/cuentas/{id}/cerrar"),
    ];

    /// <summary>Las dos operaciones de "mis cuentas": solo el rol Cliente.</summary>
    public static readonly (string Metodo, string Ruta)[] OperacionesDelCliente =
    [
        ("GET", "/mis-cuentas"),
        ("GET", "/mis-cuentas/{id}"),
    ];

    public static TheoryData<string, string> RutasDelOperador()
    {
        var datos = new TheoryData<string, string>();
        foreach (var (metodo, ruta) in OperacionesDelOperador)
            datos.Add(metodo, ruta);
        return datos;
    }

    public static TheoryData<string, string> RutasDelCliente()
    {
        var datos = new TheoryData<string, string>();
        foreach (var (metodo, ruta) in OperacionesDelCliente)
            datos.Add(metodo, ruta);
        return datos;
    }

    /// <summary>Cada operacion protegida por cada forma de presentar una credencial invalida (CL-07).</summary>
    public static TheoryData<string, string, string, string> OperacionesConCredencialInvalida()
    {
        string[] modos = [SinToken, Vencido, FirmaDeOtraClave, OtraAudiencia, OtroEmisor, Hs256, SinSub, MalFormado];
        var datos = new TheoryData<string, string, string, string>();
        foreach (var modo in modos)
        {
            foreach (var (metodo, ruta) in OperacionesDelOperador)
                datos.Add(metodo, ruta, nameof(EmisorDeToken.Empleados), modo);
            foreach (var (metodo, ruta) in OperacionesDelCliente)
                datos.Add(metodo, ruta, nameof(EmisorDeToken.Clientes), modo);
        }

        return datos;
    }

    /// <summary>
    /// Un token con UN solo defecto (el indicado), por lo demas valido para el emisor y su rol. null = sin cabecera.
    /// </summary>
    public static string? TokenConDefecto(string modo, EmisorDeToken emisor)
    {
        string[] roles = [emisor == EmisorDeToken.Empleados ? EmisorDePruebas.RolDeOperador : EmisorDePruebas.RolDeCliente];
        var sub = Guid.NewGuid().ToString();
        var ahora = DateTimeOffset.UtcNow;

        return modo switch
        {
            SinToken => null,
            Vencido => EmisorDePruebas.CrearToken(emisor, sub, roles, emitido: ahora - TimeSpan.FromMinutes(10)),
            AunNoValido => EmisorDePruebas.CrearToken(emisor, sub, roles, emitido: ahora + TimeSpan.FromMinutes(10)),
            FirmaDeOtraClave => EmisorDePruebas.CrearToken(emisor, sub, roles, firma: EmisorDePruebas.FirmaDeOtraClave()),
            OtraAudiencia => EmisorDePruebas.CrearToken(emisor, sub, roles, audiencia: "otra-api"),
            OtroEmisor => EmisorDePruebas.CrearToken(EmisorDeToken.Desconocido, sub, roles),
            Hs256 => EmisorDePruebas.CrearToken(emisor, sub, roles, firma: EmisorDePruebas.FirmaHs256()),
            SinSub => EmisorDePruebas.CrearToken(emisor, null, roles),
            MalFormado => "esto.no-es-un-jwt",
            _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de token desconocido."),
        };
    }

    public static string ConIdNuevo(string ruta) => ruta.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string metodo, string ruta, string? token = null)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ConIdNuevo(ruta));
        if (token is not null)
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return http.SendAsync(solicitud, Ct);
    }

    /// <summary>
    /// Estado, cabecera WWW-Authenticate, tipo de contenido y cuerpo sin el traceId (que cambia en cada peticion),
    /// con el id pedido sustituido por {id}: dos respuestas "identicas" dan el mismo texto.
    /// </summary>
    public static async Task<string> HuellaDeLaRespuestaAsync(HttpResponseMessage respuesta, string? idPedido = null)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(Ct);
        var json = JsonNode.Parse(cuerpo)!.AsObject();
        json.Remove("traceId");
        var texto = json.ToJsonString();
        texto = idPedido is null ? texto : texto.Replace(idPedido, "{id}", StringComparison.Ordinal);

        return $"{(int)respuesta.StatusCode}|{respuesta.Headers.WwwAuthenticate}|{respuesta.Content.Headers.ContentType?.MediaType}|{texto}";
    }

    /// <summary>Sube desde el directorio de las pruebas hasta encontrar CoreBancario.slnx.</summary>
    public static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "CoreBancario.slnx")))
            directorio = directorio.Parent;

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontro CoreBancario.slnx subiendo desde el directorio de las pruebas.");
    }
}
