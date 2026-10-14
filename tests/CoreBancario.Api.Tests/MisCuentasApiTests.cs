using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Errores;
using CoreBancario.Application.Cuentas;
using static CoreBancario.Api.Tests.Ayudas;
using static CoreBancario.Api.Tests.AyudasDeAutenticacion;

namespace CoreBancario.Api.Tests;

/// <summary>
/// "Mis cuentas" (RF-05 del cliente, RN-12, ADR-0016): el cliente se identifica por el sub de su token, nunca por la
/// URL, y una cuenta ajena responde igual que una inexistente. Los datos los prepara un operador por HTTP; el vinculo
/// sub - cliente se inserta directamente en la base (el endpoint de habilitar llega en la 003b).
/// </summary>
public class MisCuentasApiTests(ApiFixture api)
{
    private sealed record Titular(Guid ClienteId, string Sub, List<CuentaDto> Cuentas);

    /// <summary>Registra un cliente con una cuenta por cada moneda indicada y lo vincula a un sub nuevo.</summary>
    private async Task<Titular> TitularConCuentasAsync(params string[] monedas)
    {
        using var operador = api.CrearCliente();
        var cliente = await RegistrarClienteAsync(operador);
        var cuentas = new List<CuentaDto>();
        foreach (var moneda in monedas)
            cuentas.Add(await AbrirCuentaAsync(operador, cliente.Id, moneda));

        var sub = $"cliente-{Guid.NewGuid()}";
        await api.VincularAsync(cliente.Id, sub);
        return new Titular(cliente.Id, sub, cuentas);
    }

    // ---- CA-14 / CL-18: activa y cerrada, y ninguna ajena ----

    [Fact]
    public async Task F003_CA14_MisCuentas_DevuelveActivaYCerradaSinCuentasAjenas()
    {
        // Arrange
        var titular = await TitularConCuentasAsync("COP", "USD");
        var ajeno = await TitularConCuentasAsync("COP");
        var (activa, aCerrar) = (titular.Cuentas[0], titular.Cuentas[1]);
        using (var operador = api.CrearCliente())
            (await operador.PostAsync($"/cuentas/{aCerrar.Id}/cerrar", null, Ct)).EnsureSuccessStatusCode();
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuentas = await respuesta.Content.ReadFromJsonAsync<List<CuentaDto>>(Ct);
        Assert.NotNull(cuentas);
        Assert.Equal([activa.Id, aCerrar.Id], cuentas.Select(c => c.Id));
        Assert.Equal([activa.Numero, aCerrar.Numero], cuentas.Select(c => c.Numero));
        Assert.Equal(["COP", "USD"], cuentas.Select(c => c.Moneda));
        Assert.Equal(["Activa", "Cerrada"], cuentas.Select(c => c.Estado));
        Assert.Equal([activa.Saldo, aCerrar.Saldo], cuentas.Select(c => c.Saldo));
        Assert.DoesNotContain(cuentas, c => c.ClienteId != titular.ClienteId);
        Assert.DoesNotContain(cuentas, c => c.Id == ajeno.Cuentas[0].Id);
    }

    [Fact]
    public async Task F003_CA14_MisCuentas_ListaPorFechaDeAperturaAscendente()
    {
        // Arrange
        var titular = await TitularConCuentasAsync("COP", "USD", "COP");
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var cuentas = await http.GetFromJsonAsync<List<CuentaDto>>("/mis-cuentas", Ct);

        // Assert
        Assert.NotNull(cuentas);
        Assert.Equal(titular.Cuentas.Select(c => c.Id), cuentas.Select(c => c.Id));
    }

    // ---- CA-17 / CL-18: lista vacia ----

    [Fact]
    public async Task F003_CA17_ClienteSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        var titular = await TitularConCuentasAsync();
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<List<CuentaDto>>(Ct))!);
    }

    [Fact]
    public async Task F003_CA17_SubSinVinculoConUnCliente_DevuelveListaVacia()
    {
        // Arrange: un token valido de un usuario que Keycloak conoce pero que ningun cliente del banco tiene vinculado.
        using var http = api.CrearClienteComoCliente($"sin-vinculo-{Guid.NewGuid()}");

        // Act
        var respuesta = await http.GetAsync("/mis-cuentas", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<List<CuentaDto>>(Ct))!);
    }

    // ---- CA-16 / RF-05: detalle de una cuenta propia ----

    [Fact]
    public async Task F003_RF05_MiCuenta_DevuelveNumeroMonedaEstadoSaldoYFechas()
    {
        // Arrange
        var titular = await TitularConCuentasAsync("USD");
        var cuenta = titular.Cuentas[0];
        using var operador = api.CrearCliente();
        var esperada = await ConsultarCuentaAsync(operador, cuenta.Id);
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var respuesta = await http.GetAsync($"/mis-cuentas/{cuenta.Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct);
        Assert.NotNull(detalle);
        Assert.Equal(esperada, detalle);
        Assert.Equal(cuenta.Numero, detalle.Numero);
        Assert.Equal("USD", detalle.Moneda);
        Assert.Equal("Activa", detalle.Estado);
    }

    // ---- CA-15 / CL-17: ajena = inexistente ----

    [Fact]
    public async Task F003_CL17_CuentaAjenaEInexistente_RespuestasIdenticasSalvoTraceId()
    {
        // Arrange
        var titular = await TitularConCuentasAsync("COP");
        var ajeno = await TitularConCuentasAsync("COP");
        var idAjeno = ajeno.Cuentas[0].Id.ToString();
        var idInexistente = Guid.NewGuid().ToString();
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var porAjena = await http.GetAsync($"/mis-cuentas/{idAjeno}", Ct);
        var porInexistente = await http.GetAsync($"/mis-cuentas/{idInexistente}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, porAjena.StatusCode);
        Assert.Equal("application/problem+json", porAjena.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(porAjena)).Codigo);
        Assert.Equal(
            await HuellaDeLaRespuestaAsync(porInexistente, idInexistente),
            await HuellaDeLaRespuestaAsync(porAjena, idAjeno));
    }

    [Fact]
    public async Task F003_RN12_ClienteLeeCuentaAjenaPorLaRutaDelOperador_Responde403()
    {
        // Arrange: /cuentas/{id} es del operador (RN-19); el cliente no puede usarla para espiar cuentas.
        var titular = await TitularConCuentasAsync("COP");
        var ajeno = await TitularConCuentasAsync("COP");
        using var http = api.CrearClienteComoCliente(titular.Sub);

        // Act
        var respuesta = await http.GetAsync($"/cuentas/{ajeno.Cuentas[0].Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
