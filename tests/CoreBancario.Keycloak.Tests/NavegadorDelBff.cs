using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;

namespace CoreBancario.Keycloak.Tests;

/// <summary>Lo que respondio el BFF a una peticion: estado, adonde redirige (absoluto), cuerpo y las cookies que puso.</summary>
public sealed record RespuestaDelBff(HttpStatusCode Estado, Uri? Ubicacion, string Cuerpo, IReadOnlyList<string> SetCookies)
{
    /// <summary>Lo que el navegador "veria" como destino: "/ruta?consulta" (sin el origen).</summary>
    public string Destino => Ubicacion?.PathAndQuery ?? "";

    public string? Cookie(string nombre) =>
        SetCookies.FirstOrDefault(c => c.StartsWith(nombre + "=", StringComparison.Ordinal));
}

/// <summary>
/// Un navegador minimo (004, plan seccion 7) que habla con DOS servidores a la vez, cada uno en su "puerto": el BFF en
/// memoria (http://localhost) y Keycloak real (http://localhost:{puerto}). Las cookies se guardan a mano y por origen
/// (el BFF solo recibe las suyas), sin redirecciones automaticas, y recorre las pantallas de Keycloak con AngleSharp.
/// Cada "navegador" es una ventana con sus propias cookies.
/// </summary>
public sealed class NavegadorDelBff : IDisposable
{
    public const string AutoridadDelBff = "localhost";
    public const string NombreDeLaCookieDeSesion = "cb-sesion";

    private readonly Enrutador _enrutador;
    private readonly HttpClient _http;
    private readonly HtmlParser _analizador = new();

    public NavegadorDelBff(HttpMessageHandler manejadorDelBff, string urlDeKeycloak)
    {
        _enrutador = new Enrutador(manejadorDelBff, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        _http = new HttpClient(_enrutador) { Timeout = TimeSpan.FromMinutes(2) };
        UrlDeKeycloak = urlDeKeycloak;
    }

    public string UrlDeKeycloak { get; }

    /// <summary>La URI de regreso que el BFF registra en cada realm (la de pruebas, sin puerto): /bff/signin-oidc/{area}.</summary>
    public static string UriDeRedireccionDe(string realm) => $"http://{AutoridadDelBff}/bff/signin-oidc/{realm}";

    /// <summary>El ultimo regreso con codigo que se entrego al BFF (para reutilizarlo, CL-02).</summary>
    public Uri? UltimoRegreso { get; private set; }

    public void Dispose() => _http.Dispose();

    // ---- Cookies ----

    /// <summary>Simula otra ventana del mismo navegador: el BFF ya no ve su cookie, pero Keycloak conserva las suyas.</summary>
    public void OlvidarCookiesDelBff() => _enrutador.Olvidar(AutoridadDelBff);

    /// <summary>Copia la cookie de sesion del BFF de otro navegador a este (la misma sesion del BFF, otras cookies de Keycloak).</summary>
    public void CopiarCookieDeSesionDe(NavegadorDelBff otro) =>
        _enrutador.Poner(AutoridadDelBff, NombreDeLaCookieDeSesion, otro._enrutador.Valor(AutoridadDelBff, NombreDeLaCookieDeSesion)!);

    // ---- Navegacion ----

    /// <summary>GET /bff/iniciar-sesion/{area}: sigue las redirecciones hasta Keycloak (o hasta donde el BFF decida).</summary>
    public Task<Pagina> IniciarSesionAsync(string area, string? regreso = null) =>
        NavegarAsync($"/bff/iniciar-sesion/{area}" + (regreso is null ? "" : $"?regreso={Uri.EscapeDataString(regreso)}"));

    /// <summary>Una navegacion (GET) a una ruta del BFF, siguiendo las redirecciones.</summary>
    public Task<Pagina> NavegarAsync(string rutaDelBff) => SeguirAsync(new Uri($"http://{AutoridadDelBff}{rutaDelBff}"));

    /// <summary>Envia el formulario de la pantalla de Keycloak con los campos indicados y sigue las redirecciones.</summary>
    public async Task<Pagina> EnviarAsync(Pagina pantalla, params (string Campo, string Valor)[] campos)
    {
        var formulario = NavegadorSimulado.FormularioDe(pantalla, campos.Select(c => c.Campo).ToArray());
        var accion = new Uri(pantalla.Url, formulario.GetAttribute("action") ?? "");
        var datos = NavegadorSimulado.CamposDelFormulario(formulario);
        foreach (var (campo, valor) in campos)
        {
            datos.RemoveAll(d => d.Key == campo);
            datos.Add(new KeyValuePair<string, string>(campo, valor));
        }

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, accion) { Content = new FormUrlEncodedContent(datos) };
        using var respuesta = await _http.SendAsync(solicitud, Ayudas.Ct);
        return await LeerAsync(respuesta, accion);
    }

    /// <summary>Recorre las pantallas con los datos del usuario hasta que Keycloak emite el codigo o aparece un error.</summary>
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
                usuario.Otp = new GeneradorDeCodigos(NavegadorSimulado.SecretoBase32(pantalla.Valor("totpSecret")!));
                return await EnviarAsync(pantalla, ("totp", await usuario.Otp.SiguienteAsync()), ("userLabel", "telefono-de-pruebas"));
            case TipoDePagina.Otp:
                return await EnviarAsync(pantalla, ("otp", await usuario.Otp!.SiguienteAsync()));
            default:
                throw new InvalidOperationException($"No se como responder a la pantalla {pantalla.Tipo}.");
        }
    }

    /// <summary>
    /// Entrega al BFF el regreso de Keycloak (la URI con el codigo): lo que haria el navegador al seguir la redireccion.
    /// No sigue la respuesta del BFF: devuelve su estado, adonde redirige y las cookies que puso.
    /// </summary>
    public async Task<RespuestaDelBff> EntregarRegresoAsync(Uri regreso)
    {
        if (regreso.Query.Contains("code=", StringComparison.Ordinal))
            UltimoRegreso = regreso;

        return await PedirAbsolutoAsync(HttpMethod.Get, regreso, conCsrf: false, secFetchSite: "cross-site");
    }

    /// <summary>
    /// El recorrido completo de un inicio de sesion: BFF -> Keycloak (con las pantallas que pida) -> BFF. Exige que Keycloak
    /// emita el codigo y que el BFF responda con una redireccion y la cookie de sesion.
    /// </summary>
    public async Task<RespuestaDelBff> IniciarSesionCompletaAsync(UsuarioDePrueba usuario, string area, string? regreso = null)
    {
        var inicio = await IniciarSesionAsync(area, regreso);
        var (ultima, recorrido) = await ConducirAsync(inicio, usuario);
        Assert.True(ultima.Tipo == TipoDePagina.CodigoEmitido,
            $"Keycloak no emitio el codigo. Pantalla: {ultima.Tipo}. Errores: {ultima.MensajesDeError}. Recorrido: {string.Join(" > ", recorrido)}");

        var respuesta = await EntregarRegresoAsync(ultima.Url);
        Assert.True(respuesta.Estado == HttpStatusCode.Redirect && respuesta.Cookie(NombreDeLaCookieDeSesion) is not null,
            $"El BFF no creo la sesion: {(int)respuesta.Estado} hacia {respuesta.Destino}.");
        return respuesta;
    }

    /// <summary>Una peticion del SPA al BFF: con X-CSRF: 1 y Sec-Fetch-Site: same-origin salvo que se diga otra cosa.</summary>
    public Task<RespuestaDelBff> PedirAsync(HttpMethod metodo, string rutaDelBff, bool conCsrf = true, string? secFetchSite = "same-origin") =>
        PedirAbsolutoAsync(metodo, new Uri($"http://{AutoridadDelBff}{rutaDelBff}"), conCsrf, secFetchSite);

    public Task<RespuestaDelBff> GetAsync(string rutaDelBff) => PedirAsync(HttpMethod.Get, rutaDelBff);

    public Task<RespuestaDelBff> PostAsync(string rutaDelBff) => PedirAsync(HttpMethod.Post, rutaDelBff);

    /// <summary>La sesion actual segun /bff/sesion (null si responde 401).</summary>
    public async Task<JsonNode?> ObtenerSesionAsync()
    {
        var respuesta = await GetAsync("/bff/sesion");
        return respuesta.Estado == HttpStatusCode.OK ? JsonNode.Parse(respuesta.Cuerpo) : null;
    }

    /// <summary>El codigo de error de una respuesta de problema (por ejemplo "sesion-vencida-por-inactividad").</summary>
    public static string? CodigoDe(RespuestaDelBff respuesta) => (string?)JsonNode.Parse(respuesta.Cuerpo)?["codigo"];

    /// <summary>La misma URI de regreso con un parametro cambiado (o quitado si valor es null).</summary>
    public static Uri ConParametro(Uri regreso, string nombre, string? valor)
    {
        var consulta = System.Web.HttpUtility.ParseQueryString(regreso.Query);
        if (valor is null)
            consulta.Remove(nombre);
        else
            consulta[nombre] = valor;

        return new UriBuilder(regreso) { Query = consulta.ToString() }.Uri;
    }

    // ---- Lectura de pantallas ----

    private async Task<RespuestaDelBff> PedirAbsolutoAsync(HttpMethod metodo, Uri uri, bool conCsrf, string? secFetchSite)
    {
        using var solicitud = new HttpRequestMessage(metodo, uri);
        if (conCsrf)
            solicitud.Headers.Add("X-CSRF", "1");
        if (secFetchSite is not null)
            solicitud.Headers.Add("Sec-Fetch-Site", secFetchSite);

        using var respuesta = await _http.SendAsync(solicitud, Ayudas.Ct);
        var ubicacion = respuesta.Headers.Location is { } l ? (l.IsAbsoluteUri ? l : new Uri(uri, l)) : null;
        var cookies = respuesta.Headers.TryGetValues("Set-Cookie", out var valores) ? valores.ToList() : [];
        return new RespuestaDelBff(respuesta.StatusCode, ubicacion, await respuesta.Content.ReadAsStringAsync(Ayudas.Ct), cookies);
    }

    private async Task<Pagina> SeguirAsync(Uri url)
    {
        using var respuesta = await _http.GetAsync(url, Ayudas.Ct);
        return await LeerAsync(respuesta, url);
    }

    /// <summary>
    /// Sigue las redirecciones. Se detiene (sin entregarlo) en el regreso a /bff/signin-oidc/* con el codigo, y en cualquier
    /// otro destino del BFF (la SPA: la pantalla a la que el BFF manda al usuario).
    /// </summary>
    private async Task<Pagina> LeerAsync(HttpResponseMessage respuesta, Uri url)
    {
        while ((int)respuesta.StatusCode is >= 300 and < 400 && respuesta.Headers.Location is { } destino)
        {
            var siguiente = destino.IsAbsoluteUri ? destino : new Uri(url, destino);
            if (siguiente.Authority == AutoridadDelBff)
                return PaginaDelBff(siguiente);

            respuesta.Dispose();
            url = siguiente;
            respuesta = await _http.GetAsync(url, Ayudas.Ct);
        }

        var html = await respuesta.Content.ReadAsStringAsync(Ayudas.Ct);
        var documento = _analizador.ParseDocument(html);
        return new Pagina { Tipo = NavegadorSimulado.TipoDe(documento), Url = url, Html = html, Documento = documento };
    }

    private static Pagina PaginaDelBff(Uri destino)
    {
        var consulta = System.Web.HttpUtility.ParseQueryString(destino.Query);
        var esRegreso = destino.AbsolutePath.StartsWith("/bff/signin-oidc/", StringComparison.Ordinal);
        return new Pagina
        {
            Tipo = esRegreso && consulta["code"] is not null ? TipoDePagina.CodigoEmitido : TipoDePagina.Otra,
            Url = destino,
            Html = "",
            CodigoDeAutorizacion = consulta["code"],
            ErrorDeOidc = consulta["error"],
        };
    }

    /// <summary>
    /// Enruta cada peticion por su autoridad (el BFF en memoria o Keycloak) y guarda las cookies a mano y por autoridad,
    /// ignorando Secure y Path (como NavegadorSimulado).
    /// </summary>
    private sealed class Enrutador(HttpMessageHandler bff, HttpMessageHandler keycloak) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _bff = new(bff, disposeHandler: false);
        private readonly HttpMessageInvoker _keycloak = new(keycloak, disposeHandler: false);
        private readonly Dictionary<string, Dictionary<string, string>> _jarras = [];

        public void Olvidar(string autoridad)
        {
            lock (_jarras)
                _jarras.Remove(autoridad);
        }

        public void Poner(string autoridad, string nombre, string valor)
        {
            lock (_jarras)
                Jarra(autoridad)[nombre] = valor;
        }

        public string? Valor(string autoridad, string nombre)
        {
            lock (_jarras)
                return Jarra(autoridad).GetValueOrDefault(nombre);
        }

        private Dictionary<string, string> Jarra(string autoridad)
        {
            if (!_jarras.TryGetValue(autoridad, out var jarra))
                _jarras[autoridad] = jarra = [];

            return jarra;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var autoridad = request.RequestUri!.Authority;
            lock (_jarras)
            {
                var jarra = Jarra(autoridad);
                if (jarra.Count > 0)
                    request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", jarra.Select(g => $"{g.Key}={g.Value}")));
            }

            var invoker = autoridad == AutoridadDelBff ? _bff : _keycloak;
            var respuesta = await invoker.SendAsync(request, cancellationToken);
            if (respuesta.Headers.TryGetValues("Set-Cookie", out var cabeceras))
            {
                lock (_jarras)
                {
                    foreach (var cabecera in cabeceras)
                        Guardar(Jarra(autoridad), cabecera);
                }
            }

            return respuesta;
        }

        private static void Guardar(Dictionary<string, string> jarra, string cabecera)
        {
            var partes = cabecera.Split(';').Select(p => p.Trim()).ToList();
            var separador = partes[0].IndexOf('=', StringComparison.Ordinal);
            var nombre = partes[0][..separador];
            var valor = partes[0][(separador + 1)..];
            var borrada = valor.Length == 0 || partes.Any(p => p.Equals("Max-Age=0", StringComparison.OrdinalIgnoreCase));

            if (borrada)
                jarra.Remove(nombre);
            else
                jarra[nombre] = valor;
        }
    }
}
