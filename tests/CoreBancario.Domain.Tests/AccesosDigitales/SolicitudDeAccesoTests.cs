using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Domain.Tests.AccesosDigitales;

/// <summary>
/// RN-20 y ADR-0017: la solicitud es la intención del operador, escrita antes de llamar al sistema de identidad. La
/// acción que queda en el registro depende del estado actual del acceso (CL-13, CL-16) y deshabilitar valida ANTES de
/// crear nada (CL-14, CA-04).
/// </summary>
public class SolicitudDeAccesoTests
{
    private static readonly DateTimeOffset Fecha = new(2026, 10, 13, 15, 2, 11, 123, TimeSpan.Zero);
    private static readonly Guid ClienteId = Guid.NewGuid();
    private const string Operador = "operador-de-pruebas";

    // ---- CA-01 / CL-13 / CL-16: la acción depende del estado ----

    [Fact]
    public void F003_CA01_ParaHabilitarSinAcceso_EsHabilitar()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.Equal(AccionDeAcceso.Habilitar, solicitud.Accion);
    }

    [Fact]
    public void F003_CL13_ParaHabilitarConAccesoHabilitado_EsRestablecer()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.Habilitado, Fecha);

        // Assert
        Assert.Equal(AccionDeAcceso.Restablecer, solicitud.Accion);
    }

    [Fact]
    public void F003_CL16_ParaHabilitarConAccesoDeshabilitado_EsHabilitar()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.Deshabilitado, Fecha);

        // Assert
        Assert.Equal(AccionDeAcceso.Habilitar, solicitud.Accion);
    }

    [Fact]
    public void F003_CA03_ParaDeshabilitarConAccesoHabilitado_EsDeshabilitar()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaDeshabilitar(ClienteId, Operador, EstadoDeAcceso.Habilitado, Fecha);

        // Assert
        Assert.Equal(AccionDeAcceso.Deshabilitar, solicitud.Accion);
    }

    // ---- CL-14 / CA-04: deshabilitar sin nada que deshabilitar ----

    [Theory]
    [InlineData(EstadoDeAcceso.SinAcceso)]
    [InlineData(EstadoDeAcceso.Deshabilitado)]
    public void F003_CL14_ParaDeshabilitarSinAccesoODeshabilitado_LanzaAccesoNoHabilitado(EstadoDeAcceso estado)
    {
        // Arrange / Act
        var accion = () => SolicitudDeAcceso.ParaDeshabilitar(ClienteId, Operador, estado, Fecha);

        // Assert
        var error = Assert.Throws<AccesoNoHabilitadoException>(accion);
        Assert.Equal(estado, error.EstadoActual);
    }

    [Fact]
    public void F003_CL14_AccesoNoHabilitado_EsUnaReglaDeNegocio()
    {
        // Arrange / Act
        var accion = () => SolicitudDeAcceso.ParaDeshabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert: ReglaDeNegocioException es lo que la API traduce a 422.
        Assert.IsAssignableFrom<ReglaDeNegocioException>(Assert.Throws<AccesoNoHabilitadoException>(accion));
    }

    // ---- RN-20: qué queda registrado ----

    [Fact]
    public void F003_RN20_Solicitud_GuardaClienteOperadorYFecha()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.Equal(ClienteId, solicitud.ClienteId);
        Assert.Equal(Operador, solicitud.Operador);
        Assert.Equal(Fecha, solicitud.Fecha);
    }

    [Fact]
    public void F003_RN20_Solicitud_IdEsVersion7DeLaFecha()
    {
        // Arrange / Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert: un UUID v7 lleva en sus primeros 48 bits los milisegundos Unix (RFC 9562); el texto empieza por 12 hex.
        Assert.Equal(7, solicitud.Id.Version);
        Assert.Equal(Fecha.ToUnixTimeMilliseconds().ToString("x12"), solicitud.Id.ToString("N")[..12]);
    }

    [Fact]
    public void F003_RN20_DosSolicitudesDeLaMismaFecha_TienenIdDistinto()
    {
        // Arrange / Act
        var primera = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);
        var segunda = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.NotEqual(primera.Id, segunda.Id);
    }

    // ---- RN-20 / RN-19: guardas de operador y cliente ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void F003_RN20_SolicitudSinOperador_LanzaArgumentException(string? operador)
    {
        // Arrange / Act
        var habilitar = () => SolicitudDeAcceso.ParaHabilitar(ClienteId, operador!, EstadoDeAcceso.SinAcceso, Fecha);
        var deshabilitar = () => SolicitudDeAcceso.ParaDeshabilitar(ClienteId, operador!, EstadoDeAcceso.Habilitado, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(habilitar);
        Assert.ThrowsAny<ArgumentException>(deshabilitar);
    }

    [Fact]
    public void F003_RN20_OperadorDeMasDe255Caracteres_LanzaArgumentException()
    {
        // Arrange
        var operador = new string('o', SolicitudDeAcceso.LongitudMaximaDelOperador + 1);

        // Act
        var accion = () => SolicitudDeAcceso.ParaHabilitar(ClienteId, operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_RN20_OperadorDe255Caracteres_SeAcepta()
    {
        // Arrange
        var operador = new string('o', 255);

        // Act
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.Equal(operador, solicitud.Operador);
    }

    [Fact]
    public void F003_RN20_SolicitudConClienteVacio_LanzaArgumentException()
    {
        // Arrange / Act
        var accion = () => SolicitudDeAcceso.ParaHabilitar(Guid.Empty, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    // ---- ADR-0017: el resultado es otra fila ----

    [Fact]
    public void F003_RN20_Completar_DevuelveResultadoDeEsaSolicitud()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);
        var fechaDelResultado = Fecha.AddSeconds(2);

        // Act
        var resultado = solicitud.Completar(fechaDelResultado);

        // Assert
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Completado, resultado.Resultado);
        Assert.Equal(fechaDelResultado, resultado.Fecha);
        Assert.Null(resultado.Motivo);
    }

    [Fact]
    public void F003_RN20_CompletarConMotivo_ConservaElMotivo()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Act
        var resultado = solicitud.Completar(Fecha.AddSeconds(2), "Confirmada al reintentar");

        // Assert
        Assert.Equal("Confirmada al reintentar", resultado.Motivo);
    }

    [Fact]
    public void F003_RN20_Fallar_DevuelveResultadoFallidoConSuMotivo()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaDeshabilitar(ClienteId, Operador, EstadoDeAcceso.Habilitado, Fecha);

        // Act
        var resultado = solicitud.Fallar(Fecha.AddSeconds(2), "HTTP 500 al cerrar las sesiones");

        // Assert
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Fallido, resultado.Resultado);
        Assert.Equal("HTTP 500 al cerrar las sesiones", resultado.Motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void F003_RN20_FallarSinMotivo_LanzaArgumentException(string? motivo)
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);

        // Act
        var accion = () => solicitud.Fallar(Fecha.AddSeconds(2), motivo!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_RN20_FallarConMotivoDeMasDe200Caracteres_LanzaArgumentException()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);
        var motivo = new string('m', ResultadoDeSolicitud.LongitudMaximaDelMotivo + 1);

        // Act
        var accion = () => solicitud.Fallar(Fecha.AddSeconds(2), motivo);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_RN20_CompletarConMotivoDeMasDe200Caracteres_LanzaArgumentException()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);
        var motivo = new string('m', ResultadoDeSolicitud.LongitudMaximaDelMotivo + 1);

        // Act: igual que Fallar, para que la base no sea la primera en rechazarlo.
        var accion = () => solicitud.Completar(Fecha.AddSeconds(2), motivo);

        // Assert
        Assert.ThrowsAny<ArgumentException>(accion);
    }

    [Fact]
    public void F003_RN20_Completar_NoModificaLaSolicitud()
    {
        // Arrange
        var solicitud = SolicitudDeAcceso.ParaHabilitar(ClienteId, Operador, EstadoDeAcceso.SinAcceso, Fecha);
        var idAntes = solicitud.Id;

        // Act
        _ = solicitud.Completar(Fecha.AddSeconds(2));

        // Assert: la solicitud es inmutable; el resultado es una fila nueva.
        Assert.Equal(idAntes, solicitud.Id);
        Assert.Equal(Fecha, solicitud.Fecha);
        Assert.Equal(AccionDeAcceso.Habilitar, solicitud.Accion);
    }
}
