using System.Net;
using System.Net.Http.Json;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Clientes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

/// <summary>
/// RF-13, RN-19, RN-20 y ADR-0017 de punta a punta contra la API real y PostgreSQL, con un sistema de identidad falso
/// (ProveedorDeIdentidadFalso, ADR-0018). El operador sale SIEMPRE de su token, nunca de la petición. Lo que queda
/// registrado se lee por SQL: el registro de accesos es inmutable y solo se consulta en la base (spec, sección 9).
/// </summary>
public class AccesoDigitalApiTests(ApiFixture api)
{
    private static string Habilitar(Guid clienteId) => $"/clientes/{clienteId}/acceso-digital/habilitar";

    private static string Deshabilitar(Guid clienteId) => $"/clientes/{clienteId}/acceso-digital/deshabilitar";

    private static string SubDeOperadorNuevo() => $"operador-{Guid.NewGuid()}";

    private HttpClient ComoOperador(string sub) => api.CrearClienteConToken(EmisorDePruebas.CrearTokenDeOperador(sub));

    private async Task<ClienteDto> ClienteRegistradoAsync()
    {
        using var operador = api.CrearCliente();
        return await RegistrarClienteAsync(operador);
    }

    /// <summary>Un cliente registrado cuyo acceso ya fue habilitado por otro operador.</summary>
    private async Task<(ClienteDto Cliente, AccesoHabilitadoDto Acceso)> ClienteConAccesoAsync()
    {
        var cliente = await ClienteRegistradoAsync();
        using var operador = api.CrearCliente();
        var respuesta = await operador.PostAsync(Habilitar(cliente.Id), null, Ct);
        respuesta.EnsureSuccessStatusCode();
        return (cliente, (await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct))!);
    }

    private static string NombreDeUsuarioDe(ClienteDto cliente) => $"{cliente.TipoDocumento}{cliente.NumeroDocumento}";

    // ---- CA-01: habilitar el acceso de un cliente sin acceso ----

    [Fact]
    public async Task F003_CA01_HabilitarAcceso_Responde200ConClaveTemporal()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = ComoOperador(SubDeOperadorNuevo());

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.NotNull(acceso);
        Assert.Equal(cliente.Id, acceso.ClienteId);
        Assert.Equal("Habilitar", acceso.Accion);
        Assert.True(acceso.ClaveTemporal.Length >= 16, "La clave temporal debe tener al menos 16 caracteres.");
        Assert.NotEqual(Guid.Empty, acceso.SolicitudId);
    }

    [Fact]
    public async Task F003_CA01_HabilitarAcceso_RegistraAlOperadorDelToken()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        var sub = SubDeOperadorNuevo();
        using var http = ComoOperador(sub);

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        var solicitud = Assert.Single(await api.SolicitudesDeAsync(cliente.Id));
        Assert.Equal(sub, solicitud.Operador);
        Assert.Equal("Habilitar", solicitud.Accion);
        Assert.Equal(acceso!.SolicitudId, solicitud.Id);
        var resultado = Assert.Single(await api.ResultadosDeAsync(cliente.Id));
        Assert.Equal(solicitud.Id, resultado.SolicitudId);
        Assert.Equal("Completado", resultado.Resultado);
    }

    [Fact]
    public async Task F003_CA01_HabilitarAcceso_CreaElUsuarioConSuDocumentoYVinculaElSub()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var usuario = api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente));
        Assert.NotNull(usuario);
        Assert.True(usuario.Habilitado);
        Assert.False(usuario.OtpInscrito);
        Assert.Equal(usuario.Sub, await api.SubVinculadoAsync(cliente.Id));
    }

    [Fact]
    public async Task F003_CA01_HabilitarAcceso_EntregaLaClaveQueQuedoEnElSistemaDeIdentidad()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.Equal(acceso!.ClaveTemporal, api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente))?.ClaveTemporal);
    }

    // ---- RNF-03: la clave temporal solo viaja en la respuesta ----

    [Fact]
    public async Task F003_RNF03_HabilitarAcceso_RespondeNoStore()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert: ningun proxy ni navegador debe guardar la respuesta que lleva la clave.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store.");
    }

    [Fact]
    public async Task F003_RNF03_HabilitarAcceso_NingunLogContieneLaClaveTemporal()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.NotNull(acceso);
        Assert.False(string.IsNullOrEmpty(acceso.ClaveTemporal));
        Assert.Empty(api.Logs.QueContienen(acceso.ClaveTemporal));
    }

    // ---- RNF-05: el log dice quién gestionó el acceso ----

    [Theory]
    [InlineData("HabilitarAccesoDigital", "habilitar")]
    [InlineData("DeshabilitarAccesoDigital", "deshabilitar")]
    public async Task F003_RNF05_GestionarAcceso_ElLogIncluyeElOperador(string operacion, string accion)
    {
        // Arrange: el operador de la prueba tiene un sub unico para encontrar SU registro entre los de las demas pruebas.
        var (cliente, _) = await ClienteConAccesoAsync();
        var sub = SubDeOperadorNuevo();
        using var http = ComoOperador(sub);

        // Act
        var respuesta = await http.PostAsync($"/clientes/{cliente.Id}/acceso-digital/{accion}", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(api.Logs.QueContienen(sub),
            r => r.Nivel == LogLevel.Information
                 && r.Mensaje.Contains(operacion, StringComparison.Ordinal)
                 && r.Mensaje.EndsWith(": completada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task F003_RNF05_HabilitarClienteInexistente_ElLogDiceNoCompletada()
    {
        // Arrange
        var sub = SubDeOperadorNuevo();
        using var http = ComoOperador(sub);

        // Act
        var respuesta = await http.PostAsync(Habilitar(Guid.NewGuid()), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains(api.Logs.QueContienen(sub),
            r => r.Mensaje.Contains("HabilitarAccesoDigital", StringComparison.Ordinal)
                 && r.Mensaje.EndsWith(": no completada", StringComparison.Ordinal));
    }

    // ---- CA-02 / CL-13: restablecer ----

    [Fact]
    public async Task F003_CA02_HabilitarDosVeces_LaSegundaEsRestablecerConOtraClave()
    {
        // Arrange
        var (cliente, primera) = await ClienteConAccesoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var segunda = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.Equal("Restablecer", segunda!.Accion);
        Assert.NotEqual(primera.ClaveTemporal, segunda.ClaveTemporal);
        Assert.Equal(["Habilitar", "Restablecer"], (await api.SolicitudesDeAsync(cliente.Id)).Select(s => s.Accion));
    }

    [Fact]
    public async Task F003_CA02_RestablecerUnAccesoConSegundoFactorInscrito_LoDejaSinInscribir()
    {
        // Arrange: un usuario ya existente con el segundo factor inscrito (por ejemplo, de un habilitar anterior).
        var cliente = await ClienteRegistradoAsync();
        var existente = api.Proveedor.Sembrar(NombreDeUsuarioDe(cliente), habilitado: true, otpInscrito: true);
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.Equal("Restablecer", acceso!.Accion);
        Assert.False(existente.OtpInscrito);
        Assert.Equal(1, existente.SesionesCerradas);
    }

    // ---- CL-16: rehabilitar ----

    [Fact]
    public async Task F003_CL16_HabilitarUnAccesoDeshabilitado_RespondeConLaAccionHabilitar()
    {
        // Arrange
        var (cliente, _) = await ClienteConAccesoAsync();
        using var http = api.CrearCliente();
        (await http.PostAsync(Deshabilitar(cliente.Id), null, Ct)).EnsureSuccessStatusCode();

        // Act
        var respuesta = await http.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        var acceso = await respuesta.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.Equal("Habilitar", acceso!.Accion);
        Assert.True(api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente))!.Habilitado);
    }

    // ---- CA-03: deshabilitar ----

    [Fact]
    public async Task F003_CA03_DeshabilitarAcceso_Responde200YRegistraDeshabilitar()
    {
        // Arrange
        var (cliente, _) = await ClienteConAccesoAsync();
        var sub = SubDeOperadorNuevo();
        using var http = ComoOperador(sub);

        // Act
        var respuesta = await http.PostAsync(Deshabilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var registro = await respuesta.Content.ReadFromJsonAsync<RegistroDeAccesoDto>(Ct);
        Assert.Equal(cliente.Id, registro!.ClienteId);
        Assert.Equal("Deshabilitar", registro.Accion);
        var solicitudes = await api.SolicitudesDeAsync(cliente.Id);
        Assert.Equal(["Habilitar", "Deshabilitar"], solicitudes.Select(s => s.Accion));
        Assert.Equal(sub, solicitudes[1].Operador);
        Assert.Equal(registro.SolicitudId, solicitudes[1].Id);
    }

    [Fact]
    public async Task F003_CA03_DeshabilitarAcceso_DeshabilitaYCierraLasSesionesDelUsuario()
    {
        // Arrange
        var (cliente, _) = await ClienteConAccesoAsync();
        using var http = api.CrearCliente();

        // Act
        await http.PostAsync(Deshabilitar(cliente.Id), null, Ct);

        // Assert
        var usuario = api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente));
        Assert.False(usuario!.Habilitado);
        Assert.Equal(1, usuario.SesionesCerradas);
    }

    // ---- CA-04 / CL-12 / CL-14: lo que se rechaza no deja registro ----

    [Fact]
    public async Task F003_CA04_HabilitarClienteInexistente_Responde404SinRegistros()
    {
        // Arrange
        var clienteInexistente = Guid.NewGuid();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(clienteInexistente), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(await api.SolicitudesDeAsync(clienteInexistente));
        Assert.Null(await api.SubVinculadoAsync(clienteInexistente));
    }

    [Fact]
    public async Task F003_CL12_HabilitarClienteInexistente_RespondeNoEncontradoConSuCodigo()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Habilitar(Guid.NewGuid()), null, Ct);

        // Assert
        Assert.Equal("no-encontrado", (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F003_CA04_DeshabilitarClienteInexistente_Responde404SinRegistros()
    {
        // Arrange
        var clienteInexistente = Guid.NewGuid();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Deshabilitar(clienteInexistente), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(await api.SolicitudesDeAsync(clienteInexistente));
    }

    [Fact]
    public async Task F003_CA04_DeshabilitarYaDeshabilitado_Responde422SinRegistros()
    {
        // Arrange
        var (cliente, _) = await ClienteConAccesoAsync();
        using var http = api.CrearCliente();
        (await http.PostAsync(Deshabilitar(cliente.Id), null, Ct)).EnsureSuccessStatusCode();
        var antes = (await api.SolicitudesDeAsync(cliente.Id)).Count;

        // Act
        var respuesta = await http.PostAsync(Deshabilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("acceso-no-habilitado", (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Equal(antes, (await api.SolicitudesDeAsync(cliente.Id)).Count);
    }

    [Fact]
    public async Task F003_CL14_DeshabilitarClienteSinAcceso_Responde422SinRegistros()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync(Deshabilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("acceso-no-habilitado", (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Empty(await api.SolicitudesDeAsync(cliente.Id));
    }

    // ---- CA-05 / CL-08: un cliente no gestiona accesos ----

    [Theory]
    [InlineData("habilitar")]
    [InlineData("deshabilitar")]
    public async Task F003_CA05_ClienteGestionaAcceso_Responde403SinRegistros(string accion)
    {
        // Arrange: el cliente existe; aun asi, el rol Cliente no puede gestionar accesos.
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearClienteComoCliente(Guid.NewGuid().ToString());

        // Act
        var respuesta = await http.PostAsync($"/clientes/{cliente.Id}/acceso-digital/{accion}", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("prohibido", (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Empty(await api.SolicitudesDeAsync(cliente.Id));
        Assert.Null(api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente)));
    }

    // ---- ADR-0017: 502 y 503 ----

    /// <summary>Una API derivada con su propio sistema de identidad falso y un cliente HTTP de Operador contra ella.</summary>
    private sealed class ApiConProveedor : IDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _fabrica;

        public ApiConProveedor(ApiFixture api, FallaDelProveedor falla)
        {
            Proveedor = new ProveedorDeIdentidadFalso(falla);
            _fabrica = api.ConServicios(servicios =>
            {
                servicios.RemoveAll<IProveedorDeIdentidad>();
                servicios.AddSingleton<IProveedorDeIdentidad>(Proveedor);
            });
            Operador = api.CrearClienteDe(_fabrica);
        }

        public ProveedorDeIdentidadFalso Proveedor { get; }

        public HttpClient Operador { get; }

        public void Dispose()
        {
            Operador.Dispose();
            _fabrica.Dispose();
        }
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorRespondeConError_Responde502YRegistraFallido()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var derivada = new ApiConProveedor(api, FallaDelProveedor.ConError);

        // Act
        var respuesta = await derivada.Operador.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.Equal("identidad-error", (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Single(await api.SolicitudesDeAsync(cliente.Id));
        Assert.Equal("Fallido", Assert.Single(await api.ResultadosDeAsync(cliente.Id)).Resultado);
        Assert.Null(await api.SubVinculadoAsync(cliente.Id));
    }

    [Fact]
    public async Task F003_ADR0017_ProveedorNoResponde_Responde503YDejaLaSolicitudPendiente()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var derivada = new ApiConProveedor(api, FallaDelProveedor.SinRespuesta);

        // Act
        var respuesta = await derivada.Operador.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("identidad-sin-respuesta", (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Single(await api.SolicitudesDeAsync(cliente.Id));
        Assert.Empty(await api.ResultadosDeAsync(cliente.Id));
    }

    [Fact]
    public async Task F003_ADR0017_ErrorDelProveedor_NoRevelaElMotivoTecnicoAlOperador()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var derivada = new ApiConProveedor(api, FallaDelProveedor.ConError);

        // Act
        var respuesta = await derivada.Operador.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert: el motivo ("HTTP 500 al ...") queda en el registro de accesos, no en la respuesta.
        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.DoesNotContain("HTTP 500", await respuesta.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task F003_ADR0017_ReintentoTrasUnaRespuestaPerdida_CierraLaAnteriorYResponde200()
    {
        // Arrange: el primer intento SE APLICO en el sistema de identidad pero el operador recibio 503.
        var cliente = await ClienteRegistradoAsync();
        using var derivada = new ApiConProveedor(api, FallaDelProveedor.AplicaYNoResponde);
        var primero = await derivada.Operador.PostAsync(Habilitar(cliente.Id), null, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, primero.StatusCode);
        derivada.Proveedor.Falla = FallaDelProveedor.Ninguna;

        // Act
        var reintento = await derivada.Operador.PostAsync(Habilitar(cliente.Id), null, Ct);

        // Assert: la anterior se cierra Completada por la marca y la nueva es un Restablecer del usuario ya creado.
        Assert.Equal(HttpStatusCode.OK, reintento.StatusCode);
        var acceso = await reintento.Content.ReadFromJsonAsync<AccesoHabilitadoDto>(Ct);
        Assert.Equal("Restablecer", acceso!.Accion);
        var solicitudes = await api.SolicitudesDeAsync(cliente.Id);
        Assert.Equal(2, solicitudes.Count);
        var resultados = await api.ResultadosDeAsync(cliente.Id);
        Assert.Equal(solicitudes.Select(s => s.Id), resultados.Select(r => r.SolicitudId));
        Assert.All(resultados, r => Assert.Equal("Completado", r.Resultado));
    }
}
