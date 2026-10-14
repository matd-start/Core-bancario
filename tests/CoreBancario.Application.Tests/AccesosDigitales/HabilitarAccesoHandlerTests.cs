using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.AccesosDigitales;
using static CoreBancario.Application.Tests.AccesosDigitales.EscenarioDeAcceso;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>
/// RF-13, RN-20: habilitar y restablecer el acceso de un cliente. Una sola operación (CL-13): la acción que queda en el
/// registro depende del estado actual. Los fakes del sistema de identidad y de los repositorios están en Fakes/.
/// </summary>
public class HabilitarAccesoHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- CA-01: cliente sin acceso ----

    [Fact]
    public async Task F003_CA01_HabilitarClienteSinAcceso_CreaUsuarioYRegistraSolicitudYCompletado()
    {
        // Arrange
        var clave = Clave("A");
        var escenario = new EscenarioDeAcceso(claves: clave);

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        var solicitud = Assert.Single(escenario.Registro.Solicitudes);
        Assert.Equal(AccionDeAcceso.Habilitar, solicitud.Accion);
        Assert.Equal(escenario.Cliente.Id, solicitud.ClienteId);
        Assert.Equal(Operador, solicitud.Operador);
        Assert.Equal(Datos.Ahora, solicitud.Fecha);
        var resultado = Assert.Single(escenario.Registro.Resultados);
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Completado, resultado.Resultado);
        var usuario = Assert.Single(escenario.Proveedor.Usuarios);
        Assert.Equal(escenario.NombreDeUsuario.ToLowerInvariant(), usuario.NombreDeUsuario);
        Assert.Equal(clave.Valor, usuario.ClaveTemporal);
        Assert.Equal(solicitud.Id, usuario.Marca);
        Assert.Equal([nameof(IProveedorDeIdentidad.BuscarPorNombreDeUsuarioAsync), nameof(IProveedorDeIdentidad.CrearAsync)],
            escenario.Proveedor.Llamadas);
        Assert.Equal(new AccesoHabilitadoDto(solicitud.Id, escenario.Cliente.Id, "Habilitar", clave.Valor, solicitud.Fecha), dto);
    }

    [Fact]
    public async Task F003_CA01_HabilitarClienteSinAcceso_VinculaElSubConElCliente()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        var vinculo = Assert.Single(escenario.Vinculos.Vinculos);
        Assert.Equal(escenario.Cliente.Id, vinculo.ClienteId);
        Assert.Equal(Assert.Single(escenario.Proveedor.Usuarios).Sub, vinculo.Sub);
        Assert.Equal(Datos.Ahora, vinculo.FechaDeVinculo);
    }

    [Fact]
    public async Task F003_CA01_HabilitarClienteSinAcceso_DejaElSegundoFactorSinInscribirYLasAccionesObligatorias()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        var usuario = Assert.Single(escenario.Proveedor.Usuarios);
        Assert.True(usuario.Habilitado);
        Assert.False(usuario.OtpInscrito);
        Assert.Equal(["UPDATE_PASSWORD", "CONFIGURE_TOTP"], usuario.AccionesObligatorias);
    }

    [Fact]
    public async Task F003_RN20_HabilitarAcceso_RegistraAlOperadorDelComando()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar("operador-que-lo-hizo"), Ct);

        // Assert
        Assert.Equal("operador-que-lo-hizo", Assert.Single(escenario.Registro.Solicitudes).Operador);
    }

    [Fact]
    public async Task F003_ADR0017_HabilitarSinAcceso_GuardaDosVeces_LaIntencionYElResultado()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert: un guardado para la intención (antes del proveedor) y otro para el vínculo y el resultado.
        Assert.Equal(2, escenario.Unidad.Llamadas);
    }

    // ---- CA-02 / CL-13: restablecer ----

    [Fact]
    public async Task F003_CA02_HabilitarClienteHabilitado_RestableceConClaveDistintaYRegistraRestablecer()
    {
        // Arrange: habilitado y con el segundo factor ya inscrito.
        var nueva = Clave("B");
        var escenario = new EscenarioDeAcceso(claves: nueva);
        var usuario = escenario.SembrarUsuario(habilitado: true, otpInscrito: true);
        var claveAnterior = usuario.ClaveTemporal;

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.Equal("Restablecer", dto.Accion);
        Assert.Equal(nueva.Valor, dto.ClaveTemporal);
        Assert.NotEqual(claveAnterior, dto.ClaveTemporal);
        Assert.Equal(AccionDeAcceso.Restablecer, Assert.Single(escenario.Registro.Solicitudes).Accion);
        Assert.False(usuario.OtpInscrito);
        Assert.Equal(1, usuario.VecesQueSeCerraronSusSesiones);
        Assert.Contains(nameof(IProveedorDeIdentidad.ReiniciarAccesoAsync), escenario.Proveedor.Llamadas);
        Assert.DoesNotContain(nameof(IProveedorDeIdentidad.CrearAsync), escenario.Proveedor.Llamadas);
    }

    [Fact]
    public async Task F003_CA02_HabilitarDosVeces_LaSegundaEsRestablecerConOtraClave()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso(claves: [Clave("A"), Clave("B")]);

        // Act
        var primera = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);
        var segunda = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.Equal(["Habilitar", "Restablecer"], new[] { primera.Accion, segunda.Accion });
        Assert.NotEqual(primera.ClaveTemporal, segunda.ClaveTemporal);
        Assert.Equal(2, escenario.Registro.Solicitudes.Count);
        Assert.Single(escenario.Vinculos.Vinculos);
    }

    [Fact]
    public async Task F003_CL13_HabilitarClienteHabilitado_NoCreaOtroVinculo()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.SembrarUsuario(habilitado: true, otpInscrito: true);

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.Single(escenario.Vinculos.Vinculos);
        Assert.Empty(escenario.Vinculos.VinculosSinGuardar);
    }

    // ---- CL-16: rehabilitar ----

    [Fact]
    public async Task F003_CL16_HabilitarClienteDeshabilitado_RegistraHabilitarYLoHabilita()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var usuario = escenario.SembrarUsuario(habilitado: false);

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.Equal("Habilitar", dto.Accion);
        Assert.Equal(AccionDeAcceso.Habilitar, Assert.Single(escenario.Registro.Solicitudes).Accion);
        Assert.True(usuario.Habilitado);
        Assert.Contains(nameof(IProveedorDeIdentidad.ReiniciarAccesoAsync), escenario.Proveedor.Llamadas);
    }

    // ---- CA-04 / CL-12: cliente inexistente ----

    [Fact]
    public async Task F003_CA04_HabilitarClienteInexistente_NoRegistraNadaNiLlamaAlProveedor()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var comando = new HabilitarAccesoComando(Guid.NewGuid(), Operador);

        // Act
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => escenario.Habilitar.EjecutarAsync(comando, Ct));

        // Assert
        Assert.True(escenario.Registro.EstaVacio);
        Assert.Empty(escenario.Vinculos.Vinculos);
        Assert.Empty(escenario.Proveedor.Llamadas);
        Assert.Equal(0, escenario.Unidad.Llamadas);
    }

    [Fact]
    public async Task F003_CL12_HabilitarClienteInexistente_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        var comando = new HabilitarAccesoComando(Guid.NewGuid(), Operador);

        // Act
        var accion = () => escenario.Habilitar.EjecutarAsync(comando, Ct);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }

    // ---- ADR-0017: intención primero ----

    [Fact]
    public async Task F003_ADR0017_SolicitudSeConfirmaAntesDeLlamarAlProveedor()
    {
        // Arrange: en el momento exacto de la llamada que cambia algo, el fake mira qué estaba ya guardado.
        var escenario = new EscenarioDeAcceso();
        var solicitudesGuardadasAlLlamar = -1;
        var resultadosGuardadosAlLlamar = -1;
        escenario.Proveedor.AlEmpezarUnCambio = () =>
        {
            solicitudesGuardadasAlLlamar = escenario.Registro.Solicitudes.Count;
            resultadosGuardadosAlLlamar = escenario.Registro.Resultados.Count;
        };

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.Equal(1, solicitudesGuardadasAlLlamar);
        Assert.Equal(0, resultadosGuardadosAlLlamar);
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorRespondeConError_RegistraFallidoYPropaga()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.Proveedor.ResponderConError();

        // Act
        var error = await Assert.ThrowsAsync<ErrorDelProveedorDeIdentidadException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));

        // Assert
        var solicitud = Assert.Single(escenario.Registro.Solicitudes);
        var resultado = Assert.Single(escenario.Registro.Resultados);
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Fallido, resultado.Resultado);
        Assert.Equal(error.Motivo, resultado.Motivo);
        Assert.Empty(escenario.Vinculos.Vinculos);
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorNoResponde_DejaLaSolicitudPendiente()
    {
        // Arrange
        var escenario = new EscenarioDeAcceso();
        escenario.Proveedor.NoResponder();

        // Act
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));

        // Assert: un tiempo de espera agotado no es un "no": no se escribe resultado.
        Assert.Single(escenario.Registro.Solicitudes);
        Assert.Empty(escenario.Registro.Resultados);
        Assert.Empty(escenario.Vinculos.Vinculos);
    }

    [Fact]
    public async Task F003_ADR0017_ReintentoConPendienteAplicada_LaCierraComoCompletada()
    {
        // Arrange: el primer intento SE APLICÓ en el proveedor pero no hubo respuesta.
        var escenario = new EscenarioDeAcceso(claves: [Clave("A"), Clave("B")]);
        escenario.Proveedor.AplicarYNoResponder();
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));
        var pendiente = Assert.Single(escenario.Registro.Solicitudes);
        escenario.Proveedor.Normalizar();

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert: la anterior queda Completada por la marca, y la nueva es un Restablecer (el usuario ya existía).
        var cierre = Assert.Single(escenario.Registro.Resultados, r => r.SolicitudId == pendiente.Id);
        Assert.Equal(ResultadoDeAcceso.Completado, cierre.Resultado);
        Assert.Equal("Confirmada al reintentar", cierre.Motivo);
        Assert.Equal("Restablecer", dto.Accion);
        Assert.Equal(2, escenario.Registro.Solicitudes.Count);
    }

    [Fact]
    public async Task F003_ADR0017_ReintentoConPendienteNoAplicada_LaCierraComoFallida()
    {
        // Arrange: el primer intento NO llegó al proveedor.
        var escenario = new EscenarioDeAcceso(claves: [Clave("A"), Clave("B")]);
        escenario.Proveedor.NoResponder();
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));
        var pendiente = Assert.Single(escenario.Registro.Solicitudes);
        escenario.Proveedor.Normalizar();

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        var cierre = Assert.Single(escenario.Registro.Resultados, r => r.SolicitudId == pendiente.Id);
        Assert.Equal(ResultadoDeAcceso.Fallido, cierre.Resultado);
        Assert.Equal("Habilitar", dto.Accion);
    }

    [Fact]
    public async Task F003_ADR0017_UsuarioExisteSinVinculo_LoAdoptaSinCrearOtro()
    {
        // Arrange: quedó un usuario de un habilitar interrumpido, y no hay vínculo en la base.
        var escenario = new EscenarioDeAcceso();
        var huerfano = escenario.SembrarUsuario(habilitado: true, conVinculo: false);

        // Act
        await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        Assert.DoesNotContain(nameof(IProveedorDeIdentidad.CrearAsync), escenario.Proveedor.Llamadas);
        Assert.Contains(nameof(IProveedorDeIdentidad.ReiniciarAccesoAsync), escenario.Proveedor.Llamadas);
        Assert.Single(escenario.Proveedor.Usuarios);
        var vinculo = Assert.Single(escenario.Vinculos.Vinculos);
        Assert.Equal(huerfano.Sub, vinculo.Sub);
        Assert.Equal(escenario.Cliente.Id, vinculo.ClienteId);
    }

    [Fact]
    public async Task F003_ADR0017_FallaLaBaseTrasAplicar_LaSolicitudQuedaPendiente()
    {
        // Arrange: el segundo guardado (vínculo y resultado) falla porque la base cae; el primero (la intención) sí se guardó.
        var unidad = new UnidadDeTrabajoEspia
        {
            ExcepcionAlGuardar = new InvalidOperationException("la base se cayó"),
            GuardadoQueFalla = 2,
        };
        var escenario = new EscenarioDeAcceso(unidad);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));

        // Assert: se aplicó en el proveedor, pero no quedó resultado ni vínculo guardados.
        Assert.Single(escenario.Registro.Solicitudes);
        Assert.Empty(escenario.Registro.Resultados);
        Assert.Empty(escenario.Vinculos.Vinculos);
        Assert.Single(escenario.Proveedor.Usuarios);
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorNoRespondeAlLocalizar_NoRegistraNada()
    {
        // Arrange: el proveedor está caído desde el primer paso.
        var escenario = new EscenarioDeAcceso();
        escenario.Proveedor.NoResponder(tambienAlLeer: true);

        // Act
        await Assert.ThrowsAsync<ProveedorDeIdentidadSinRespuestaException>(
            () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct));

        // Assert: todavía no se pidió nada.
        Assert.True(escenario.Registro.EstaVacio);
        Assert.Equal(0, escenario.Unidad.Llamadas);
        Assert.Equal(0, escenario.Proveedor.Cambios);
    }

    [Fact]
    public async Task F003_ADR0017_OperadorCancelaTrasConfirmarLaIntencion_LaOperacionTerminaIgual()
    {
        // Arrange: el operador cierra el navegador justo después de que se guarda la intención.
        using var cancelacion = new CancellationTokenSource();
        var escenario = new EscenarioDeAcceso();
        escenario.Unidad.Confirmado += cancelacion.Cancel;

        // Act
        var dto = await escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), cancelacion.Token);

        // Assert: desde la intención confirmada nada usa el token del operador (CancellationToken.None).
        Assert.True(cancelacion.IsCancellationRequested);
        Assert.Equal("Habilitar", dto.Accion);
        Assert.Equal(ResultadoDeAcceso.Completado, Assert.Single(escenario.Registro.Resultados).Resultado);
        Assert.All(escenario.Proveedor.TokensDeLosCambios, token => Assert.False(token.CanBeCanceled));
        Assert.All(escenario.Unidad.Tokens.Skip(1), token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task F003_RN20_ConflictoAlGuardarElVinculo_PropagaElConflictoDeConcurrencia()
    {
        // Arrange: otro operador ganó la carrera por el mismo vínculo (índice único).
        var unidad = new UnidadDeTrabajoEspia
        {
            ExcepcionAlGuardar = new ConflictoDeConcurrenciaException(),
            GuardadoQueFalla = 2,
        };
        var escenario = new EscenarioDeAcceso(unidad);

        // Act
        var accion = () => escenario.Habilitar.EjecutarAsync(escenario.ComandoDeHabilitar(), Ct);

        // Assert
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(accion);
    }
}
