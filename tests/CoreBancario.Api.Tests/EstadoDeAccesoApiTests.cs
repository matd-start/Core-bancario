using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Errores;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Application.Clientes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

/// <summary>
/// RF-16, CA-29, CL-20: GET /clientes/{id}/acceso-digital contra la API real y PostgreSQL, con el sistema de identidad
/// falso (ADR-0018). Es una consulta: el estado sale de Keycloak y no se escribe nada.
/// </summary>
public class EstadoDeAccesoApiTests(ApiFixture api)
{
    private static string Estado(Guid clienteId) => $"/clientes/{clienteId}/acceso-digital";

    private async Task<ClienteDto> ClienteRegistradoAsync()
    {
        using var operador = api.CrearCliente();
        return await RegistrarClienteAsync(operador);
    }

    private async Task HabilitarAsync(ClienteDto cliente)
    {
        using var operador = api.CrearCliente();
        var respuesta = await operador.PostAsync($"/clientes/{cliente.Id}/acceso-digital/habilitar", null, Ct);
        respuesta.EnsureSuccessStatusCode();
    }

    private static string NombreDeUsuarioDe(ClienteDto cliente) => $"{cliente.TipoDocumento}{cliente.NumeroDocumento}";

    /// <summary>Consulta el estado de un cliente registrado con un sistema de identidad que falla al LEER así.</summary>
    private async Task<HttpResponseMessage> ConsultarConFallaAlLeerAsync(FallaDelProveedor falla)
    {
        var cliente = await ClienteRegistradoAsync();
        var proveedor = new ProveedorDeIdentidadFalso { FallaAlLeer = falla };
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<IProveedorDeIdentidad>();
            servicios.AddSingleton<IProveedorDeIdentidad>(proveedor);
        });
        using var http = api.CrearClienteDe(fabrica);
        return await http.GetAsync(Estado(cliente.Id), Ct);
    }

    // ---- CA-29: el operador consulta el estado ----

    [Fact]
    public async Task F004_CA29_OperadorConsultaClienteSinAcceso_Responde200SinAcceso()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<EstadoDeAccesoDto>(Ct);
        Assert.Equal(new EstadoDeAccesoDto(cliente.Id, "SinAcceso", null), estado);
    }

    [Fact]
    public async Task F004_CA29_OperadorConsultaClienteHabilitadoSinEntrar_Responde200ConPrimerIngresoPendiente()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        await HabilitarAsync(cliente);
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<EstadoDeAccesoDto>(Ct);
        Assert.Equal(new EstadoDeAccesoDto(cliente.Id, "Habilitado", true), estado);
    }

    [Fact]
    public async Task F004_RF16_ClienteHabilitadoQueYaCompletoElPrimerIngreso_PrimerIngresoPendienteFalse()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        await HabilitarAsync(cliente);
        api.Proveedor.PorNombre(NombreDeUsuarioDe(cliente))!.PrimerIngresoPendiente = false;
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        var estado = await respuesta.Content.ReadFromJsonAsync<EstadoDeAccesoDto>(Ct);
        Assert.Equal(new EstadoDeAccesoDto(cliente.Id, "Habilitado", false), estado);
    }

    [Fact]
    public async Task F004_CA29_OperadorConsultaClienteDeshabilitado_Responde200Deshabilitado()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        await HabilitarAsync(cliente);
        using var http = api.CrearCliente();
        var deshabilitar = await http.PostAsync($"/clientes/{cliente.Id}/acceso-digital/deshabilitar", null, Ct);
        deshabilitar.EnsureSuccessStatusCode();

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        var estado = await respuesta.Content.ReadFromJsonAsync<EstadoDeAccesoDto>(Ct);
        Assert.Equal(new EstadoDeAccesoDto(cliente.Id, "Deshabilitado", null), estado);
    }

    [Fact]
    public async Task F004_RF16_ConsultarElEstado_NoRegistraSolicitudesNiVinculos()
    {
        // Arrange
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(await api.SolicitudesDeAsync(cliente.Id));
        Assert.Null(await api.SubVinculadoAsync(cliente.Id));
    }

    // ---- CA-29: cliente inexistente y rol cliente ----

    [Fact]
    public async Task F004_CA29_ClienteInexistente_Responde404ConSuCodigo()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync(Estado(Guid.NewGuid()), Ct);

        // Assert: el código distingue este 404 del de una ruta que no existe (que no trae cuerpo).
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(CodigosDeError.NoEncontrado, problema.Codigo);
    }

    [Fact]
    public async Task F004_CA29_RolCliente_Responde403()
    {
        // Arrange: el cliente existe; aun así, el rol Cliente no consulta accesos de otros.
        var cliente = await ClienteRegistradoAsync();
        using var http = api.CrearClienteComoCliente(Guid.NewGuid().ToString());

        // Act
        var respuesta = await http.GetAsync(Estado(cliente.Id), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.Prohibido, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    // ---- CL-20: el sistema de identidad no está disponible ----

    [Fact]
    public async Task F004_CL20_IdentidadSinRespuesta_EstadoResponde503()
    {
        // Arrange / Act
        var respuesta = await ConsultarConFallaAlLeerAsync(FallaDelProveedor.SinRespuesta);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.IdentidadSinRespuesta, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F004_CL20_ErrorDeIdentidad_EstadoResponde502()
    {
        // Arrange / Act
        var respuesta = await ConsultarConFallaAlLeerAsync(FallaDelProveedor.ConError);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.IdentidadError, (await LeerProblemaAsync(respuesta)).Codigo);
    }
}
