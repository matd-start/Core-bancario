using System.Net;
using System.Net.Http.Json;
using CoreBancario.Application.AccesosDigitales;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

/// <summary>
/// ADR-0017, RNF-02: el operador debe poder leer qué pasó cuando el sistema de identidad falla. Los errores del catálogo
/// (502 y 503) llevan su mensaje fijo en "detail" y se registran como Warning (son fallos de un tercero, no del sistema);
/// el 500 inesperado sigue sin detail y en Error. El motivo técnico y el cuerpo de Keycloak nunca llegan al cliente.
/// </summary>
public class ErroresDeIdentidadApiTests(ApiFixture api)
{
    private const string DetalleDe502 = "El sistema de identidad rechazó la operación y no se completó. Vuelve a intentarla.";
    private const string DetalleDe503 = "El sistema de identidad no respondió. La operación quedó pendiente de confirmar; vuelve a intentarla.";

    private const string CategoriaDelManejador = "CoreBancario.Api.Errores.ManejadorDeErrores";

    /// <summary>Habilita a un cliente nuevo con un sistema de identidad que falla así. Devuelve la respuesta y el id del cliente.</summary>
    private async Task<(HttpResponseMessage Respuesta, Guid ClienteId)> HabilitarConFallaAsync(FallaDelProveedor falla)
    {
        using var operador = api.CrearCliente();
        var cliente = await RegistrarClienteAsync(operador);
        var proveedor = new ProveedorDeIdentidadFalso(falla);
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<IProveedorDeIdentidad>();
            servicios.AddSingleton<IProveedorDeIdentidad>(proveedor);
        });
        using var http = api.CrearClienteDe(fabrica);
        var respuesta = await http.PostAsync($"/clientes/{cliente.Id}/acceso-digital/habilitar", null, Ct);
        return (respuesta, cliente.Id);
    }

    private IEnumerable<RegistroDeLog> LogsDelManejadorDe(Guid clienteId) =>
        api.Logs.QueContienen(clienteId.ToString()).Where(r => r.Categoria == CategoriaDelManejador);

    // ---- detail con el mensaje fijo ----

    [Fact]
    public async Task F003_ADR0017_ErrorDelProveedor_Responde502ConElMensajeFijoEnDetail()
    {
        // Arrange / Act
        var (respuesta, _) = await HabilitarConFallaAsync(FallaDelProveedor.ConError);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal("identidad-error", problema.Codigo);
        Assert.Equal(DetalleDe502, problema.Detail);
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorSinRespuesta_Responde503ConElMensajeFijoEnDetail()
    {
        // Arrange / Act
        var (respuesta, _) = await HabilitarConFallaAsync(FallaDelProveedor.SinRespuesta);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal("identidad-sin-respuesta", problema.Codigo);
        Assert.Equal(DetalleDe503, problema.Detail);
    }

    [Theory]
    [InlineData(FallaDelProveedor.ConError)]
    [InlineData(FallaDelProveedor.SinRespuesta)]
    public async Task F003_ADR0017_DetailDelCatalogo_NoIncluyeElMotivoTecnico(FallaDelProveedor falla)
    {
        // Arrange / Act: el motivo del fake es "HTTP 500 al aplicar el cambio".
        var (respuesta, _) = await HabilitarConFallaAsync(falla);

        // Assert: hay detail (el mensaje fijo) y no trae el motivo técnico.
        var problema = await LeerProblemaAsync(respuesta);
        Assert.False(string.IsNullOrWhiteSpace(problema.Detail));
        Assert.DoesNotContain("HTTP 500", problema.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("aplicar el cambio", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task F003_RNF02_ErrorInesperado500_SigueSinDetail()
    {
        // Arrange: guardia de regresión, ya cumplida hoy. Un 500 inesperado no envía su mensaje (puede traer SQL o rutas).
        const string mensajeInterno = "SELECT secreto FROM interno";
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<TimeProvider>();
            servicios.AddSingleton<TimeProvider>(new RelojQueLanza(mensajeInterno));
        });
        using var http = api.CrearClienteDe(fabrica);

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro(), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal("error-inesperado", problema.Codigo);
        Assert.Null(problema.Detail);
    }

    // ---- niveles de log ----

    [Theory]
    [InlineData(FallaDelProveedor.ConError)]
    [InlineData(FallaDelProveedor.SinRespuesta)]
    public async Task F003_ADR0017_Errores502Y503_SeRegistranComoWarning(FallaDelProveedor falla)
    {
        // Arrange / Act
        var (_, clienteId) = await HabilitarConFallaAsync(falla);

        // Assert
        var registros = LogsDelManejadorDe(clienteId).ToList();
        Assert.Contains(registros, r => r.Nivel == LogLevel.Warning);
        Assert.DoesNotContain(registros, r => r.Nivel >= LogLevel.Error);
    }

    [Fact]
    public async Task F003_RNF02_ErrorInesperado500_SigueRegistrandoseComoError()
    {
        // Arrange: guardia de regresión, ya cumplida hoy.
        const string marca = "mensaje-interno-unico-del-reloj";
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<TimeProvider>();
            servicios.AddSingleton<TimeProvider>(new RelojQueLanza(marca));
        });
        using var http = api.CrearClienteDe(fabrica);

        // Act
        await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro(), Ct);

        // Assert
        Assert.Contains(api.Logs.QueContienen(marca), r => r.Categoria == CategoriaDelManejador && r.Nivel == LogLevel.Error);
    }
}
