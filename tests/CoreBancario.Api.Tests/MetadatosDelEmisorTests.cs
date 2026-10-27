using CoreBancario.Api.Autenticacion;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace CoreBancario.Api.Tests;

/// <summary>
/// ADR-0020 (compose): la API valida el "iss" que ve el navegador (http://localhost:8080/...) pero baja el documento de
/// descubrimiento por la red interna (http://keycloak:8080/...). Metadatos lo permite sin cambiar el emisor valido.
/// </summary>
public class MetadatosDelEmisorTests
{
    private const string EmisorDeClientes = "http://localhost:8080/realms/clientes";
    private const string MetadatosDeClientes = "http://keycloak:8080/realms/clientes/.well-known/openid-configuration";

    private sealed class EntornoDePruebas : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CoreBancario.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static JwtBearerOptions OpcionesDeJwtDeClientes()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Autenticacion:Audiencia"] = "core-bancario-api",
                ["Autenticacion:Clientes:Emisor"] = EmisorDeClientes,
                ["Autenticacion:Clientes:Metadatos"] = MetadatosDeClientes,
                ["Autenticacion:Empleados:Emisor"] = "http://localhost:8080/realms/empleados",
            })
            .Build();
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddAutenticacionConKeycloak(configuracion, new EntornoDePruebas());

        return servicios.BuildServiceProvider().GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(EsquemasDeAutenticacion.Clientes);
    }

    [Fact]
    public void F004_ADR0020_Metadatos_SeUsaComoDocumentoDeDescubrimiento()
    {
        // Arrange / Act
        var jwt = OpcionesDeJwtDeClientes();

        // Assert
        Assert.Equal(MetadatosDeClientes, jwt.MetadataAddress);
        Assert.Equal(EmisorDeClientes, jwt.TokenValidationParameters.ValidIssuer);
        Assert.Equal(EmisorDeClientes, jwt.Authority);
    }
}
