using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// Admin REST API de Keycloak con el administrador de master del contenedor (KC_BOOTSTRAP_ADMIN_*). Solo la usa el
/// fixture y las pruebas para preparar usuarios y tiempos; la API real (003b) usara su propia cuenta de servicio.
/// </summary>
public sealed class AdministracionDeKeycloak(string urlBase, string usuario, string clave) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(urlBase), Timeout = TimeSpan.FromMinutes(2) };
    private readonly SemaphoreSlim _candado = new(1, 1);
    private string? _token;
    private DateTimeOffset _vence;

    public void Dispose()
    {
        _http.Dispose();
        _candado.Dispose();
    }

    // El token de master vive 60 s: se renueva cuando le quedan menos de 15.
    private async Task<string> TokenAsync()
    {
        await _candado.WaitAsync(Ayudas.Ct);
        try
        {
            if (_token is not null && _vence > DateTimeOffset.UtcNow.AddSeconds(15))
                return _token;

            using var respuesta = await _http.PostAsync("/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = "admin-cli",
                    ["username"] = usuario,
                    ["password"] = clave,
                }), Ayudas.Ct);
            respuesta.EnsureSuccessStatusCode();

            var json = (await respuesta.Content.ReadFromJsonAsync<JsonNode>(Ayudas.Ct))!;
            _token = (string)json["access_token"]!;
            _vence = DateTimeOffset.UtcNow.AddSeconds((int)json["expires_in"]!);
            return _token;
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, JsonNode? cuerpo = null)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
        if (cuerpo is not null)
            solicitud.Content = JsonContent.Create(cuerpo);

        return await _http.SendAsync(solicitud, Ayudas.Ct);
    }

    private async Task<JsonNode> LeerAsync(string ruta)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<JsonNode>(Ayudas.Ct))!;
    }

    private static async Task AseguraExitoAsync(HttpResponseMessage respuesta, string queSeHacia)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(
            $"Keycloak respondio {(int)respuesta.StatusCode} al {queSeHacia}: {await respuesta.Content.ReadAsStringAsync(Ayudas.Ct)}");
    }

    /// <summary>Cambia ajustes del realm (PUT parcial: Keycloak ignora lo que no se envia).</summary>
    public async Task ActualizarRealmAsync(string realm, JsonObject ajustes)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Put, $"/admin/realms/{realm}", ajustes);
        await AseguraExitoAsync(respuesta, $"actualizar el realm {realm}");
    }

    /// <summary>Registra la URI de redireccion de las pruebas en el cliente core-bancario-web del realm.</summary>
    public async Task RegistrarUriDeRedireccionAsync(string realm, string clientId, string uri)
    {
        var encontrados = await LeerAsync($"/admin/realms/{realm}/clients?clientId={Uri.EscapeDataString(clientId)}");
        var cliente = encontrados.AsArray().Single()!.AsObject();
        cliente["redirectUris"] = new JsonArray(uri);
        cliente["webOrigins"] = new JsonArray("+");

        using var respuesta = await EnviarAsync(HttpMethod.Put, $"/admin/realms/{realm}/clients/{(string)cliente["id"]!}", cliente);
        await AseguraExitoAsync(respuesta, $"registrar la redireccion en {clientId}");
    }

    /// <summary>
    /// Crea un usuario con solo el nombre de usuario (sin correo ni nombres, como hara la API en la 003b) y devuelve su
    /// sub. Con temporal = true, Keycloak exige cambiar la clave en el primer ingreso.
    /// </summary>
    public async Task<string> CrearUsuarioAsync(string realm, string nombreDeUsuario, string clave, bool temporal,
        string[] accionesObligatorias, bool habilitado = true)
    {
        var usuario = new JsonObject
        {
            ["username"] = nombreDeUsuario,
            ["enabled"] = habilitado,
            ["requiredActions"] = new JsonArray(accionesObligatorias.Select(a => (JsonNode)a).ToArray()),
            ["credentials"] = new JsonArray(new JsonObject { ["type"] = "password", ["value"] = clave, ["temporary"] = temporal }),
        };

        using var respuesta = await EnviarAsync(HttpMethod.Post, $"/admin/realms/{realm}/users", usuario);
        await AseguraExitoAsync(respuesta, $"crear el usuario {nombreDeUsuario} en {realm}");

        // El sub sale de la cabecera Location: .../users/{sub}
        return respuesta.Headers.Location!.Segments[^1];
    }

    public async Task AsignarRolDeRealmAsync(string realm, string sub, string rol)
    {
        var representacion = await LeerAsync($"/admin/realms/{realm}/roles/{rol}");
        using var respuesta = await EnviarAsync(HttpMethod.Post, $"/admin/realms/{realm}/users/{sub}/role-mappings/realm",
            new JsonArray(representacion));
        await AseguraExitoAsync(respuesta, $"asignar el rol {rol}");
    }

    /// <summary>Deshabilita al usuario (plan, seccion 1.1, paso 4): es lo que hara DeshabilitarAsync en la 003b.</summary>
    public async Task DeshabilitarUsuarioAsync(string realm, string sub)
    {
        var usuario = (await LeerAsync($"/admin/realms/{realm}/users/{sub}")).AsObject();
        usuario["enabled"] = false;

        using var respuesta = await EnviarAsync(HttpMethod.Put, $"/admin/realms/{realm}/users/{sub}", usuario);
        await AseguraExitoAsync(respuesta, "deshabilitar al usuario");
    }

    public async Task<bool> ExisteUsuarioAsync(string realm, string sub)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"/admin/realms/{realm}/users/{sub}");
        return respuesta.StatusCode != HttpStatusCode.NotFound;
    }
}
