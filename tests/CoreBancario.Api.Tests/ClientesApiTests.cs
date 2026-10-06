using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Errores;
using CoreBancario.Application.Clientes;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

public class ClientesApiTests(ApiFixture api)
{
    // ---- RF-01 Registrar cliente ----

    [Fact]
    public async Task CA01_RegistrarClienteValido_Responde201ConIdFechaYDatosNormalizados()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var solicitud = SolicitudDeRegistro("CC", ConPuntos(numero));

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", solicitud, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cliente = await respuesta.Content.ReadFromJsonAsync<ClienteDto>(Ct);
        Assert.NotNull(cliente);
        Assert.NotEqual(Guid.Empty, cliente.Id);
        Assert.Equal("CC", cliente.TipoDocumento);
        Assert.Equal(numero, cliente.NumeroDocumento);
        Assert.Equal("+573001234567", cliente.Telefono);
        Assert.Equal("María José", cliente.Nombres);
        Assert.InRange(cliente.FechaRegistro, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.EndsWith($"/clientes/{cliente.Id}", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CA01_ClienteRegistrado_SeConsultaPorIdConLosMismosDatos()
    {
        // Arrange
        using var http = api.CrearCliente();
        var registrado = await RegistrarClienteAsync(http);

        // Act
        var respuesta = await http.GetAsync($"/clientes/{registrado.Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ficha = await respuesta.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Equal(registrado.Id, ficha!.Cliente.Id);
        Assert.Equal(registrado.NumeroDocumento, ficha.Cliente.NumeroDocumento);
        Assert.Equal(registrado.Correo, ficha.Cliente.Correo);
        Assert.Equal(registrado.FechaRegistro, ficha.Cliente.FechaRegistro);
    }

    [Fact]
    public async Task CA02_RegistrarMismoDocumentoConPuntos_RespondeDuplicadoYQuedaUnSoloCliente()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var primero = await RegistrarClienteAsync(http, "CC", numero);

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro("CC", ConPuntos(numero)), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.DocumentoDuplicado, (await LeerProblemaAsync(respuesta)).Codigo);
        var busqueda = await BuscarPorDocumentoAsync(http, "CC", numero);
        var ficha = await busqueda.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Equal(primero.Id, ficha!.Cliente.Id);
    }

    [Fact]
    public async Task CA03_RegistrarMismoNumeroComoPasaporte_SeAcepta()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        await RegistrarClienteAsync(http, "CC", numero);

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro("PA", numero), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task CA04_RegistroConTresCamposInvalidos_Responde400ConLosTresErrores()
    {
        // Arrange
        using var http = api.CrearCliente();
        var solicitud = SolicitudDeRegistro() with { NumeroDocumento = "12AB", Correo = "no-es-correo", Telefono = "3001234567" };

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", solicitud, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(CodigosDeError.DatosInvalidos, problema.Codigo);
        Assert.Equal(["correo", "numeroDocumento", "telefono"], problema.Errors!.Keys.Order());
    }

    [Fact]
    public async Task CA05_DosRegistrosSimultaneos_UnoCreaYOtroRespondeDuplicado()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var solicitud = SolicitudDeRegistro("CC", numero);

        // Act
        var respuestas = await Task.WhenAll(
            http.PostAsJsonAsync("/clientes", solicitud, Ct),
            http.PostAsJsonAsync("/clientes", solicitud, Ct));

        // Assert
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            respuestas.Select(r => r.StatusCode).Order());
        var rechazada = respuestas.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(CodigosDeError.DocumentoDuplicado, (await LeerProblemaAsync(rechazada)).Codigo);
    }

    [Fact]
    public async Task CA05_DosRegistrosSimultaneos_QuedaExactamenteUnClienteConEseDocumento()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var solicitud = SolicitudDeRegistro("CC", numero);
        var respuestas = await Task.WhenAll(
            http.PostAsJsonAsync("/clientes", solicitud, Ct),
            http.PostAsJsonAsync("/clientes", solicitud, Ct));
        var creado = await respuestas.Single(r => r.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<ClienteDto>(Ct);

        // Act
        var busqueda = await BuscarPorDocumentoAsync(http, "CC", numero);

        // Assert
        Assert.Equal(HttpStatusCode.OK, busqueda.StatusCode);
        var ficha = await busqueda.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Equal(creado!.Id, ficha!.Cliente.Id);
    }

    [Fact]
    public async Task CL08_ReintentarRegistroGuardado_RespondeDuplicadoYSeEncuentraPorDocumento()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var guardado = await RegistrarClienteAsync(http, "CC", numero);

        // Act
        var reintento = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro("CC", numero), Ct);
        var busqueda = await BuscarPorDocumentoAsync(http, "CC", numero);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, reintento.StatusCode);
        Assert.Equal(HttpStatusCode.OK, busqueda.StatusCode);
        var ficha = await busqueda.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Equal(guardado.Id, ficha!.Cliente.Id);
    }

    // ---- RF-12 Consultar clientes ----

    [Fact]
    public async Task CA15_BuscarConPuntos_DevuelveLaFichaConSusCuentas()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        var cliente = await RegistrarClienteAsync(http, "CC", numero);
        var cuenta = await AbrirCuentaAsync(http, cliente.Id, "USD");

        // Act
        var respuesta = await BuscarPorDocumentoAsync(http, "CC", ConPuntos(numero));

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ficha = await respuesta.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Equal(cliente.Id, ficha!.Cliente.Id);
        var encontrada = Assert.Single(ficha.Cuentas);
        Assert.Equal(cuenta.Numero, encontrada.Numero);
        Assert.Equal("USD", encontrada.Moneda);
        Assert.Equal("Activa", encontrada.Estado);
        Assert.Equal("0.00", encontrada.Saldo.Monto);
    }

    [Fact]
    public async Task CA16_BuscarDocumentoNoRegistrado_Responde404()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await BuscarPorDocumentoAsync(http, "CC", NumeroDeDocumentoUnico());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task CA16_ClienteSinCuentas_DevuelveListaVacia()
    {
        // Arrange
        using var http = api.CrearCliente();
        var numero = NumeroDeDocumentoUnico();
        await RegistrarClienteAsync(http, "CC", numero);

        // Act
        var respuesta = await BuscarPorDocumentoAsync(http, "CC", numero);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ficha = await respuesta.Content.ReadFromJsonAsync<FichaClienteDto>(Ct);
        Assert.Empty(ficha!.Cuentas);
    }

    [Fact]
    public async Task CL18_ObtenerClienteInexistente_Responde404()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync($"/clientes/{Guid.NewGuid()}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task CL18_BuscarConTipoInvalido_Responde400()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await BuscarPorDocumentoAsync(http, "XX", NumeroDeDocumentoUnico());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(CodigosDeError.DatosInvalidos, problema.Codigo);
        Assert.Contains("tipoDocumento", problema.Errors!.Keys);
    }
}
