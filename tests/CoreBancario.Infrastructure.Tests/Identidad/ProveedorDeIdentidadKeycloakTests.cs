using System.Net;
using System.Text.Json.Nodes;
using System.Web;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Infrastructure.Identidad;
using Microsoft.Extensions.Options;
using static CoreBancario.Infrastructure.Tests.Identidad.ManejadorHttpDeGuion;

namespace CoreBancario.Infrastructure.Tests.Identidad;

/// <summary>
/// ADR-0017, plan sección 4 ("Llamadas a Keycloak"): lo que el adaptador pide a la Admin REST API, en qué orden, y cómo
/// traduce los fallos. Aquí no hay Keycloak: un servidor HTTP falso registra las peticiones. Que Keycloak haga de verdad
/// lo que el adaptador cree lo comprueba Keycloak.Tests.
/// </summary>
public class ProveedorDeIdentidadKeycloakTests
{
    private const string Sub = "9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10";
    private const string RutaDelUsuario = $"/admin/realms/clientes/users/{Sub}";
    private const string CuerpoInterno = "detalle-interno-confidencial";
    private const string RepresentacionDelUsuario = $$$"""
        {"id":"{{{Sub}}}","username":"cc1234567","enabled":false,"emailVerified":false,
         "requiredActions":[],"attributes":{"otro_atributo":["conservar"]}}
        """;

    private static readonly Guid Marca = Guid.NewGuid();
    // No es un campo estático: ClaveTemporal es de la implementación y un fallo al construirla no debe romper todo el tipo.
    private static ClaveTemporal NuevaClave() => new("Xq7mK2vB9nLw4TpZ");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProveedorDeIdentidadKeycloak CrearProveedor(ManejadorHttpDeGuion manejador)
    {
        var http = new HttpClient(manejador) { BaseAddress = new Uri("http://keycloak.test/") };
        var opciones = Options.Create(new OpcionesDeKeycloak { UrlBase = http.BaseAddress, Realm = "clientes" });
        return new ProveedorDeIdentidadKeycloak(http, opciones);
    }

    /// <summary>Un Keycloak que responde bien a todo lo que el adaptador pide para reiniciar o deshabilitar un acceso.</summary>
    private static HttpResponseMessage KeycloakSano(PeticionCapturada peticion) =>
        (peticion.Metodo.Method, peticion.Ruta) switch
        {
            ("GET", RutaDelUsuario) => Json(HttpStatusCode.OK, RepresentacionDelUsuario),
            ("GET", RutaDelUsuario + "/credentials") => Json(HttpStatusCode.OK,
                """[{"id":"c-clave","type":"password"},{"id":"c-otp-1","type":"otp"},{"id":"c-otp-2","type":"otp"}]"""),
            _ => Vacia(HttpStatusCode.NoContent),
        };

    private static string[] Resumen(ManejadorHttpDeGuion manejador) =>
        [.. manejador.Peticiones.Select(p => $"{p.Metodo} {p.Ruta[RutaDelUsuario.Length..]}")];

    /// <summary>Las cinco operaciones del puerto, para las pruebas que valen para todas (traducción de errores).</summary>
    public static TheoryData<string> Operaciones =>
    [
        nameof(IProveedorDeIdentidad.BuscarPorNombreDeUsuarioAsync),
        nameof(IProveedorDeIdentidad.ObtenerAsync),
        nameof(IProveedorDeIdentidad.CrearAsync),
        nameof(IProveedorDeIdentidad.ReiniciarAccesoAsync),
        nameof(IProveedorDeIdentidad.DeshabilitarAsync),
    ];

    private static Task Llamar(ProveedorDeIdentidadKeycloak proveedor, string operacion) =>
        operacion switch
        {
            nameof(IProveedorDeIdentidad.BuscarPorNombreDeUsuarioAsync) => proveedor.BuscarPorNombreDeUsuarioAsync("CC1234567", Ct),
            nameof(IProveedorDeIdentidad.ObtenerAsync) => proveedor.ObtenerAsync(Sub, Ct),
            nameof(IProveedorDeIdentidad.CrearAsync) => proveedor.CrearAsync("CC1234567", NuevaClave(), Marca, Ct),
            nameof(IProveedorDeIdentidad.ReiniciarAccesoAsync) => proveedor.ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct),
            nameof(IProveedorDeIdentidad.DeshabilitarAsync) => proveedor.DeshabilitarAsync(Sub, Marca, Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, "Operación desconocida."),
        };

    // ---- Lecturas ----

    [Fact]
    public async Task F003_ADR0017_BuscarPorNombreDeUsuario_UsaBusquedaExactaEnMinusculas()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, "[]"));

        // Act
        await CrearProveedor(manejador).BuscarPorNombreDeUsuarioAsync("CC1234567", Ct);

        // Assert
        var peticion = Assert.Single(manejador.Peticiones);
        Assert.Equal(HttpMethod.Get, peticion.Metodo);
        Assert.Equal("/admin/realms/clientes/users", peticion.Ruta);
        var consulta = HttpUtility.ParseQueryString(peticion.Consulta);
        Assert.Equal("cc1234567", consulta["username"]);
        Assert.Equal("true", consulta["exact"]);
    }

    [Fact]
    public async Task F003_ADR0017_BuscarPorNombreDeUsuarioSinCoincidencias_DevuelveNull()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, "[]"));

        // Act
        var usuario = await CrearProveedor(manejador).BuscarPorNombreDeUsuarioAsync("CC1234567", Ct);

        // Assert
        Assert.Null(usuario);
    }

    [Fact]
    public async Task F003_ADR0017_BuscarPorNombreDeUsuarioConCoincidencia_DevuelveSubEstadoYMarca()
    {
        // Arrange
        var cuerpo = $$$"""[{"id":"{{{Sub}}}","username":"cc1234567","enabled":true,"attributes":{"ultima_solicitud_de_acceso":["{{{Marca}}}"]}}]""";
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, cuerpo));

        // Act
        var usuario = await CrearProveedor(manejador).BuscarPorNombreDeUsuarioAsync("CC1234567", Ct);

        // Assert
        Assert.Equal(new UsuarioDeIdentidad(Sub, true, Marca), usuario);
    }

    [Fact]
    public async Task F003_ADR0017_ObtenerUsuarioInexistente_DevuelveNull()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => Vacia(HttpStatusCode.NotFound));

        // Act
        var usuario = await CrearProveedor(manejador).ObtenerAsync(Sub, Ct);

        // Assert
        Assert.Null(usuario);
        Assert.Equal(RutaDelUsuario, Assert.Single(manejador.Peticiones).Ruta);
    }

    [Fact]
    public async Task F003_ADR0017_ObtenerAsync_LeeSubEstadoYMarca()
    {
        // Arrange
        var cuerpo = $$$"""{"id":"{{{Sub}}}","username":"cc1234567","enabled":true,"attributes":{"ultima_solicitud_de_acceso":["{{{Marca}}}"]}}""";
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, cuerpo));

        // Act
        var usuario = await CrearProveedor(manejador).ObtenerAsync(Sub, Ct);

        // Assert
        Assert.Equal(new UsuarioDeIdentidad(Sub, true, Marca), usuario);
    }

    [Theory]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":false}""")]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":false,"attributes":{}}""")]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":false,"attributes":{"ultima_solicitud_de_acceso":["no-es-un-guid"]}}""")]
    public async Task F003_ADR0017_ObtenerAsyncSinMarcaUtil_LaDejaNull(string cuerpo)
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, cuerpo));

        // Act
        var usuario = await CrearProveedor(manejador).ObtenerAsync(Sub, Ct);

        // Assert
        Assert.Equal(new UsuarioDeIdentidad(Sub, false, null), usuario);
    }

    [Theory]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":true,"attributes":{"ultima_solicitud_de_acceso":[123]}}""")]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":true,"attributes":{"ultima_solicitud_de_acceso":[{"x":1}]}}""")]
    [InlineData("""{"id":"9d1f0c4e-5b1a-4c3e-8d0a-6f2b7a1c9e10","enabled":true,"attributes":{"ultima_solicitud_de_acceso":[true]}}""")]
    public async Task F003_ADR0017_ObtenerAsyncConMarcaQueNoEsTexto_LanzaErrorDelProveedor(string cuerpo)
    {
        // Arrange: una respuesta inesperada de Keycloak debe ser un 502, no un 500 por InvalidOperationException.
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.OK, cuerpo));

        // Act
        var accion = () => CrearProveedor(manejador).ObtenerAsync(Sub, Ct);

        // Assert
        await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(accion);
    }

    // ---- Crear: una sola petición ----

    [Fact]
    public async Task F003_CA01_CrearAsync_DevuelveElSubDeLaCabeceraLocation()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ =>
        {
            var respuesta = Vacia(HttpStatusCode.Created);
            respuesta.Headers.Location = new Uri($"http://keycloak.test/admin/realms/clientes/users/{Sub}");
            return respuesta;
        });

        // Act
        var sub = await CrearProveedor(manejador).CrearAsync("CC1234567", NuevaClave(), Marca, Ct);

        // Assert
        Assert.Equal(Sub, sub);
    }

    [Fact]
    public async Task F003_CA01_CrearAsync_EnviaUnaSolaPeticionConElUsuarioCompleto()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ =>
        {
            var respuesta = Vacia(HttpStatusCode.Created);
            respuesta.Headers.Location = new Uri($"http://keycloak.test/admin/realms/clientes/users/{Sub}");
            return respuesta;
        });

        // Act
        await CrearProveedor(manejador).CrearAsync("CC1234567", NuevaClave(), Marca, Ct);

        // Assert
        var peticion = Assert.Single(manejador.Peticiones);
        Assert.Equal(HttpMethod.Post, peticion.Metodo);
        Assert.Equal("/admin/realms/clientes/users", peticion.Ruta);
        var usuario = JsonNode.Parse(peticion.Cuerpo!)!;
        Assert.Equal("cc1234567", ((string?)usuario["username"])?.ToLowerInvariant());
        Assert.True((bool?)usuario["enabled"]);
        Assert.Equivalent(new[] { "UPDATE_PASSWORD", "CONFIGURE_TOTP" }, usuario["requiredActions"]!.AsArray().Select(a => (string)a!));
        var credencial = Assert.Single(usuario["credentials"]!.AsArray())!;
        Assert.Equal("password", (string?)credencial["type"]);
        Assert.Equal(NuevaClave().Valor, (string?)credencial["value"]);
        Assert.True((bool?)credencial["temporary"]);
        Assert.Equal([Marca.ToString()], usuario["attributes"]!["ultima_solicitud_de_acceso"]!.AsArray().Select(m => (string)m!));
    }

    // ---- Reiniciar acceso (habilitar o restablecer): orden de las peticiones ----

    [Fact]
    public async Task F003_CA02_ReiniciarAcceso_HaceLasPeticionesEnElOrdenDelPlan()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(KeycloakSano);

        // Act
        await CrearProveedor(manejador).ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct);

        // Assert: clave temporal → borrar solo las credenciales OTP → cerrar sesiones → representación completa con la marca.
        var resumen = Resumen(manejador);
        Assert.Equal(["PUT /reset-password", "GET /credentials"], resumen[..2]);
        Assert.Equivalent(new[] { "DELETE /credentials/c-otp-1", "DELETE /credentials/c-otp-2" }, resumen[2..4]);
        Assert.Equal(["POST /logout", "GET ", "PUT "], resumen[4..]);
    }

    [Fact]
    public async Task F003_CA02_ReiniciarAcceso_PoneLaClaveTemporalEnElPrimerPaso()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(KeycloakSano);

        // Act
        await CrearProveedor(manejador).ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct);

        // Assert
        var primera = manejador.Peticiones[0];
        Assert.Equal($"{RutaDelUsuario}/reset-password", primera.Ruta);
        var credencial = JsonNode.Parse(primera.Cuerpo!)!;
        Assert.Equal("password", (string?)credencial["type"]);
        Assert.Equal(NuevaClave().Valor, (string?)credencial["value"]);
        Assert.True((bool?)credencial["temporary"]);
    }

    [Fact]
    public async Task F003_ADR0017_ReiniciarAcceso_LaMarcaVaEnLaUltimaPeticionConLaRepresentacionCompleta()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(KeycloakSano);

        // Act
        await CrearProveedor(manejador).ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct);

        // Assert: Keycloak reemplaza "attributes" entero, así que el PUT lleva todo lo que el GET trajo, ya modificado.
        var ultima = manejador.Peticiones[^1];
        Assert.Equal(HttpMethod.Put, ultima.Metodo);
        Assert.Equal(RutaDelUsuario, ultima.Ruta);
        var usuario = JsonNode.Parse(ultima.Cuerpo!)!;
        Assert.True((bool?)usuario["enabled"]);
        Assert.Equivalent(new[] { "UPDATE_PASSWORD", "CONFIGURE_TOTP" }, usuario["requiredActions"]!.AsArray().Select(a => (string)a!));
        Assert.Equal([Marca.ToString()], usuario["attributes"]!["ultima_solicitud_de_acceso"]!.AsArray().Select(m => (string)m!));
        Assert.Equal(["conservar"], usuario["attributes"]!["otro_atributo"]!.AsArray().Select(m => (string)m!));
        Assert.Equal("cc1234567", (string?)usuario["username"]);
    }

    [Fact]
    public async Task F003_ADR0017_ReiniciarAccesoQueFallaAlCerrarLasSesiones_NoEscribeLaMarca()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(p =>
            p.Ruta.EndsWith("/logout", StringComparison.Ordinal) ? Vacia(HttpStatusCode.InternalServerError) : KeycloakSano(p));

        // Act
        var accion = () => CrearProveedor(manejador).ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct);

        // Assert: si la marca coincidiera sin haberse aplicado todo, el reintento daría por hecho lo que no pasó.
        await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(accion);
        Assert.DoesNotContain(manejador.Peticiones, p => p.Metodo == HttpMethod.Put && p.Ruta == RutaDelUsuario);
    }

    // ---- Deshabilitar ----

    [Fact]
    public async Task F003_CA03_DeshabilitarAsync_CierraSesionesAntesDeDeshabilitar()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(KeycloakSano);

        // Act
        await CrearProveedor(manejador).DeshabilitarAsync(Sub, Marca, Ct);

        // Assert
        Assert.Equal(["POST /logout", "GET ", "PUT "], Resumen(manejador));
    }

    [Fact]
    public async Task F003_CA03_DeshabilitarAsync_LaUltimaPeticionDeshabilitaYPoneLaMarca()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(KeycloakSano);

        // Act
        await CrearProveedor(manejador).DeshabilitarAsync(Sub, Marca, Ct);

        // Assert
        var ultima = manejador.Peticiones[^1];
        Assert.Equal(HttpMethod.Put, ultima.Metodo);
        var usuario = JsonNode.Parse(ultima.Cuerpo!)!;
        Assert.False((bool?)usuario["enabled"]);
        Assert.Equal([Marca.ToString()], usuario["attributes"]!["ultima_solicitud_de_acceso"]!.AsArray().Select(m => (string)m!));
        Assert.Equal(["conservar"], usuario["attributes"]!["otro_atributo"]!.AsArray().Select(m => (string)m!));
    }

    [Fact]
    public async Task F003_ADR0017_DeshabilitarQueFallaAlCerrarLasSesiones_NoEscribeLaMarca()
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(p =>
            p.Ruta.EndsWith("/logout", StringComparison.Ordinal) ? Vacia(HttpStatusCode.BadGateway) : KeycloakSano(p));

        // Act
        var accion = () => CrearProveedor(manejador).DeshabilitarAsync(Sub, Marca, Ct);

        // Assert
        await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(accion);
        Assert.DoesNotContain(manejador.Peticiones, p => p.Metodo == HttpMethod.Put);
    }

    // ---- Traducción de errores ----

    [Theory]
    [MemberData(nameof(Operaciones))]
    public async Task F003_ADR0017_RespuestaDeError_LanzaErrorDelProveedorSinElCuerpo(
        string operacion)
    {
        // Arrange: el cuerpo de un error de Keycloak puede traer datos internos; no debe viajar en el motivo ni en el mensaje.
        var manejador = new ManejadorHttpDeGuion(_ => Json(HttpStatusCode.InternalServerError, $$"""{"error":"{{CuerpoInterno}}"}"""));
        var proveedor = CrearProveedor(manejador);

        // Act
        var error = await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(() => Llamar(proveedor, operacion));

        // Assert
        Assert.StartsWith("HTTP 500", error.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(CuerpoInterno, error.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(CuerpoInterno, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F003_RNF03_ErrorAlCrear_NoIncluyeLaClaveTemporalEnElMotivo()
    {
        // Arrange: un servidor que repite en el error lo que recibió.
        var manejador = new ManejadorHttpDeGuion(p => Json(HttpStatusCode.BadRequest, $$"""{"recibido":{{p.Cuerpo}}}"""));

        // Act
        var error = await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(
            () => CrearProveedor(manejador).CrearAsync("CC1234567", NuevaClave(), Marca, Ct));

        // Assert
        Assert.DoesNotContain(NuevaClave().Valor, error.Motivo, StringComparison.Ordinal);
        Assert.DoesNotContain(NuevaClave().Valor, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F003_ADR0017_ReiniciarUsuarioInexistente_LanzaErrorDelProveedor()
    {
        // Arrange: solo el GET de un usuario inexistente da null; un cambio sobre uno inexistente es un error.
        var manejador = new ManejadorHttpDeGuion(_ => Vacia(HttpStatusCode.NotFound));

        // Act
        var accion = () => CrearProveedor(manejador).ReiniciarAccesoAsync(Sub, NuevaClave(), Marca, Ct);

        // Assert
        var error = await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(accion);
        Assert.StartsWith("HTTP 404", error.Motivo, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Operaciones))]
    public async Task F003_ADR0017_TiempoDeEsperaAgotado_LanzaSinRespuesta(
        string operacion)
    {
        // Arrange: el HttpClient agota su tiempo y lanza TaskCanceledException con un TimeoutException dentro; nadie canceló.
        var manejador = new ManejadorHttpDeGuion(_ => throw new TaskCanceledException("Tiempo agotado.", new TimeoutException()));
        var proveedor = CrearProveedor(manejador);

        // Act
        var accion = () => Llamar(proveedor, operacion);

        // Assert
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(accion);
    }

    [Theory]
    [MemberData(nameof(Operaciones))]
    public async Task F003_ADR0017_ConexionRechazada_LanzaSinRespuesta(
        string operacion)
    {
        // Arrange
        var manejador = new ManejadorHttpDeGuion(_ => throw new HttpRequestException("Conexión rechazada."));
        var proveedor = CrearProveedor(manejador);

        // Act
        var accion = () => Llamar(proveedor, operacion);

        // Assert
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(accion);
    }

    [Fact]
    public async Task F003_ADR0017_CancelacionDeQuienLlama_NoSeTraduceASinRespuesta()
    {
        // Arrange: quien llama canceló; eso no es "el sistema de identidad no respondió".
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();
        var manejador = new ManejadorHttpDeGuion(_ => Vacia(HttpStatusCode.OK));
        var proveedor = CrearProveedor(manejador);

        // Act
        var accion = () => proveedor.ObtenerAsync(Sub, cancelacion.Token);

        // Assert
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(accion);
        Assert.IsNotType<ProveedorDeIdentidadSinRespuestaException>(error);
    }
}
