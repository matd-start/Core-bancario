using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Tests.Fakes;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.Tests.AccesosDigitales;

/// <summary>
/// ADR-0017, reconciliación (a): al reintentar, cada solicitud pendiente se cierra mirando la marca del usuario en el
/// sistema de identidad. Si la marca coincide con su Id, la operación se aplicó entera.
/// </summary>
public class ReconciliadorDeSolicitudesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid ClienteId = Guid.NewGuid();
    private readonly UnidadDeTrabajoEspia _unidad = new();

    private (ReconciliadorDeSolicitudes Reconciliador, RegistroDeAccesosEnMemoria Registro) Crear()
    {
        var registro = new RegistroDeAccesosEnMemoria(_unidad);
        return (new ReconciliadorDeSolicitudes(registro, _unidad, Datos.Reloj), registro);
    }

    private static SolicitudDeAcceso Pendiente(DateTimeOffset fecha, Guid? clienteId = null) =>
        SolicitudDeAcceso.ParaHabilitar(clienteId ?? ClienteId, "operador-de-pruebas", EstadoDeAcceso.SinAcceso, fecha);

    [Fact]
    public async Task F003_ADR0017_MarcaCoincide_CierraComoCompletadaConfirmadaAlReintentar()
    {
        // Arrange
        var (reconciliador, registro) = Crear();
        var solicitud = Pendiente(Datos.Ahora.AddMinutes(-5));
        registro.Sembrar(solicitud);
        var usuario = new UsuarioDeIdentidad("sub", true, solicitud.Id);

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, usuario, Ct);

        // Assert
        var resultado = Assert.Single(resultados);
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal(ResultadoDeAcceso.Completado, resultado.Resultado);
        Assert.Equal("Confirmada al reintentar", resultado.Motivo);
        Assert.Equal(Datos.Ahora, resultado.Fecha);
        Assert.Equal(resultado.SolicitudId, Assert.Single(registro.Resultados).SolicitudId);
    }

    [Fact]
    public async Task F003_ADR0017_MarcaNoCoincide_CierraComoFallida()
    {
        // Arrange
        var (reconciliador, registro) = Crear();
        var solicitud = Pendiente(Datos.Ahora.AddMinutes(-5));
        registro.Sembrar(solicitud);
        var usuario = new UsuarioDeIdentidad("sub", true, Guid.NewGuid());

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, usuario, Ct);

        // Assert
        var resultado = Assert.Single(resultados);
        Assert.Equal(ResultadoDeAcceso.Fallido, resultado.Resultado);
        Assert.Equal("No se aplicó; cerrada al reintentar", resultado.Motivo);
    }

    [Fact]
    public async Task F003_ADR0017_UsuarioInexistente_CierraComoFallida()
    {
        // Arrange: la solicitud nunca llegó a crear al usuario.
        var (reconciliador, registro) = Crear();
        registro.Sembrar(Pendiente(Datos.Ahora.AddMinutes(-5)));

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, null, Ct);

        // Assert
        Assert.Equal(ResultadoDeAcceso.Fallido, Assert.Single(resultados).Resultado);
    }

    [Fact]
    public async Task F003_ADR0017_SinPendientes_NoGuardaNadaYDevuelveVacio()
    {
        // Arrange
        var (reconciliador, registro) = Crear();
        var cerrada = Pendiente(Datos.Ahora.AddMinutes(-5));
        registro.Sembrar(cerrada, cerrada.Completar(Datos.Ahora.AddMinutes(-4)));

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, null, Ct);

        // Assert
        Assert.Empty(resultados);
        Assert.Equal(0, _unidad.Llamadas);
    }

    [Fact]
    public async Task F003_ADR0017_VariasPendientes_LasCierraTodasConUnSoloGuardado()
    {
        // Arrange: dos intentos sin respuesta; solo el último llegó a aplicarse (la marca es de la más reciente).
        var (reconciliador, registro) = Crear();
        var primera = Pendiente(Datos.Ahora.AddMinutes(-10));
        var segunda = Pendiente(Datos.Ahora.AddMinutes(-5));
        registro.Sembrar(segunda);
        registro.Sembrar(primera);
        var usuario = new UsuarioDeIdentidad("sub", true, segunda.Id);

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, usuario, Ct);

        // Assert
        Assert.Equal(1, _unidad.Llamadas);
        Assert.Equal(
            [(primera.Id, ResultadoDeAcceso.Fallido), (segunda.Id, ResultadoDeAcceso.Completado)],
            resultados.Select(r => (r.SolicitudId, r.Resultado)));
    }

    [Fact]
    public async Task F003_ADR0017_PendientesDeOtroCliente_NoSeTocan()
    {
        // Arrange
        var (reconciliador, registro) = Crear();
        registro.Sembrar(Pendiente(Datos.Ahora.AddMinutes(-5), clienteId: Guid.NewGuid()));

        // Act
        var resultados = await reconciliador.ReconciliarAsync(ClienteId, null, Ct);

        // Assert
        Assert.Empty(resultados);
        Assert.Empty(registro.Resultados);
    }
}
