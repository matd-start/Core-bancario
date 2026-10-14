using System.Text.Json.Nodes;

namespace CoreBancario.Api.Tests;

/// <summary>
/// Configuracion de los realms de PRODUCCION (infra/keycloak/realms/*.json). No arranca nada: lee los JSON y
/// verifica los valores reales (ADR-0018). El comportamiento de esos ajustes lo comprueba Keycloak.Tests.
/// Solo se verifica lo que el realm importa; la cuenta de servicio y su secreto son de la 003b.
/// </summary>
public class ConfiguracionDeRealmsTests
{
    /// <summary>El JSON de import de un realm. Lanza si el archivo no existe.</summary>
    internal static JsonNode Cargar(string realm)
    {
        var ruta = Path.Combine(AyudasDeAutenticacion.RaizDelRepositorio(), "infra", "keycloak", "realms", $"{realm}-realm.json");
        return JsonNode.Parse(File.ReadAllText(ruta))!;
    }

    private static JsonNode ClienteWeb(string realm) =>
        Cargar(realm)["clients"]!.AsArray().Single(c => (string?)c!["clientId"] == "core-bancario-web")!;

    private static IReadOnlyList<string> Textos(JsonNode? arreglo) =>
        arreglo is null ? [] : [.. arreglo.AsArray().Select(n => (string)n!)];

    /// <summary>El rol por defecto: en los export de Keycloak sus composites van en roles.realm; si no, en defaultRole.</summary>
    private static JsonNode? RolPorDefecto(JsonNode realm)
    {
        var nombre = (string?)realm["defaultRole"]?["name"];
        var enRoles = realm["roles"]?["realm"]?.AsArray().SingleOrDefault(r => (string?)r!["name"] == nombre);
        return enRoles ?? realm["defaultRole"];
    }

    private static IReadOnlyList<string> NombresDeRolesDeRealm(JsonNode realm) =>
        realm["roles"]?["realm"] is JsonArray roles ? [.. roles.Select(r => (string)r!["name"]!)] : [];

    // ---- RN-22: tiempos de sesion ----

    [Theory]
    [InlineData("clientes", 300, 1800)]
    [InlineData("empleados", 900, 32400)]
    public void F003_RN22_RealmsDeProduccion_TienenLosTiemposDeSesion(string realm, int inactividad, int vidaMaxima)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.Equal(inactividad, (int?)json["ssoSessionIdleTimeout"]);
        Assert.Equal(vidaMaxima, (int?)json["ssoSessionMaxLifespan"]);
    }

    // ---- RNF-01 / ADR-0015: vida del access token ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RNF01_RealmsDeProduccion_EmitenAccessTokensDe270Segundos(string realm)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.Equal(270, (int?)json["accessTokenLifespan"]);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_ADR0015_RealmsDeProduccion_RotanElRefreshTokenYSoloSirveUnaVez(string realm)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.True((bool?)json["revokeRefreshToken"]);
        Assert.Equal(0, (int?)json["refreshTokenMaxReuse"]);
    }

    // ---- RN-23: bloqueo temporal ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN23_RealmsDeProduccion_BloqueanCincoFallosQuinceMinutos(string realm)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.True((bool?)json["bruteForceProtected"]);
        Assert.False((bool?)json["permanentLockout"]);
        Assert.Equal(5, (int?)json["failureFactor"]);
        Assert.Equal(900, (int?)json["waitIncrementSeconds"]);
        Assert.Equal(900, (int?)json["maxFailureWaitSeconds"]);
    }

    // ---- RN-24: segundo factor obligatorio ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN24_RealmsDeProduccion_ExigenOtpEnElFlujoDeNavegador(string realm)
    {
        // Arrange
        var json = Cargar(realm);
        var alias = (string?)json["browserFlow"];

        // Act
        var formularios = EjecucionesDelFlujo(json, alias!).Where(e => (string?)e.Ejecucion["authenticator"] == "auth-otp-form").ToList();

        // Assert
        var otp = Assert.Single(formularios);
        Assert.Equal("REQUIRED", (string?)otp.Ejecucion["requirement"]);
        Assert.True(otp.SinAncestroCondicional, "El OTP esta dentro de un subflujo CONDITIONAL: quien no lo tiene inscrito podria saltarlo.");
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN24_RealmsDeProduccion_UsanTotpDeSeisDigitosCadaTreintaSegundos(string realm)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.Equal("totp", (string?)json["otpPolicyType"]);
        Assert.Equal("HmacSHA1", (string?)json["otpPolicyAlgorithm"]);
        Assert.Equal(6, (int?)json["otpPolicyDigits"]);
        Assert.Equal(30, (int?)json["otpPolicyPeriod"]);
        Assert.False((bool?)json["otpPolicyCodeReusable"]);
    }

    // ---- RN-21: sin atajos hacia una sesion que no caduca o hacia la consola de cuenta ----

    [Theory]
    [InlineData("clientes", "delete_credential")]
    [InlineData("clientes", "UPDATE_PROFILE")]
    [InlineData("clientes", "VERIFY_EMAIL")]
    [InlineData("empleados", "delete_credential")]
    [InlineData("empleados", "UPDATE_PROFILE")]
    [InlineData("empleados", "VERIFY_EMAIL")]
    public void F003_RN21_RealmsDeProduccion_AccionesQueBorranElOtpOEditanElPerfilEstanDesactivadas(string realm, string accion)
    {
        // Arrange: delete_credential permitiria borrar el OTP desde la SPA con kc_action (RN-20: lo restablece el operador).
        var acciones = Cargar(realm)["requiredActions"]!.AsArray();

        // Act
        var configurada = acciones.SingleOrDefault(a => (string?)a!["alias"] == accion);

        // Assert
        Assert.NotNull(configurada);
        Assert.False((bool?)configurada["enabled"]);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN21_RealmsDeProduccion_RolesPorDefectoSinOfflineAccessNiConsolaDeCuenta(string realm)
    {
        // Arrange
        var rolPorDefecto = RolPorDefecto(Cargar(realm));
        Assert.NotNull(rolPorDefecto);

        // Act
        var deRealm = Textos(rolPorDefecto["composites"]?["realm"]);
        var deLaCuenta = Textos(rolPorDefecto["composites"]?["client"]?["account"]);

        // Assert
        Assert.DoesNotContain("offline_access", deRealm);
        Assert.DoesNotContain("manage-account", deLaCuenta);
        Assert.DoesNotContain("view-profile", deLaCuenta);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN21_ClienteWeb_SinConcesionDirectaNiScopeOfflineAccess(string realm)
    {
        // Arrange / Act
        var cliente = ClienteWeb(realm);

        // Assert
        Assert.False((bool?)cliente["directAccessGrantsEnabled"]);
        Assert.DoesNotContain("offline_access", Textos(cliente["defaultClientScopes"]));
        Assert.DoesNotContain("offline_access", Textos(cliente["optionalClientScopes"]));
    }

    // ---- ADR-0014: el cliente de la SPA ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_ADR0014_ClienteWeb_EsPublicoYUsaCodigoConPkceS256(string realm)
    {
        // Arrange / Act
        var cliente = ClienteWeb(realm);

        // Assert
        Assert.True((bool?)cliente["publicClient"]);
        Assert.True((bool?)cliente["standardFlowEnabled"]);
        Assert.False((bool?)cliente["implicitFlowEnabled"]);
        Assert.False((bool?)cliente["serviceAccountsEnabled"]);
        Assert.Equal("S256", (string?)cliente["attributes"]?["pkce.code.challenge.method"]);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_ADR0014_ClienteWeb_TraeSubRolesYAudienciaDeLaApi(string realm)
    {
        // Arrange / Act
        var cliente = ClienteWeb(realm);
        var mapperDeAudiencia = cliente["protocolMappers"]?.AsArray()
            .SingleOrDefault(m => (string?)m!["protocolMapper"] == "oidc-audience-mapper");

        // Assert
        var scopes = Textos(cliente["defaultClientScopes"]);
        Assert.Contains("basic", scopes);
        Assert.Contains("roles", scopes);
        Assert.NotNull(mapperDeAudiencia);
        Assert.Equal("core-bancario-api", (string?)mapperDeAudiencia["config"]?["included.client.audience"]);
        Assert.Equal("true", (string?)mapperDeAudiencia["config"]?["access.token.claim"]);
    }

    // ---- RF-15: politica de clave y cambio de clave ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RF15_RealmsDeProduccion_ExigenLaPoliticaDeClave(string realm)
    {
        // Arrange / Act
        var politica = (string?)Cargar(realm)["passwordPolicy"];

        // Assert
        Assert.NotNull(politica);
        Assert.Contains("length(12)", politica, StringComparison.Ordinal);
        Assert.Contains("maxLength(64)", politica, StringComparison.Ordinal);
        Assert.Contains("notUsername()", politica, StringComparison.Ordinal);
        Assert.Contains("notContainsUsername()", politica, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RF15_RealmsDeProduccion_CambiarLaClaveSiemprePideLaActual(string realm)
    {
        // Arrange
        var acciones = Cargar(realm)["requiredActions"]!.AsArray();

        // Act
        var cambiarClave = acciones.Single(a => (string?)a!["alias"] == "UPDATE_PASSWORD");

        // Assert
        Assert.True((bool?)cambiarClave!["enabled"]);
        Assert.Equal("0", (string?)cambiarClave["config"]?["max_auth_age"]);
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RF15_RealmsDeProduccion_NoExigenCompletarElPerfil(string realm)
    {
        // Arrange
        var acciones = Cargar(realm)["requiredActions"]!.AsArray();

        // Act
        var verificarPerfil = acciones.SingleOrDefault(a => (string?)a!["alias"] == "VERIFY_PROFILE");

        // Assert: un usuario creado solo con el nombre de usuario no debe recibir VERIFY_PROFILE (hallazgo 6).
        Assert.NotNull(verificarPerfil);
        Assert.False((bool?)verificarPerfil["enabled"]);
    }

    // ---- RN-19: sin autorregistro, sin recuperacion sin operador, sin inicio con correo ----

    [Theory]
    [InlineData("clientes")]
    [InlineData("empleados")]
    public void F003_RN19_RealmsDeProduccion_SinAutorregistroNiRecuperacionNiInicioConCorreo(string realm)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.False((bool?)json["loginWithEmailAllowed"]);
        Assert.False((bool?)json["registrationAllowed"]);
        Assert.False((bool?)json["resetPasswordAllowed"]);
        Assert.False((bool?)json["rememberMe"]);
        Assert.False((bool?)json["editUsernameAllowed"]);
    }

    [Fact]
    public void F003_RN19_RealmClientes_DefineElRolClienteYLoDaPorDefecto()
    {
        // Arrange
        var json = Cargar("clientes");

        // Act
        var roles = NombresDeRolesDeRealm(json);
        var porDefecto = Textos(RolPorDefecto(json)?["composites"]?["realm"]);

        // Assert
        Assert.Contains("cliente", roles);
        Assert.Contains("cliente", porDefecto);
    }

    [Fact]
    public void F003_RN19_RealmEmpleados_DefineElRolOperadorYNoLoDaPorDefecto()
    {
        // Arrange
        var json = Cargar("empleados");

        // Act
        var roles = NombresDeRolesDeRealm(json);
        var porDefecto = Textos(RolPorDefecto(json)?["composites"]?["realm"]);

        // Assert
        Assert.Contains("operador", roles);
        Assert.DoesNotContain("operador", porDefecto);
    }

    // ---- CA-08 / CA-12: temas de login ----

    [Theory]
    [InlineData("clientes", "core-bancario-clientes")]
    [InlineData("empleados", "core-bancario")]
    public void F003_CA12_RealmsDeProduccion_UsanSuTemaDeLogin(string realm, string tema)
    {
        // Arrange / Act
        var json = Cargar(realm);

        // Assert
        Assert.Equal(tema, (string?)json["loginTheme"]);
        Assert.Equal("es", (string?)json["defaultLocale"]);
    }

    private sealed record EjecucionConContexto(JsonNode Ejecucion, bool SinAncestroCondicional);

    /// <summary>Todas las ejecuciones del flujo y de sus subflujos, y si alguna esta dentro de un subflujo CONDITIONAL.</summary>
    private static IEnumerable<EjecucionConContexto> EjecucionesDelFlujo(JsonNode realm, string alias, bool sinAncestroCondicional = true)
    {
        var flujo = realm["authenticationFlows"]!.AsArray().Single(f => (string?)f!["alias"] == alias)!;
        foreach (var ejecucion in flujo["authenticationExecutions"]!.AsArray().Select(e => e!))
        {
            yield return new EjecucionConContexto(ejecucion, sinAncestroCondicional);

            var subflujo = (string?)ejecucion["flowAlias"];
            if (subflujo is null)
                continue;

            var dentroDeCondicional = sinAncestroCondicional && (string?)ejecucion["requirement"] != "CONDITIONAL";
            foreach (var hija in EjecucionesDelFlujo(realm, subflujo, dentroDeCondicional))
                yield return hija;
        }
    }
}
