using System.Text.Json.Nodes;

namespace CoreBancario.Api.Tests;

/// <summary>
/// 003b, T24: la cuenta de servicio con la que la API gestiona accesos (ADR-0014, menor privilegio) y la marca de ADR-0017.
/// Como en ConfiguracionDeRealmsTests, no arranca nada: lee los JSON de infra/keycloak/realms, docker-compose.yml y
/// .env.example. Que Keycloak aplique esa configuración lo comprueban las pruebas de Keycloak.Tests.
/// </summary>
public class ConfiguracionDeCuentaDeServicioTests
{
    private const string ClientIdDeServicio = "core-bancario-api-admin";
    private const string MarcadorDelSecreto = "${KC_CLIENTES_API_SECRET}";

    private static JsonNode? ClienteDeServicio(string realm) =>
        ConfiguracionDeRealmsTests.Cargar(realm)["clients"]!.AsArray()
            .SingleOrDefault(c => (string?)c!["clientId"] == ClientIdDeServicio);

    /// <summary>El usuario de la cuenta de servicio, tal como lo exporta Keycloak (users[].serviceAccountClientId).</summary>
    private static JsonNode? UsuarioDeLaCuentaDeServicio() =>
        ConfiguracionDeRealmsTests.Cargar("clientes")["users"]?.AsArray()
            .SingleOrDefault(u => (string?)u!["serviceAccountClientId"] == ClientIdDeServicio);

    private static string Texto(string rutaRelativa) =>
        File.ReadAllText(Path.Combine(AyudasDeAutenticacion.RaizDelRepositorio(), rutaRelativa));

    [Fact]
    public void F003_ADR0014_CuentaDeServicio_SoloTieneManageUsersYViewUsers()
    {
        // Arrange
        var usuario = UsuarioDeLaCuentaDeServicio();
        Assert.NotNull(usuario);

        // Act
        var rolesDeClientes = usuario["clientRoles"]!.AsObject();
        var deRealmManagement = rolesDeClientes["realm-management"]!.AsArray().Select(r => (string)r!).ToList();

        // Assert: ningún otro cliente y ningún rol de administración más allá de gestionar y ver usuarios.
        Assert.Equal(["realm-management"], rolesDeClientes.Select(c => c.Key));
        Assert.Equivalent(new[] { "manage-users", "view-users" }, deRealmManagement);
    }

    [Fact]
    public void F003_ADR0014_CuentaDeServicio_NoTieneRolesDeRealmDeAdministracion()
    {
        // Arrange
        var usuario = UsuarioDeLaCuentaDeServicio();
        Assert.NotNull(usuario);

        // Act
        var rolesDeRealm = usuario["realmRoles"]?.AsArray().Select(r => (string)r!).ToList() ?? [];

        // Assert
        Assert.DoesNotContain(rolesDeRealm, rol => rol.Contains("admin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void F003_ADR0014_CuentaDeServicio_NoTieneNingunRolDeRealm()
    {
        // Arrange
        var usuario = UsuarioDeLaCuentaDeServicio();
        Assert.NotNull(usuario);

        // Act: sin default-roles-clientes (que incluye "cliente") la cuenta de servicio no es un Cliente de la API.
        var rolesDeRealm = usuario["realmRoles"]?.AsArray().Select(r => (string)r!).ToList() ?? [];

        // Assert
        Assert.Empty(rolesDeRealm);
    }

    [Fact]
    public void F003_ADR0014_CuentaDeServicio_EsUnClienteConfidencialSoloDeServicio()
    {
        // Arrange / Act
        var cliente = ClienteDeServicio("clientes");

        // Assert: sin flujo de navegador, sin concesión directa de usuario y clave, con secreto.
        Assert.NotNull(cliente);
        Assert.False((bool?)cliente["publicClient"]);
        Assert.True((bool?)cliente["serviceAccountsEnabled"]);
        Assert.False((bool?)cliente["standardFlowEnabled"]);
        Assert.False((bool?)cliente["implicitFlowEnabled"]);
        Assert.False((bool?)cliente["directAccessGrantsEnabled"]);
    }

    [Fact]
    public void F003_RNF03_RealmClientes_NoContieneElSecretoDeServicio()
    {
        // Arrange / Act
        var cliente = ClienteDeServicio("clientes");

        // Assert: el valor es EXACTAMENTE el marcador; Keycloak lo resuelve con la variable de entorno al importar.
        Assert.NotNull(cliente);
        Assert.Equal(MarcadorDelSecreto, (string?)cliente["secret"]);
    }

    [Fact]
    public void F003_ADR0017_RealmClientes_DefineLaMarcaSoloVisibleYEditablePorAdmin()
    {
        // Arrange
        var perfil = ConfiguracionDeRealmsTests.Cargar("clientes")["components"]!["org.keycloak.userprofile.UserProfileProvider"]![0]!
            ["config"]!["kc.user.profile.config"]![0]!.GetValue<string>();

        // Act
        var configuracion = JsonNode.Parse(perfil)!;
        var marca = configuracion["attributes"]!.AsArray().SingleOrDefault(a => (string?)a!["name"] == "ultima_solicitud_de_acceso");

        // Assert: el usuario no puede ver ni cambiar su propia marca desde la consola de cuenta.
        Assert.NotNull(marca);
        Assert.Equal(["admin"], marca["permissions"]!["view"]!.AsArray().Select(p => (string)p!));
        Assert.Equal(["admin"], marca["permissions"]!["edit"]!.AsArray().Select(p => (string)p!));
    }

    [Fact]
    public void F003_RNF03_DockerCompose_PasaElSecretoDeServicioAlServicioKeycloak()
    {
        // Arrange / Act
        var compose = Texto("docker-compose.yml");

        // Assert: la variable se lee de .env, nunca con un valor escrito en el repositorio.
        Assert.Contains("KC_CLIENTES_API_SECRET: ${KC_CLIENTES_API_SECRET", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void F003_RNF03_EnvExample_DeclaraElSecretoDeServicioSinValor()
    {
        // Arrange / Act
        var lineas = Texto(".env.example").Split('\n').Select(l => l.Trim()).ToList();

        // Assert
        Assert.Contains("KC_CLIENTES_API_SECRET=", lineas);
    }
}
