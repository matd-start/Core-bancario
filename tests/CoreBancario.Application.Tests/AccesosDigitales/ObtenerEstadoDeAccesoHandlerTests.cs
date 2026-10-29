using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>
/// RF-16, CA-29, CL-20: consultar el estado del acceso digital de un cliente. Es una consulta: el estado sale de
/// Keycloak por LocalizadorDeAcceso (ADR-0017) y no se escribe ni se reconcilia nada.
/// </summary>
public class ObtenerEstadoDeAccesoHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- CA-29: los tres estados ----

    [Fact]
    public async Task F004_CA29_ClienteSinUsuarioEnElProveedor_EstadoSinAcceso()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal(new EstadoDeAccesoDto(escenario.Cliente.Id, "SinAcceso", null), dto);
    }

    [Fact]
    public async Task F004_CA29_ClienteConUsuarioHabilitado_EstadoHabilitado()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal(escenario.Cliente.Id, dto.ClienteId);
        Assert.Equal("Habilitado", dto.Estado);
    }

    [Fact]
    public async Task F004_CA29_ClienteConUsuarioDeshabilitado_EstadoDeshabilitado()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: false);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal("Deshabilitado", dto.Estado);
    }

    [Fact]
    public async Task F004_CA29_UsuarioSinVinculoEnLaBase_SeAdoptaParaElEstado()
    {
        // Arrange: un habilitar interrumpido dejó al usuario en Keycloak sin vínculo (ADR-0017).
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true, conVinculo: false);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal("Habilitado", dto.Estado);
    }

    // ---- CA-29: cliente inexistente ----

    [Fact]
    public async Task F004_CA29_ClienteInexistente_LanzaNoEncontradoSinLlamarAlProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        var excepcion = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() =>
            escenario.ObtenerEstado.EjecutarAsync(new ObtenerEstadoDeAccesoConsulta(Guid.NewGuid()), Ct));

        // Assert
        Assert.Equal("No existe un cliente con ese identificador.", excepcion.Message);
        Assert.Empty(escenario.Proveedor.Llamadas);
    }

    // ---- RF-16: primer ingreso pendiente ----

    [Fact]
    public async Task F004_RF16_HabilitadoConAccionesObligatorias_PrimerIngresoPendienteTrue()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true, primerIngresoPendiente: true);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal("Habilitado", dto.Estado);
        Assert.True(dto.PrimerIngresoPendiente);
    }

    [Fact]
    public async Task F004_RF16_HabilitadoSinAccionesObligatorias_PrimerIngresoPendienteFalse()
    {
        // Arrange: el cliente ya completó su primer ingreso.
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true, primerIngresoPendiente: false);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.False(dto.PrimerIngresoPendiente);
    }

    [Fact]
    public async Task F004_RF16_DeshabilitadoConAccionesObligatorias_PrimerIngresoPendienteNull()
    {
        // Arrange: solo tiene sentido si el acceso está Habilitado.
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: false, primerIngresoPendiente: true);

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal("Deshabilitado", dto.Estado);
        Assert.Null(dto.PrimerIngresoPendiente);
    }

    [Fact]
    public async Task F004_RF16_SinAcceso_PrimerIngresoPendienteNull()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        var dto = await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Null(dto.PrimerIngresoPendiente);
    }

    // ---- RF-16: es una consulta ----

    [Fact]
    public async Task F004_RF16_Consulta_NoEscribeNiGuardaNiLlamaOperacionesQueCambian()
    {
        // Arrange: un usuario sin vínculo, que habilitar/deshabilitar adoptaría y vincularía.
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true, conVinculo: false);

        // Act
        await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Equal(0, escenario.Unidad.Llamadas);
        Assert.Equal(0, escenario.Proveedor.Cambios);
        Assert.Empty(escenario.Vinculos.VinculosSinGuardar);
        Assert.Empty(escenario.Vinculos.Vinculos);
        Assert.True(escenario.Registro.EstaVacio);
    }

    [Fact]
    public async Task F004_RF16_ConSolicitudPendiente_NoLaReconcilia()
    {
        // Arrange: la marca del usuario coincide con una solicitud pendiente; habilitar la cerraría como Completada.
        var escenario = new EscenarioDeAcceso();
        var solicitud = SolicitudDeAcceso.ParaHabilitar(escenario.Cliente.Id, EscenarioDeAcceso.Operador,
            EstadoDeAcceso.SinAcceso, Datos.Ahora.AddMinutes(-5));
        escenario.Registro.Sembrar(solicitud);
        escenario.SembrarUsuario(habilitado: true, marca: solicitud.Id);

        // Act
        await escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct);

        // Assert
        Assert.Empty(escenario.Registro.Resultados);
        Assert.Equal(0, escenario.Unidad.Llamadas);
    }

    // ---- CL-20: el sistema de identidad falla ----

    [Fact]
    public async Task F004_CL20_ProveedorSinRespuesta_LanzaSinRespuesta()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario();
        escenario.Proveedor.NoResponder(tambienAlLeer: true);

        // Act / Assert
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(() =>
            escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct));
    }

    [Fact]
    public async Task F004_CL20_ProveedorRespondeConError_LanzaErrorDelProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario();
        escenario.Proveedor.ResponderConError(tambienAlLeer: true);

        // Act / Assert
        await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(() =>
            escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct));
    }

    // ---- Inconsistencia: vínculo sin usuario ----

    [Fact]
    public async Task F004_RF16_VinculoSinUsuarioEnElProveedor_LanzaInvalidOperation()
    {
        // Arrange: hay vínculo en la base, pero el usuario ya no existe en Keycloak (riesgo 7 de la 003: 500).
        var escenario = new EscenarioDeAcceso();
        escenario.Vinculos.Sembrar(AccesoDigital.Vincular(escenario.Cliente.Id, "sub-que-ya-no-existe", Datos.Ahora));

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            escenario.ObtenerEstado.EjecutarAsync(escenario.ConsultaDeEstado(), Ct));
    }
}
