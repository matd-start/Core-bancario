using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Comun;
using CoreBancario.Domain.AccesosDigitales;
using static CoreBancario.Application.Tests.AccesosDigitales.EscenarioDeAcceso;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>
/// RF-13, RN-20: deshabilitar el acceso. Sigue la misma secuencia "intención primero" que habilitar (ADR-0017), pero
/// valida ANTES de escribir la solicitud: si no hay nada que deshabilitar, no queda ningún registro (CL-14).
/// </summary>
public class DeshabilitarAccesoHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- CA-03: deshabilitar un acceso habilitado ----

    [Fact]
    public async Task F003_CA03_DeshabilitarClienteHabilitado_RegistraDeshabilitar()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var usuario = escenario.SembrarUsuario(habilitado: true, otpInscrito: true);

        // Act
        var dto = await escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct);

        // Assert
        var solicitud = Assert.Single(escenario.Registro.Solicitudes);
        Assert.Equal(AccionDeAcceso.Deshabilitar, solicitud.Accion);
        Assert.Equal(Operador, solicitud.Operador);
        Assert.Equal(escenario.Cliente.Id, solicitud.ClienteId);
        Assert.Equal(ResultadoDeAcceso.Completado, Assert.Single(escenario.Registro.Resultados).Resultado);
        Assert.Equal(new RegistroDeAccesoDto(solicitud.Id, escenario.Cliente.Id, "Deshabilitar", solicitud.Fecha), dto);
        Assert.False(usuario.Habilitado);
        Assert.Equal(solicitud.Id, usuario.Marca);
    }

    [Fact]
    public async Task F003_CA03_DeshabilitarClienteHabilitado_CierraSusSesionesEnElProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var usuario = escenario.SembrarUsuario(habilitado: true);

        // Act
        await escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct);

        // Assert
        Assert.Equal(1, usuario.VecesQueSeCerraronSusSesiones);
        Assert.Contains(nameof(IProveedorDeIdentidad.DeshabilitarAsync), escenario.Proveedor.Llamadas);
    }

    [Fact]
    public async Task F003_CA03_DeshabilitarClienteHabilitado_NoCreaOtroVinculo()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true);

        // Act
        await escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct);

        // Assert
        Assert.Single(escenario.Vinculos.Vinculos);
        Assert.Empty(escenario.Vinculos.VinculosSinGuardar);
    }

    // ---- CA-04 / CL-12 / CL-14: lo que se rechaza no deja registro ----

    [Fact]
    public async Task F003_CA04_DeshabilitarYaDeshabilitado_LanzaAccesoNoHabilitadoSinRegistrar()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: false);

        // Act
        var error = await Assert.ThrowsAsync<AccesoNoHabilitadoException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));

        // Assert
        Assert.Equal(EstadoDeAcceso.Deshabilitado, error.EstadoActual);
        Assert.True(escenario.Registro.EstaVacio);
        Assert.Equal(0, escenario.Proveedor.Cambios);
    }

    [Fact]
    public async Task F003_CL14_DeshabilitarClienteSinAcceso_LanzaAccesoNoHabilitadoConEstadoSinAcceso()
    {
        // Arrange: ni vínculo ni usuario en el proveedor.
        var escenario = new EscenarioDeAcceso();

        // Act
        var error = await Assert.ThrowsAsync<AccesoNoHabilitadoException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));

        // Assert
        Assert.Equal(EstadoDeAcceso.SinAcceso, error.EstadoActual);
        Assert.True(escenario.Registro.EstaVacio);
        Assert.Equal(0, escenario.Proveedor.Cambios);
    }

    [Fact]
    public async Task F003_CA04_DeshabilitarClienteInexistente_LanzaNoEncontradoSinRegistrarNiLlamarAlProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var comando = new DeshabilitarAccesoComando(Guid.NewGuid(), Operador);

        // Act
        var accion = () => escenario.Deshabilitar.EjecutarAsync(comando, Ct);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
        Assert.True(escenario.Registro.EstaVacio);
        Assert.Empty(escenario.Proveedor.Llamadas);
        Assert.Equal(0, escenario.Unidad.Llamadas);
    }

    // ---- ADR-0017: intención primero ----

    [Fact]
    public async Task F003_ADR0017_DeshabilitarConfirmaLaSolicitudAntesDeLlamarAlProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true);
        var solicitudesGuardadasAlLlamar = -1;
        escenario.Proveedor.AlEmpezarUnCambio = () => solicitudesGuardadasAlLlamar = escenario.Registro.Solicitudes.Count;

        // Act
        await escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct);

        // Assert
        Assert.Equal(1, solicitudesGuardadasAlLlamar);
    }

    [Fact]
    public async Task F003_ADR0017_DeshabilitarConErrorDelProveedor_RegistraFallidoYPropaga()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var usuario = escenario.SembrarUsuario(habilitado: true);
        escenario.Proveedor.ResponderConError();

        // Act
        var error = await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));

        // Assert
        var resultado = Assert.Single(escenario.Registro.Resultados);
        Assert.Equal(ResultadoDeAcceso.Fallido, resultado.Resultado);
        Assert.Equal(error.Motivo, resultado.Motivo);
        Assert.True(usuario.Habilitado);
    }

    [Fact]
    public async Task F003_ADR0017_DeshabilitarSinRespuestaDelProveedor_DejaLaSolicitudPendiente()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true);
        escenario.Proveedor.NoResponder();

        // Act
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));

        // Assert
        Assert.Single(escenario.Registro.Solicitudes);
        Assert.Empty(escenario.Registro.Resultados);
    }

    [Fact]
    public async Task F003_ADR0017_DeshabilitarAplicadoSinRespuestaYReintento_CierraLaAnteriorYLanzaAccesoNoHabilitado()
    {
        // Arrange: el deshabilitar SE APLICÓ en el proveedor pero el operador no recibió respuesta.
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true);
        escenario.Proveedor.AplicarYNoResponder();
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));
        var pendiente = Assert.Single(escenario.Registro.Solicitudes);
        escenario.Proveedor.Normalizar();

        // Act: el reintento ve el acceso ya deshabilitado.
        var error = await Assert.ThrowsAsync<AccesoNoHabilitadoException>(
            () => escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), Ct));

        // Assert: un "ya está deshabilitado" correcto; la anterior se cierra Completada y no se escribe una solicitud nueva.
        Assert.Equal(EstadoDeAcceso.Deshabilitado, error.EstadoActual);
        Assert.Equal(pendiente.Id, Assert.Single(escenario.Registro.Solicitudes).Id);
        var cierre = Assert.Single(escenario.Registro.Resultados);
        Assert.Equal(pendiente.Id, cierre.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Completado, cierre.Resultado);
    }

    [Fact]
    public async Task F003_ADR0017_OperadorCancelaTrasConfirmarLaIntencion_ElDeshabilitarTerminaIgual()
    {
        // Arrange
        using var cancelacion = new CancellationTokenSource();
        var escenario = new EscenarioDeAcceso();
        var usuario = escenario.SembrarUsuario(habilitado: true);
        escenario.Unidad.Confirmado += cancelacion.Cancel;

        // Act
        await escenario.Deshabilitar.EjecutarAsync(escenario.ComandoDeDeshabilitar(), cancelacion.Token);

        // Assert
        Assert.False(usuario.Habilitado);
        Assert.Equal(ResultadoDeAcceso.Completado, Assert.Single(escenario.Registro.Resultados).Resultado);
        Assert.All(escenario.Proveedor.TokensDeLosCambios, token => Assert.False(token.CanBeCanceled));
    }
}
