using System.Net;
using CoreBancario.Api.Autenticacion;
using CoreBancario.Api.Errores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static CoreBancario.Api.Tests.Ayudas;
using static CoreBancario.Api.Tests.AyudasDeAutenticacion;

namespace CoreBancario.Api.Tests;

/// <summary>
/// 401 (no autenticado): CL-07, CA-18, CA-21, RNF-01, RNF-02, RNF-04. Los tokens los firma EmisorDePruebas con claves
/// RSA propias: no se arranca Keycloak (ADR-0018).
/// </summary>
public class AutenticacionApiTests(ApiFixture api)
{
    public static TheoryData<string, string, string, string> CredencialesInvalidas => OperacionesConCredencialInvalida();

    // ---- CA-18 / CL-07: cada operacion protegida rechaza una credencial invalida ----

    [Theory]
    [MemberData(nameof(CredencialesInvalidas))]
    public async Task F003_CA18_OperacionProtegidaConCredencialInvalida_Responde401(
        string metodo, string ruta, string emisor, string modo)
    {
        // Arrange
        using var http = api.CrearClienteAnonimo();
        var token = TokenConDefecto(modo, Enum.Parse<EmisorDeToken>(emisor));

        // Act
        var respuesta = await EnviarAsync(http, metodo, ruta, token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Bearer", respuesta.Headers.WwwAuthenticate.ToString());
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(401, problema.Status);
        Assert.Equal(CodigosDeError.NoAutenticado, problema.Codigo);
        Assert.Null(problema.Detail);
    }

    // ---- CA-21 / RNF-01: la ventana de 5 minutos incluye la tolerancia de reloj ----

    [Fact]
    public async Task F003_CA21_TokenEmitidoHaceMasDeCincoMinutos_SeRechaza()
    {
        // Arrange: iat = ahora - 301 s y exp = iat + 270 s, es decir, vencio hace 31 s (mas que la tolerancia de 30 s).
        var token = EmisorDePruebas.CrearToken(EmisorDeToken.Clientes, Guid.NewGuid().ToString(),
            [EmisorDePruebas.RolDeCliente], emitido: DateTimeOffset.UtcNow - TimeSpan.FromSeconds(301));
        using var http = api.CrearClienteConToken(token);

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task F003_CA21_TokenVencidoDentroDeLaTolerancia_SeAcepta()
    {
        // Arrange: exp = ahora - 20 s, dentro de la tolerancia de 30 s.
        var token = EmisorDePruebas.CrearToken(EmisorDeToken.Clientes, Guid.NewGuid().ToString(),
            [EmisorDePruebas.RolDeCliente], emitido: DateTimeOffset.UtcNow - TimeSpan.FromSeconds(290));
        using var http = api.CrearClienteConToken(token);

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("clientes", EsquemasDeAutenticacion.Clientes)]
    [InlineData("empleados", EsquemasDeAutenticacion.Empleados)]
    public void F003_RNF01_VidaDelTokenMasToleranciaDeReloj_NoSuperaCincoMinutos(string realm, string esquema)
    {
        // Arrange
        var vidaEnElRealm = TimeSpan.FromSeconds((int?)ConfiguracionDeRealmsTests.Cargar(realm)["accessTokenLifespan"] ?? int.MaxValue);
        var opciones = api.Factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(esquema);

        // Act
        var ventana = vidaEnElRealm + opciones.TokenValidationParameters.ClockSkew;

        // Assert
        Assert.True(ventana <= TimeSpan.FromMinutes(5), $"La ventana de revocacion es {ventana}, mas de 5 minutos.");
    }

    // ---- ADR-0015: validacion estricta en los dos esquemas ----

    [Theory]
    [InlineData(EsquemasDeAutenticacion.Clientes, EmisorDeToken.Clientes)]
    [InlineData(EsquemasDeAutenticacion.Empleados, EmisorDeToken.Empleados)]
    public void F003_ADR0015_EsquemaJwt_ValidaEmisorAudienciaFirmaAlgoritmoYVigencia(string esquema, EmisorDeToken emisor)
    {
        // Arrange / Act
        var opciones = api.Factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(esquema);
        var parametros = opciones.TokenValidationParameters;

        // Assert
        Assert.Equal(EmisorDePruebas.UrlDe(emisor), parametros.ValidIssuer);
        Assert.Equal(EmisorDePruebas.Audiencia, parametros.ValidAudience);
        Assert.Equal([OpcionesDeAutenticacion.AlgoritmoPermitido], parametros.ValidAlgorithms);
        Assert.Equal(OpcionesDeAutenticacion.ToleranciaDeReloj, parametros.ClockSkew);
        Assert.True(parametros.ValidateIssuer);
        Assert.True(parametros.ValidateAudience);
        Assert.True(parametros.ValidateLifetime);
        Assert.True(parametros.RequireSignedTokens);
        Assert.True(parametros.RequireExpirationTime);
    }

    [Theory]
    [InlineData(EsquemasDeAutenticacion.Clientes)]
    [InlineData(EsquemasDeAutenticacion.Empleados)]
    public void F003_ADR0015_EsquemaJwt_NoMapeaClaimsNiExponeDetallesDeError(string esquema)
    {
        // Arrange / Act
        var opciones = api.Factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(esquema);

        // Assert
        Assert.False(opciones.MapInboundClaims);
        Assert.False(opciones.IncludeErrorDetails);
    }

    // ---- RNF-04: el motivo queda en el log, nunca el token, y la respuesta no lo revela ----

    [Theory]
    [InlineData(Vencido, MotivoDeRechazo.Vencida)]
    [InlineData(AunNoValido, MotivoDeRechazo.AunNoValida)]
    [InlineData(FirmaDeOtraClave, MotivoDeRechazo.FirmaInvalida)]
    [InlineData(OtraAudiencia, MotivoDeRechazo.AudienciaIncorrecta)]
    [InlineData(OtroEmisor, MotivoDeRechazo.EmisorIncorrecto)]
    [InlineData(SinSub, MotivoDeRechazo.SinIdentificador)]
    [InlineData(MalFormado, MotivoDeRechazo.MalFormada)]
    [InlineData(SinToken, MotivoDeRechazo.SinCredencial)]
    public async Task F003_RNF04_TokenRechazado_ElLogTieneElMotivoSinElToken(string modo, string motivo)
    {
        // Arrange: la ruta lleva un Guid unico para encontrar el registro de ESTA peticion entre los de las demas pruebas.
        var ruta = $"/cuentas/{Guid.NewGuid()}";
        var token = TokenConDefecto(modo, EmisorDeToken.Empleados);
        using var http = api.CrearClienteAnonimo();

        // Act
        var respuesta = await EnviarAsync(http, "GET", ruta, token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var registros = api.Logs.QueContienen(ruta).ToList();
        Assert.Contains(registros, r => r.Nivel == LogLevel.Information && r.Mensaje.Contains(motivo, StringComparison.Ordinal));
        Assert.DoesNotContain(api.Logs.Registros, r => ContieneAlgunaParteDelToken(r, token));
    }

    [Fact]
    public async Task F003_RNF04_MotivosDistintos_MismaRespuesta401()
    {
        // Arrange: la misma ruta (el instance del problema es la ruta) con siete defectos distintos.
        var ruta = $"/cuentas/{Guid.NewGuid()}";
        string[] modos = [SinToken, Vencido, FirmaDeOtraClave, OtraAudiencia, OtroEmisor, SinSub, MalFormado];

        // Act
        var huellas = await Task.WhenAll(modos.Select(modo => HuellaDeUnDefectoAsync(modo, ruta)));

        // Assert
        Assert.All(huellas, huella => Assert.StartsWith("401|Bearer|application/problem+json|", huella, StringComparison.Ordinal));
        Assert.Single(huellas.Distinct());
    }

    // ---- RN-19: un rol por credencial y cada ruta sirve a un solo rol ----

    [Fact]
    public async Task F003_RN19_RutaSinPolitica_QuedaProtegidaPorDefecto()
    {
        // Arrange
        using var alcance = api.Factory.Services.CreateScope();
        var proveedor = alcance.ServiceProvider.GetService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        Assert.NotNull(proveedor);

        // Act
        var politica = await proveedor.GetFallbackPolicyAsync();

        // Assert
        Assert.NotNull(politica);
        Assert.Contains(politica.Requirements,
            r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    private async Task<string> HuellaDeUnDefectoAsync(string modo, string ruta)
    {
        using var http = api.CrearClienteAnonimo();
        var respuesta = await EnviarAsync(http, "GET", ruta, TokenConDefecto(modo, EmisorDeToken.Empleados));
        return await HuellaDeLaRespuestaAsync(respuesta, ruta[(ruta.LastIndexOf('/') + 1)..]);
    }

    private static bool ContieneAlgunaParteDelToken(RegistroDeLog registro, string? token)
    {
        if (token is null)
            return false;

        var texto = registro.Mensaje + registro.Excepcion;
        var firma = token.Split('.')[^1];
        return texto.Contains(token, StringComparison.Ordinal) || texto.Contains(firma, StringComparison.Ordinal);
    }
}
