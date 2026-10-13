using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace CoreBancario.Keycloak.Tests;

/// <summary>Que pantalla de Keycloak devolvio el servidor.</summary>
public enum TipoDePagina
{
    /// <summary>Usuario y clave.</summary>
    Login,

    /// <summary>Solo la clave (re-autenticacion: "dame la actual").</summary>
    Clave,

    /// <summary>Codigo de la app autenticadora.</summary>
    Otp,

    /// <summary>Inscripcion del segundo factor (muestra el secreto).</summary>
    ConfigurarOtp,

    /// <summary>Clave nueva y confirmacion.</summary>
    CambioDeClave,

    /// <summary>Keycloak redirigio a la URI de la aplicacion con un codigo de autorizacion.</summary>
    CodigoEmitido,

    /// <summary>Cualquier otra pantalla (informacion, error, sesion cerrada...).</summary>
    Otra,
}

/// <summary>Una pantalla (o la redireccion final) de Keycloak, ya leida.</summary>
public sealed class Pagina
{
    public required TipoDePagina Tipo { get; init; }

    public required Uri Url { get; init; }

    public required string Html { get; init; }

    public IDocument? Documento { get; init; }

    /// <summary>Solo cuando Tipo es CodigoEmitido.</summary>
    public string? CodigoDeAutorizacion { get; init; }

    /// <summary>Solo cuando Keycloak redirigio a la aplicacion con ?error=...</summary>
    public string? ErrorDeOidc { get; init; }

    /// <summary>Los mensajes de error visibles (los de Keycloak, sin HTML), separados por " | ".</summary>
    public string MensajesDeError => string.Join(" | ", MensajesDeErrorEnLista());

    public bool TieneError => MensajesDeErrorEnLista().Count > 0;

    public string? Valor(string campo) => (Documento?.QuerySelector($"input[name='{campo}']") as IHtmlInputElement)?.Value;

    /// <summary>
    /// La pantalla trae el usuario: en el valor de un campo (por ejemplo un input oculto name=username) o en un atributo
    /// data-* de algun elemento. Sin distinguir mayusculas, porque Keycloak guarda el usuario en minusculas.
    /// </summary>
    public bool TraeElUsuario(string nombreDeUsuario) =>
        Documento is not null
        && (Documento.QuerySelectorAll("input").OfType<IHtmlInputElement>().Any(i => string.Equals(i.Value, nombreDeUsuario, StringComparison.OrdinalIgnoreCase))
            || Documento.All.Any(e => e.Attributes.Any(a => a.Name.StartsWith("data-", StringComparison.Ordinal)
                                                            && string.Equals(a.Value, nombreDeUsuario, StringComparison.OrdinalIgnoreCase))));

    public bool CargaElScript(string nombreDelArchivo) =>
        Documento?.QuerySelectorAll("script[src]").Any(s => (s.GetAttribute("src") ?? "").Contains(nombreDelArchivo, StringComparison.Ordinal)) ?? false;

    private List<string> MensajesDeErrorEnLista() =>
        Documento is null
            ? []
            : [.. Documento
                .QuerySelectorAll(".kc-feedback-text, .alert-error, #kc-error-message")
                .Where(e => e.Closest(".pf-m-warning, .pf-m-info, .pf-m-success") is null)
                .Select(e => Ayudas.SinEspaciosRepetidos(e.TextContent))
                .Where(t => t.Length > 0)
                .Distinct()];
}

/// <summary>Tokens que devuelve el endpoint /token.</summary>
public sealed record TokensOidc(string AccessToken, string RefreshToken, string IdToken);

/// <summary>El usuario de prueba y lo que recuerda entre un ingreso y el siguiente.</summary>
public sealed class UsuarioDePrueba(string realm, string nombreDeUsuario, string sub, string clave)
{
    public string Realm { get; } = realm;

    public string NombreDeUsuario { get; } = nombreDeUsuario;

    public string Sub { get; } = sub;

    /// <summary>La clave vigente: cambia cuando el usuario la cambia.</summary>
    public string Clave { get; set; } = clave;

    /// <summary>Su "telefono": existe desde que inscribe el segundo factor.</summary>
    public GeneradorDeCodigos? Otp { get; set; }

    /// <summary>La clave nueva que elegira cuando Keycloak se la pida.</summary>
    public string ClaveNueva { get; set; } = Ayudas.ClaveValida();
}

/// <summary>
/// Un navegador minimo para el flujo de codigo de autorizacion con PKCE (ADR-0018): HttpClient sin redirecciones
/// automaticas y con las cookies a mano (las del login llevan Secure y el CookieContainer no las reenvia por HTTP,
/// hallazgo 7), AngleSharp para leer cada pantalla y su formulario. Un navegador es una "ventana": cookies propias.
/// </summary>
public sealed class NavegadorSimulado : IDisposable
{
    public const string ClientId = "core-bancario-web";
    public const string UriDeRedireccion = "http://localhost:3000/callback";

    private readonly Uri _urlBase;
    private readonly HttpClient _http;
    private readonly GalletasManuales _galletas = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private readonly HtmlParser _analizador = new();
    private string _realm = "";
    private string _verificador = "";

    public NavegadorSimulado(string urlBase)
    {
        _urlBase = new Uri(urlBase);
        _http = new HttpClient(_galletas) { Timeout = TimeSpan.FromMinutes(2) };
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Abre la pantalla de inicio de sesion del realm (con PKCE S256) y la lee.</summary>
    public async Task<Pagina> IniciarAsync(string realm, string? accionDeLaAplicacion = null)
    {
        _realm = realm;
        _verificador = Base64Url(RandomNumberGenerator.GetBytes(32));
        var desafio = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(_verificador)));

        var consulta = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = UriDeRedireccion,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = desafio,
            ["code_challenge_method"] = "S256",
        };
        if (accionDeLaAplicacion is not null)
            consulta["kc_action"] = accionDeLaAplicacion;

        var url = new Uri(_urlBase, $"/realms/{realm}/protocol/openid-connect/auth?" +
                                    string.Join('&', consulta.Select(c => $"{c.Key}={Uri.EscapeDataString(c.Value)}")));
        return await LeerAsync(await _http.GetAsync(url, Ayudas.Ct), url);
    }

    /// <summary>Envia el formulario de la pantalla con los campos indicados (el resto se envia como lo trae la pagina).</summary>
    public async Task<Pagina> EnviarAsync(Pagina pantalla, params (string Campo, string Valor)[] campos)
    {
        var formulario = FormularioDe(pantalla, campos.Select(c => c.Campo).ToArray());
        var accion = new Uri(pantalla.Url, formulario.GetAttribute("action") ?? "");
        var datos = CamposDelFormulario(formulario);
        foreach (var (campo, valor) in campos)
        {
            datos.RemoveAll(d => d.Key == campo);
            datos.Add(new KeyValuePair<string, string>(campo, valor));
        }

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, accion) { Content = new FormUrlEncodedContent(datos) };
        return await LeerAsync(await _http.SendAsync(solicitud, Ayudas.Ct), accion);
    }

    /// <summary>
    /// Recorre las pantallas con los datos del usuario hasta que Keycloak emite el codigo, muestra un error o llega a una
    /// pantalla desconocida. Inscribe el segundo factor si se lo piden (y guarda el "telefono" en el usuario) y cambia la
    /// clave por la nueva si se la piden. Devuelve la ultima pantalla y el recorrido (los tipos de pantalla que vio).
    /// </summary>
    public async Task<(Pagina Ultima, IReadOnlyList<TipoDePagina> Recorrido)> ConducirAsync(
        Pagina inicio, UsuarioDePrueba usuario, int maximoDePantallas = 10)
    {
        var recorrido = new List<TipoDePagina>();
        var pantalla = inicio;
        while (recorrido.Count < maximoDePantallas && pantalla.Tipo is not (TipoDePagina.CodigoEmitido or TipoDePagina.Otra))
        {
            recorrido.Add(pantalla.Tipo);
            pantalla = await ResponderAsync(pantalla, usuario);
            if (pantalla.TieneError)
                break;
        }

        return (pantalla, recorrido);
    }


    /// <summary>
    /// Responde a las pantallas anteriores (login, claves, OTP) y se detiene EN la pantalla de destino, sin enviarla:
    /// para probar lo que esa pantalla muestra o lo que hace el servidor con lo que se le envie.
    /// </summary>
    public async Task<Pagina> AvanzarHastaAsync(Pagina inicio, UsuarioDePrueba usuario, TipoDePagina destino, int maximoDePantallas = 10)
    {
        var pantalla = inicio;
        for (var vistas = 0; vistas < maximoDePantallas && pantalla.Tipo != destino; vistas++)
            pantalla = await ResponderAsync(pantalla, usuario);

        Assert.True(pantalla.Tipo == destino,
            $"Se esperaba llegar a {destino} y Keycloak mostro {pantalla.Tipo}. Errores: {pantalla.MensajesDeError}");
        return pantalla;
    }

    private async Task<Pagina> ResponderAsync(Pagina pantalla, UsuarioDePrueba usuario)
    {
        switch (pantalla.Tipo)
        {
            case TipoDePagina.Login:
                return await EnviarAsync(pantalla, ("username", usuario.NombreDeUsuario), ("password", usuario.Clave));
            case TipoDePagina.Clave:
                return await EnviarAsync(pantalla, ("password", usuario.Clave));
            case TipoDePagina.CambioDeClave:
                var pagina = await EnviarAsync(pantalla, ("password-new", usuario.ClaveNueva), ("password-confirm", usuario.ClaveNueva));
                usuario.Clave = pagina.TieneError ? usuario.Clave : usuario.ClaveNueva;
                return pagina;
            case TipoDePagina.ConfigurarOtp:
                usuario.Otp = new GeneradorDeCodigos(SecretoBase32(pantalla.Valor("totpSecret")!));
                return await EnviarAsync(pantalla, ("totp", await usuario.Otp.SiguienteAsync()), ("userLabel", "telefono-de-pruebas"));
            case TipoDePagina.Otp:
                return await EnviarAsync(pantalla, ("otp", await usuario.Otp!.SiguienteAsync()));
            default:
                throw new InvalidOperationException($"No se como responder a la pantalla {pantalla.Tipo}.");
        }
    }

    /// <summary>Canjea el codigo de autorizacion (con el verificador PKCE de esta sesion) por los tokens.</summary>
    public async Task<TokensOidc> CanjearCodigoAsync(Pagina emitida)
    {
        using var respuesta = await _http.PostAsync(new Uri(_urlBase, $"/realms/{_realm}/protocol/openid-connect/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = ClientId,
                ["code"] = emitida.CodigoDeAutorizacion!,
                ["redirect_uri"] = UriDeRedireccion,
                ["code_verifier"] = _verificador,
            }), Ayudas.Ct);
        respuesta.EnsureSuccessStatusCode();

        var json = (await respuesta.Content.ReadFromJsonAsync<JsonNode>(Ayudas.Ct))!;
        return new TokensOidc((string)json["access_token"]!, (string)json["refresh_token"]!, (string)json["id_token"]!);
    }

    /// <summary>Canjea un codigo cualquiera (por ejemplo, uno inventado). Devuelve la respuesta tal cual.</summary>
    public Task<HttpResponseMessage> PedirTokenConCodigoAsync(string realm, string codigo) =>
        _http.PostAsync(new Uri(_urlBase, $"/realms/{realm}/protocol/openid-connect/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = ClientId,
                ["code"] = codigo,
                ["redirect_uri"] = UriDeRedireccion,
                ["code_verifier"] = _verificador,
            }), Ayudas.Ct);

    /// <summary>Intenta renovar la sesion con el refresh token. Devuelve la respuesta tal cual (200, o 400 invalid_grant).</summary>
    public Task<HttpResponseMessage> RenovarAsync(string realm, string refreshToken) =>
        _http.PostAsync(new Uri(_urlBase, $"/realms/{realm}/protocol/openid-connect/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ClientId,
                ["refresh_token"] = refreshToken,
            }), Ayudas.Ct);

    /// <summary>Cierra la sesion como lo hara la SPA: el end_session_endpoint con el id_token_hint.</summary>
    public async Task CerrarSesionAsync(string realm, string idToken)
    {
        var url = new Uri(_urlBase,
            $"/realms/{realm}/protocol/openid-connect/logout?client_id={ClientId}&id_token_hint={Uri.EscapeDataString(idToken)}");
        using var respuesta = await _http.GetAsync(url, Ayudas.Ct);
        Assert.True((int)respuesta.StatusCode is >= 200 and < 400, $"El cierre de sesion respondio {(int)respuesta.StatusCode}.");
    }

    // ---- lectura de pantallas ----

    private async Task<Pagina> LeerAsync(HttpResponseMessage respuesta, Uri url)
    {
        using var _ = respuesta;
        while ((int)respuesta.StatusCode is >= 300 and < 400 && respuesta.Headers.Location is { } destino)
        {
            var siguiente = destino.IsAbsoluteUri ? destino : new Uri(url, destino);
            if (siguiente.AbsoluteUri.StartsWith(UriDeRedireccion, StringComparison.Ordinal))
                return PaginaDeRedireccion(siguiente);

            respuesta.Dispose();
            url = siguiente;
            respuesta = await _http.GetAsync(url, Ayudas.Ct);
        }

        var html = await respuesta.Content.ReadAsStringAsync(Ayudas.Ct);
        var documento = _analizador.ParseDocument(html);
        return new Pagina { Tipo = TipoDe(documento), Url = url, Html = html, Documento = documento };
    }

    private static Pagina PaginaDeRedireccion(Uri destino)
    {
        var consulta = System.Web.HttpUtility.ParseQueryString(destino.Query);
        return new Pagina
        {
            Tipo = consulta["code"] is null ? TipoDePagina.Otra : TipoDePagina.CodigoEmitido,
            Url = destino,
            Html = "",
            CodigoDeAutorizacion = consulta["code"],
            ErrorDeOidc = consulta["error"],
        };
    }

    private static TipoDePagina TipoDe(IDocument documento)
    {
        if (documento.QuerySelector("input[name='totpSecret']") is not null)
            return TipoDePagina.ConfigurarOtp;
        if (documento.QuerySelector("input[name='password-new']") is not null)
            return TipoDePagina.CambioDeClave;
        if (documento.QuerySelector("input[name='otp']") is not null)
            return TipoDePagina.Otp;
        if (documento.QuerySelector("input[name='password']") is not null)
            return documento.QuerySelector("input[name='username']") is not null ? TipoDePagina.Login : TipoDePagina.Clave;

        return TipoDePagina.Otra;
    }

    private static IHtmlFormElement FormularioDe(Pagina pantalla, string[] camposQueDebeTener)
    {
        var formularios = pantalla.Documento!.QuerySelectorAll("form").OfType<IHtmlFormElement>();
        return formularios.FirstOrDefault(f => camposQueDebeTener.Any(c => f.QuerySelector($"[name='{c}']") is not null))
               ?? formularios.First();
    }

    /// <summary>Los campos del formulario tal como los enviaria un navegador (salvo los botones, de los que va uno).</summary>
    private static List<KeyValuePair<string, string>> CamposDelFormulario(IHtmlFormElement formulario)
    {
        var campos = new List<KeyValuePair<string, string>>();
        foreach (var entrada in formulario.QuerySelectorAll("input[name]").OfType<IHtmlInputElement>())
        {
            var tipo = (entrada.Type ?? "text").ToLowerInvariant();
            var esBoton = tipo is "submit" or "button" or "image" or "reset" or "file";
            var sinMarcar = tipo is "checkbox" or "radio" && !entrada.IsChecked;
            if (!esBoton && !sinMarcar)
                campos.Add(new KeyValuePair<string, string>(entrada.Name!, entrada.Value ?? ""));
        }

        foreach (var lista in formulario.QuerySelectorAll("select[name]").OfType<IHtmlSelectElement>())
            campos.Add(new KeyValuePair<string, string>(lista.Name!, lista.Value ?? ""));

        var boton = formulario.QuerySelectorAll("input[type=submit][name], button[type=submit][name]")
            .FirstOrDefault(b => !b.GetAttribute("name")!.StartsWith("cancel", StringComparison.OrdinalIgnoreCase));
        if (boton is not null)
            campos.Add(new KeyValuePair<string, string>(boton.GetAttribute("name")!, boton.GetAttribute("value") ?? ""));

        return campos;
    }

    /// <summary>
    /// Keycloak muestra en un campo oculto el secreto "crudo" (texto aleatorio) y a la app autenticadora le entrega ese mismo
    /// texto codificado en Base32 (es lo que lleva el codigo QR). El telefono de la prueba usa la forma Base32.
    /// </summary>
    private static string SecretoBase32(string secretoCrudo) =>
        OtpNet.Base32Encoding.ToString(Encoding.UTF8.GetBytes(secretoCrudo));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Guarda y reenvia las cookies a mano, ignorando Secure y Path (una ventana = un realm a la vez).</summary>
    private sealed class GalletasManuales(HttpMessageHandler interno) : DelegatingHandler(interno)
    {
        private readonly Dictionary<string, string> _jarra = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_jarra)
            {
                if (_jarra.Count > 0)
                    request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _jarra.Select(g => $"{g.Key}={g.Value}")));
            }

            var respuesta = await base.SendAsync(request, cancellationToken);
            if (respuesta.Headers.TryGetValues("Set-Cookie", out var cabeceras))
            {
                lock (_jarra)
                {
                    foreach (var cabecera in cabeceras)
                        Guardar(cabecera);
                }
            }

            return respuesta;
        }

        private void Guardar(string cabecera)
        {
            var partes = cabecera.Split(';').Select(p => p.Trim()).ToList();
            var separador = partes[0].IndexOf('=', StringComparison.Ordinal);
            var nombre = partes[0][..separador];
            var valor = partes[0][(separador + 1)..];
            var borrada = valor.Length == 0 || partes.Any(p => p.Equals("Max-Age=0", StringComparison.OrdinalIgnoreCase));

            if (borrada)
                _jarra.Remove(nombre);
            else
                _jarra[nombre] = valor;
        }
    }
}
