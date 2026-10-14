using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBancario.Application.AccesosDigitales;
using Microsoft.Extensions.Options;

namespace CoreBancario.Infrastructure.Identidad;

/// <summary>
/// Cliente tipado de la Admin REST API (plan, sección 4, "Llamadas a Keycloak"). Traduce:
///   respuesta 2xx → éxito; GET de usuario con 404 → null;
///   cualquier otra respuesta → ErrorDelProveedorDeIdentidadException("HTTP {estado} al {paso}") (sin el cuerpo);
///   HttpRequestException sin respuesta, o TaskCanceledException por tiempo de espera (no por el token de quien
///   llama) → ProveedorDeIdentidadSinRespuestaException.
/// Compara nombres de usuario en minúsculas: Keycloak los guarda así.
/// </summary>
public sealed class ProveedorDeIdentidadKeycloak(HttpClient http, IOptions<OpcionesDeKeycloak> opciones) : IProveedorDeIdentidad
{
    private const string AtributoDeMarca = "ultima_solicitud_de_acceso";
    private static readonly string[] AccionesObligatorias = ["UPDATE_PASSWORD", "CONFIGURE_TOTP"];

    private string RutaDeUsuarios => $"admin/realms/{opciones.Value.Realm}/users";

    public async Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken)
    {
        var nombre = Uri.EscapeDataString(nombreDeUsuario.ToLowerInvariant());
        using var respuesta = await EnviarAsync(
            HttpMethod.Get, $"{RutaDeUsuarios}?username={nombre}&exact=true", null, "buscar el usuario", cancellationToken);

        var usuarios = await LeerJsonAsync(respuesta, "buscar el usuario", cancellationToken) as JsonArray;
        return usuarios is { Count: > 0 } ? ComoUsuario(usuarios[0]) : null;
    }

    public async Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(
            HttpMethod.Get, $"{RutaDeUsuarios}/{sub}", null, "obtener el usuario", cancellationToken, permitirNoEncontrado: true);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return ComoUsuario(await LeerJsonAsync(respuesta, "obtener el usuario", cancellationToken));
    }

    public async Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        var usuario = new JsonObject
        {
            ["username"] = nombreDeUsuario.ToLowerInvariant(),
            ["enabled"] = true,
            ["requiredActions"] = AccionesComoJson(),
            ["credentials"] = new JsonArray(CredencialTemporal(claveTemporal)),
            ["attributes"] = new JsonObject { [AtributoDeMarca] = new JsonArray(marca.ToString()) },
        };

        using var respuesta = await EnviarAsync(HttpMethod.Post, RutaDeUsuarios, usuario, "crear el usuario", cancellationToken);

        // El sub lo asigna Keycloak y viaja en la última parte de la ruta de la cabecera Location.
        var ubicacion = respuesta.Headers.Location
            ?? throw new ErrorDelProveedorDeIdentidadException(
                $"HTTP {(int)respuesta.StatusCode} sin cabecera Location al crear el usuario");

        return ubicacion.Segments[^1].TrimEnd('/');
    }

    public async Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken)
    {
        var ruta = $"{RutaDeUsuarios}/{sub}";

        using (await EnviarAsync(HttpMethod.Put, $"{ruta}/reset-password", CredencialTemporal(claveTemporal),
                   "poner la clave temporal", cancellationToken))
        {
        }

        await BorrarCredencialesOtpAsync(ruta, cancellationToken);
        await CerrarSesionesAsync(ruta, cancellationToken);

        // La marca va en el último paso: si coincide, se aplicó todo lo anterior (ADR-0017).
        await ActualizarUsuarioAsync(ruta, marca, usuario =>
        {
            usuario["enabled"] = true;
            usuario["requiredActions"] = AccionesComoJson();
        }, cancellationToken);
    }

    public async Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken)
    {
        var ruta = $"{RutaDeUsuarios}/{sub}";

        // Primero las sesiones: si cerrarlas fallara después de deshabilitar, la marca diría "completado" sin serlo.
        await CerrarSesionesAsync(ruta, cancellationToken);
        await ActualizarUsuarioAsync(ruta, marca, usuario => usuario["enabled"] = false, cancellationToken);
    }

    private async Task BorrarCredencialesOtpAsync(string ruta, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(
            HttpMethod.Get, $"{ruta}/credentials", null, "consultar las credenciales", cancellationToken);

        var credenciales = await LeerJsonAsync(respuesta, "consultar las credenciales", cancellationToken) as JsonArray;
        var idsDeOtp = (credenciales ?? [])
            .Where(c => (string?)c?["type"] == "otp")
            .Select(c => (string?)c?["id"])
            .OfType<string>();

        foreach (var id in idsDeOtp)
        {
            using (await EnviarAsync(HttpMethod.Delete, $"{ruta}/credentials/{id}", null,
                       "borrar el segundo factor", cancellationToken))
            {
            }
        }
    }

    private async Task CerrarSesionesAsync(string ruta, CancellationToken cancellationToken)
    {
        using (await EnviarAsync(HttpMethod.Post, $"{ruta}/logout", null, "cerrar las sesiones", cancellationToken))
        {
        }
    }

    /// <summary>
    /// Lee la representación completa, la modifica y la reemplaza con la marca. Keycloak reemplaza "attributes"
    /// entero, así que no se envía una representación parcial.
    /// </summary>
    private async Task ActualizarUsuarioAsync(string ruta, Guid marca, Action<JsonObject> cambiar, CancellationToken cancellationToken)
    {
        using var lectura = await EnviarAsync(HttpMethod.Get, ruta, null, "leer el usuario", cancellationToken);
        var usuario = await LeerJsonAsync(lectura, "leer el usuario", cancellationToken) as JsonObject
            ?? throw new ErrorDelProveedorDeIdentidadException("Respuesta ilegible al leer el usuario");

        cambiar(usuario);
        var atributos = usuario["attributes"] as JsonObject ?? [];
        atributos[AtributoDeMarca] = new JsonArray(marca.ToString());
        usuario["attributes"] = atributos;

        using (await EnviarAsync(HttpMethod.Put, ruta, usuario, "actualizar el usuario", cancellationToken))
        {
        }
    }

    private static JsonArray AccionesComoJson() => new([.. AccionesObligatorias.Select(a => JsonValue.Create(a))]);

    private static JsonObject CredencialTemporal(ClaveTemporal clave) => new()
    {
        ["type"] = "password",
        ["value"] = clave.Valor,
        ["temporary"] = true,
    };

    private static UsuarioDeIdentidad ComoUsuario(JsonNode? usuario)
    {
        var sub = (string?)usuario?["id"]
            ?? throw new ErrorDelProveedorDeIdentidadException("Respuesta sin identificador al leer el usuario");

        var habilitado = (bool?)usuario?["enabled"] ?? false;
        var textoDeLaMarca = (usuario?["attributes"]?[AtributoDeMarca] as JsonArray)?.FirstOrDefault()?.GetValue<string>();
        Guid? marca = Guid.TryParse(textoDeLaMarca, out var guid) ? guid : null;

        return new UsuarioDeIdentidad(sub, habilitado, marca);
    }

    private static async Task<JsonNode?> LeerJsonAsync(HttpResponseMessage respuesta, string paso, CancellationToken cancellationToken)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
        }
        catch (JsonException error)
        {
            throw new ErrorDelProveedorDeIdentidadException($"Respuesta ilegible al {paso}", error);
        }
    }

    /// <summary>Envía la petición y traduce los fallos (ver el resumen de la clase). El cuerpo de un error nunca viaja en la excepción.</summary>
    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string ruta, JsonNode? cuerpo, string paso, CancellationToken cancellationToken,
        bool permitirNoEncontrado = false)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(peticion, cancellationToken);
        }
        catch (HttpRequestException error)
        {
            throw new ProveedorDeIdentidadSinRespuestaException(error);
        }
        catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            // Tiempo de espera del HttpClient: nadie canceló. Un tiempo agotado no es un "no".
            throw new ProveedorDeIdentidadSinRespuestaException(error);
        }

        if (respuesta.IsSuccessStatusCode || (permitirNoEncontrado && respuesta.StatusCode == System.Net.HttpStatusCode.NotFound))
            return respuesta;

        var estado = (int)respuesta.StatusCode;
        respuesta.Dispose();
        throw new ErrorDelProveedorDeIdentidadException($"HTTP {estado} al {paso}");
    }
}
