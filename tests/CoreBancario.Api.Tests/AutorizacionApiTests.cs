using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Errores;
using static CoreBancario.Api.Tests.Ayudas;
using static CoreBancario.Api.Tests.AyudasDeAutenticacion;

namespace CoreBancario.Api.Tests;

/// <summary>
/// 403 (prohibido): RN-19, CL-08, CL-09, CA-19. Y el uso normal del operador, ahora identificado: CA-20, RNF-05.
/// La autorizacion corre ANTES del endpoint: el recurso exista o no, la respuesta es la misma.
/// </summary>
public class AutorizacionApiTests(ApiFixture api)
{
    public static TheoryData<string, string> RutasDeOperador => RutasDelOperador();

    public static TheoryData<string, string> RutasDeCliente => RutasDelCliente();

    private static async Task AssertEsProhibidoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(403, problema.Status);
        Assert.Equal(CodigosDeError.Prohibido, problema.Codigo);
        Assert.Null(problema.Detail);
    }

    // ---- CA-19 / CL-08 / CL-09 ----

    [Theory]
    [MemberData(nameof(RutasDeOperador))]
    public async Task F003_CA19_ClienteLlamaOperacionDelOperador_Responde403(string metodo, string ruta)
    {
        // Arrange: ids inexistentes a proposito (CL-08: prohibido sin importar si el recurso existe).
        using var http = api.CrearClienteComoCliente(Guid.NewGuid().ToString());

        // Act
        var respuesta = await EnviarAsync(http, metodo, ruta);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
    }

    [Theory]
    [MemberData(nameof(RutasDeCliente))]
    public async Task F003_CA19_OperadorLlamaMisCuentas_Responde403(string metodo, string ruta)
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await EnviarAsync(http, metodo, ruta);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
    }

    [Fact]
    public async Task F003_CA19_ClienteRegistraCliente_Responde403YNoCreaElCliente()
    {
        // Arrange
        using var comoCliente = api.CrearClienteComoCliente(Guid.NewGuid().ToString());
        using var comoOperador = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();

        // Act
        var respuesta = await comoCliente.PostAsJsonAsync("/clientes", SolicitudDeRegistro("CC", numero), Ct);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
        var busqueda = await BuscarPorDocumentoAsync(comoOperador, "CC", numero);
        Assert.Equal(HttpStatusCode.NotFound, busqueda.StatusCode);
    }

    // ---- RN-19: el rol sale del realm que firmo el token Y de sus roles ----

    [Fact]
    public async Task F003_RN19_TokenDelRealmClientesConRolOperador_Responde403()
    {
        // Arrange: alguien crea el rol "operador" en el realm clientes; el rol de la API depende del esquema, no solo del token.
        var token = EmisorDePruebas.CrearToken(EmisorDeToken.Clientes, Guid.NewGuid().ToString(), [EmisorDePruebas.RolDeOperador]);
        using var http = api.CrearClienteConToken(token);

        // Act
        var respuesta = await http.GetAsync($"/cuentas/{Guid.NewGuid()}", Ct);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
    }

    [Fact]
    public async Task F003_RN19_TokenDelRealmEmpleadosConRolCliente_Responde403EnMisCuentas()
    {
        // Arrange
        var token = EmisorDePruebas.CrearToken(EmisorDeToken.Empleados, Guid.NewGuid().ToString(), [EmisorDePruebas.RolDeCliente]);
        using var http = api.CrearClienteConToken(token);

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
    }

    [Theory]
    [InlineData(nameof(EmisorDeToken.Empleados), "/cuentas/{id}")]
    [InlineData(nameof(EmisorDeToken.Clientes), "/mis-cuentas")]
    public async Task F003_RN19_TokenValidoSinRolDeLaApi_Responde403(string emisor, string ruta)
    {
        // Arrange: roles por defecto de Keycloak, ninguno de la API.
        var token = EmisorDePruebas.CrearToken(Enum.Parse<EmisorDeToken>(emisor), Guid.NewGuid().ToString(),
            ["uma_authorization"]);
        using var http = api.CrearClienteConToken(token);

        // Act
        var respuesta = await EnviarAsync(http, "GET", ruta);

        // Assert
        await AssertEsProhibidoAsync(respuesta);
    }

    // ---- CA-20 / RNF-05: la 002 sigue funcionando y el log dice quien la uso ----

    [Theory]
    [InlineData("RegistrarCliente")]
    [InlineData("AbrirCuenta")]
    [InlineData("BloquearCuenta")]
    [InlineData("DesbloquearCuenta")]
    [InlineData("CerrarCuenta")]
    public async Task F003_CA20_OperadorUsaOperacionesDe002_ElLogIncluyeSuIdentificador(string operacion)
    {
        // Arrange: el operador de la prueba tiene un sub unico para encontrar SU registro entre los de las demas pruebas.
        var sub = $"operador-{Guid.NewGuid()}";
        using var preparacion = api.CrearCliente();
        using var solicitud = await PrepararOperacionAsync(preparacion, operacion);
        using var http = api.CrearClienteConToken(EmisorDePruebas.CrearTokenDeOperador(sub));

        // Act
        var respuesta = await http.SendAsync(solicitud, Ct);

        // Assert
        Assert.True(respuesta.IsSuccessStatusCode, $"{operacion} respondio {(int)respuesta.StatusCode}.");
        Assert.Contains(api.Logs.QueContienen(sub),
            r => r.Nivel == LogLevel.Information
                 && r.Mensaje.Contains(operacion, StringComparison.Ordinal)
                 && r.Mensaje.EndsWith(": completada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task F003_RNF05_OperacionDelOperadorQueFalla_ElLogDiceNoCompletadaConSuIdentificador()
    {
        // Arrange
        var sub = $"operador-{Guid.NewGuid()}";
        using var http = api.CrearClienteConToken(EmisorDePruebas.CrearTokenDeOperador(sub));

        // Act: abrir una cuenta a un cliente que no existe.
        var respuesta = await http.PostAsJsonAsync($"/clientes/{Guid.NewGuid()}/cuentas", new { moneda = "COP" }, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains(api.Logs.QueContienen(sub),
            r => r.Mensaje.Contains("AbrirCuenta", StringComparison.Ordinal)
                 && r.Mensaje.EndsWith(": no completada", StringComparison.Ordinal));
    }

    /// <summary>Deja listos los datos y devuelve la peticion de la operacion que se va a probar.</summary>
    private static async Task<HttpRequestMessage> PrepararOperacionAsync(HttpClient preparacion, string operacion)
    {
        if (operacion == "RegistrarCliente")
            return new HttpRequestMessage(HttpMethod.Post, "/clientes") { Content = JsonContent.Create(SolicitudDeRegistro()) };

        var cliente = await RegistrarClienteAsync(preparacion);
        if (operacion == "AbrirCuenta")
            return new HttpRequestMessage(HttpMethod.Post, $"/clientes/{cliente.Id}/cuentas") { Content = JsonContent.Create(new { moneda = "COP" }) };

        var cuenta = await AbrirCuentaAsync(preparacion, cliente.Id);
        if (operacion == "DesbloquearCuenta")
            (await preparacion.PostAsync($"/cuentas/{cuenta.Id}/bloquear", null, Ct)).EnsureSuccessStatusCode();

        var accion = operacion switch
        {
            "BloquearCuenta" => "bloquear",
            "DesbloquearCuenta" => "desbloquear",
            "CerrarCuenta" => "cerrar",
            _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, "Operacion desconocida."),
        };
        return new HttpRequestMessage(HttpMethod.Post, $"/cuentas/{cuenta.Id}/{accion}");
    }
}
