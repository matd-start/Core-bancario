using System.Security.Claims;
using CoreBancario.Api.Autenticacion;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace CoreBancario.Api.Tests;

/// <summary>Funciones puras de la autenticacion: sin base de datos, sin servidor.</summary>
public class AutenticacionUnitariaTests
{
    private static OpcionesDeAutenticacion Opciones => new()
    {
        Clientes = new OpcionesDeEmisor { Emisor = EmisorDePruebas.UrlDe(EmisorDeToken.Clientes) },
        Empleados = new OpcionesDeEmisor { Emisor = EmisorDePruebas.UrlDe(EmisorDeToken.Empleados) },
    };

    private static DefaultHttpContext ContextoConAuthorization(string? valor)
    {
        var contexto = new DefaultHttpContext();
        if (valor is not null)
            contexto.Request.Headers.Authorization = valor;

        return contexto;
    }

    // ---- RNF-04: MotivoDeRechazo ----

    public static TheoryData<Exception?, string> FallasYMotivos => new()
    {
        { null, MotivoDeRechazo.SinCredencial },
        { new SecurityTokenExpiredException(), MotivoDeRechazo.Vencida },
        { new SecurityTokenNotYetValidException(), MotivoDeRechazo.AunNoValida },
        { new SecurityTokenInvalidSignatureException(), MotivoDeRechazo.FirmaInvalida },
        { new SecurityTokenSignatureKeyNotFoundException(), MotivoDeRechazo.FirmaInvalida },
        { new SecurityTokenInvalidIssuerException(), MotivoDeRechazo.EmisorIncorrecto },
        { new SecurityTokenInvalidAudienceException(), MotivoDeRechazo.AudienciaIncorrecta },
        { new SecurityTokenInvalidAlgorithmException(), MotivoDeRechazo.AlgoritmoNoPermitido },
        { new TokenSinSujetoException(), MotivoDeRechazo.SinIdentificador },
        { new SecurityTokenMalformedException(), MotivoDeRechazo.MalFormada },
        { new InvalidOperationException("cualquier otra"), MotivoDeRechazo.MalFormada },
    };

    [Theory]
    [MemberData(nameof(FallasYMotivos))]
    public void F003_RNF04_MotivoDeRechazo_ClasificaCadaFalla(Exception? falla, string motivo)
    {
        // Arrange / Act
        var resultado = MotivoDeRechazo.Desde(falla);

        // Assert
        Assert.Equal(motivo, resultado);
    }

    [Fact]
    public void F003_RNF04_MotivoDeRechazo_NoIncluyeElMensajeDeLaExcepcion()
    {
        // Arrange: el mensaje de IdentityModel puede traer partes del token.
        var falla = new SecurityTokenInvalidSignatureException("IDX10511: eyJhbGciOiJSUzI1NiJ9.cuerpo.firma");

        // Act
        var motivo = MotivoDeRechazo.Desde(falla);

        // Assert
        Assert.DoesNotContain("eyJ", motivo, StringComparison.Ordinal);
        Assert.DoesNotContain("IDX10511", motivo, StringComparison.Ordinal);
    }

    // ---- ADR-0015: SelectorDeEsquema ----

    [Fact]
    public void F003_ADR0015_SelectorDeEsquema_TokenDelEmisorDeEmpleados_ElijeEmpleados()
    {
        // Arrange
        var token = EmisorDePruebas.CrearTokenDeOperador();
        var contexto = ContextoConAuthorization($"Bearer {token}");

        // Act
        var esquema = SelectorDeEsquema.Elegir(contexto, Opciones);

        // Assert
        Assert.Equal(EsquemasDeAutenticacion.Empleados, esquema);
    }

    [Fact]
    public void F003_ADR0015_SelectorDeEsquema_TokenDelEmisorDeClientes_ElijeClientes()
    {
        // Arrange
        var token = EmisorDePruebas.CrearTokenDeCliente(Guid.NewGuid().ToString());
        var contexto = ContextoConAuthorization($"Bearer {token}");

        // Act
        var esquema = SelectorDeEsquema.Elegir(contexto, Opciones);

        // Assert
        Assert.Equal(EsquemasDeAutenticacion.Clientes, esquema);
    }

    [Fact]
    public void F003_ADR0015_SelectorDeEsquema_TokenDeUnEmisorDesconocido_ElijeClientesParaQueLoRechace()
    {
        // Arrange
        var token = EmisorDePruebas.CrearToken(EmisorDeToken.Desconocido, Guid.NewGuid().ToString(), [EmisorDePruebas.RolDeOperador]);
        var contexto = ContextoConAuthorization($"Bearer {token}");

        // Act
        var esquema = SelectorDeEsquema.Elegir(contexto, Opciones);

        // Assert
        Assert.Equal(EsquemasDeAutenticacion.Clientes, esquema);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer esto.no-es-un-jwt")]
    [InlineData("Bearer a.b.c")]
    [InlineData("Basic dXN1YXJpbzpjbGF2ZQ==")]
    public void F003_ADR0015_SelectorDeEsquema_CabeceraAusenteOIlegible_ElijeClientesYNoLanza(string? cabecera)
    {
        // Arrange
        var contexto = ContextoConAuthorization(cabecera);

        // Act
        var esquema = SelectorDeEsquema.Elegir(contexto, Opciones);

        // Assert
        Assert.Equal(EsquemasDeAutenticacion.Clientes, esquema);
    }

    // ---- ClaimsPrincipalExtensiones.Sub ----

    [Fact]
    public void F003_RN19_Sub_DevuelveElClaimSubDelToken()
    {
        // Arrange
        var identidad = new ClaimsIdentity([new Claim("sub", "abc-123")], "Bearer");
        var usuario = new ClaimsPrincipal(identidad);

        // Act
        var sub = usuario.Sub();

        // Assert
        Assert.Equal("abc-123", sub);
    }

    [Fact]
    public void F003_RN19_Sub_SinClaimSub_LanzaInvalidOperationException()
    {
        // Arrange
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alguien")], "Bearer"));

        // Act
        var accion = () => usuario.Sub();

        // Assert
        Assert.Throws<InvalidOperationException>(accion);
    }
}
