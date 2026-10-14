using CoreBancario.Application.AccesosDigitales;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>RNF-03: la clave temporal solo aparece en la respuesta de la operación que la genera. Y el registro en la inyección de dependencias.</summary>
public class ClaveTemporalYDtosTests
{
    private const string ClaveDe16 = "Xq7mK2vB9nLw4TpZ";

    [Fact]
    public void F003_RNF03_ClaveTemporal_ToStringNoRevelaElValor()
    {
        // Arrange
        var clave = new ClaveTemporal(ClaveDe16);

        // Act
        var texto = clave.ToString();

        // Assert
        Assert.DoesNotContain(ClaveDe16, texto, StringComparison.Ordinal);
        Assert.Equal("[clave temporal oculta]", texto);
    }

    [Fact]
    public void F003_RNF03_ClaveTemporal_InterpoladaEnUnTextoNoRevelaElValor()
    {
        // Arrange
        var clave = new ClaveTemporal(ClaveDe16);

        // Act
        var linea = $"Se entregó la clave {clave} al operador";

        // Assert
        Assert.DoesNotContain(ClaveDe16, linea, StringComparison.Ordinal);
    }

    [Fact]
    public void F003_RNF03_ClaveTemporalDeDieciseisCaracteres_ExponeSuValor()
    {
        // Arrange / Act
        var clave = new ClaveTemporal(ClaveDe16);

        // Assert
        Assert.Equal(ClaveDe16, clave.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    [InlineData("Xq7mK2vB9nLw4Tp")]
    public void F003_RNF03_ClaveTemporalMenorADieciseisCaracteres_LanzaArgumentException(string? valor)
    {
        // Arrange / Act
        var accion = () => new ClaveTemporal(valor!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_RNF03_AccesoHabilitadoDto_ToStringNoRevelaLaClave()
    {
        // Arrange
        var dto = new AccesoHabilitadoDto(Guid.NewGuid(), Guid.NewGuid(), "Habilitar", ClaveDe16, DateTimeOffset.UtcNow);

        // Act
        var texto = dto.ToString();

        // Assert
        Assert.DoesNotContain(ClaveDe16, texto, StringComparison.Ordinal);
    }

    [Fact]
    public void F003_RNF03_AccesoHabilitadoDto_ToStringConservaLosDemasDatos()
    {
        // Arrange
        var dto = new AccesoHabilitadoDto(Guid.NewGuid(), Guid.NewGuid(), "Restablecer", ClaveDe16, DateTimeOffset.UtcNow);

        // Act
        var texto = dto.ToString();

        // Assert
        Assert.Contains(dto.SolicitudId.ToString(), texto, StringComparison.Ordinal);
        Assert.Contains(dto.ClienteId.ToString(), texto, StringComparison.Ordinal);
        Assert.Contains("Restablecer", texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(LocalizadorDeAcceso))]
    [InlineData(typeof(ReconciliadorDeSolicitudes))]
    [InlineData(typeof(HabilitarAccesoHandler))]
    [InlineData(typeof(DeshabilitarAccesoHandler))]
    public void F003_RF13_AddAplicacion_RegistraElAccesoDigitalComoScoped(Type tipo)
    {
        // Arrange
        var servicios = new ServiceCollection();

        // Act
        servicios.AddAplicacion();

        // Assert
        var registro = Assert.Single(servicios, d => d.ServiceType == tipo);
        Assert.Equal(ServiceLifetime.Scoped, registro.Lifetime);
    }
}
