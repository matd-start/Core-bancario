using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using CoreBancario.Api.Errores;
using CoreBancario.Api.Cuentas;
using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using CoreBancario.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

public class CuentasApiTests(ApiFixture api)
{
    private async Task<(HttpClient Http, Guid ClienteId)> ClienteNuevoAsync()
    {
        var http = api.CrearCliente();
        var cliente = await RegistrarClienteAsync(http);
        return (http, cliente.Id);
    }

    private static async Task<List<CuentaDto>> AbrirVariasAsync(HttpClient http, Guid clienteId, int cantidad)
    {
        var cuentas = new List<CuentaDto>();
        for (var i = 0; i < cantidad; i++)
            cuentas.Add(await AbrirCuentaAsync(http, clienteId));
        return cuentas;
    }

    // ---- RF-02 Abrir cuenta ----

    [Fact]
    public async Task F002_CA06_AbrirCuentaCop_QuedaActivaConSaldoCeroFechaDeAperturaYNumeroValido()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;

        // Act
        var respuesta = await http.PostAsJsonAsync($"/clientes/{clienteId}/cuentas", new AbrirCuentaRequest("COP"), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuenta = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct);
        Assert.NotNull(cuenta);
        Assert.Equal(clienteId, cuenta.ClienteId);
        Assert.Equal("COP", cuenta.Moneda);
        Assert.Equal("Activa", cuenta.Estado);
        Assert.Equal(new DineroDto("0", "COP"), cuenta.Saldo);
        Assert.InRange(cuenta.FechaApertura, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Null(cuenta.FechaCierre);
        Assert.True(EsNumeroDeCuentaValido(cuenta.Numero), $"Numero invalido: {cuenta.Numero}");
        Assert.EndsWith($"/cuentas/{cuenta.Id}", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task F002_CA07_AbrirCuentaUsd_QuedaEnUsdConSaldoCero()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;

        // Act
        var respuesta = await http.PostAsJsonAsync($"/clientes/{clienteId}/cuentas", new AbrirCuentaRequest("USD"), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuenta = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct);
        Assert.Equal("USD", cuenta!.Moneda);
        Assert.Equal("0.00", cuenta.Saldo.Monto);
        Assert.Equal("USD", cuenta.Saldo.Moneda);
    }

    [Fact]
    public async Task F002_CA08_AbrirCuentaClienteInexistente_Responde404YNoCreaCuenta()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsJsonAsync($"/clientes/{Guid.NewGuid()}/cuentas", new AbrirCuentaRequest("COP"), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F002_CA09_AbrirCincuentaCuentas_NumerosDistintosValidosYNoConsecutivos()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuentas = await AbrirVariasAsync(http, clienteId, 50);

        // Act
        var numeros = cuentas.Select(c => c.Numero).ToList();
        var consecutivos = numeros.Zip(numeros.Skip(1),
            (anterior, siguiente) => long.Parse(siguiente, CultureInfo.InvariantCulture)
                == long.Parse(anterior, CultureInfo.InvariantCulture) + 1);

        // Assert
        Assert.Equal(50, numeros.Distinct().Count());
        Assert.All(numeros, numero => Assert.True(EsNumeroDeCuentaValido(numero), $"Numero invalido: {numero}"));
        Assert.DoesNotContain(true, consecutivos);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("cop")]
    [InlineData("")]
    public async Task F002_CL10_AbrirCuentaEnMonedaDesconocida_Responde400(string moneda)
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;

        // Act
        var respuesta = await http.PostAsJsonAsync($"/clientes/{clienteId}/cuentas", new AbrirCuentaRequest(moneda), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await LeerProblemaAsync(respuesta);
        Assert.Equal(CodigosDeError.DatosInvalidos, problema.Codigo);
        Assert.Equal(["moneda"], problema.Errors!.Keys);
    }

    [Fact]
    public async Task F002_CL10_AbrirCuentaSinMoneda_Responde400()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;

        // Act
        var respuesta = await http.PostAsJsonAsync($"/clientes/{clienteId}/cuentas", new AbrirCuentaRequest(null), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("moneda", (await LeerProblemaAsync(respuesta)).Errors!.Keys);
    }

    [Fact]
    public async Task F002_CL11_GeneradorDevuelveUnNumeroExistente_AbreConOtroNumero()
    {
        // Arrange: secuencia A, A, B. La primera cuenta toma A; la segunda recibe A (ya existe) y luego B.
        var numeroA = NumeroDeCuenta.Crear(NumeroDeCuentaUnico());
        var numeroB = NumeroDeCuenta.Crear(NumeroDeCuentaUnico());
        var generador = new GeneradorDeSecuencia(numeroA, numeroA, numeroB);
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<IGeneradorDeNumeroDeCuenta>();
            servicios.AddSingleton<IGeneradorDeNumeroDeCuenta>(generador);
        });
        using var http = fabrica.CreateClient();
        var cliente = await RegistrarClienteAsync(http);
        var primera = await AbrirCuentaAsync(http, cliente.Id);

        // Act
        var segunda = await AbrirCuentaAsync(http, cliente.Id);

        // Assert
        Assert.Equal(numeroA.Valor, primera.Numero);
        Assert.Equal(numeroB.Valor, segunda.Numero);
    }

    [Fact]
    public async Task F002_CL12_ReintentarApertura_AbreUnaSegundaCuenta()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var primera = await AbrirCuentaAsync(http, clienteId);

        // Act: limite conocido y aceptado, sin idempotencia hasta el S4.
        var segunda = await AbrirCuentaAsync(http, clienteId);

        // Assert
        Assert.NotEqual(primera.Id, segunda.Id);
        Assert.NotEqual(primera.Numero, segunda.Numero);
    }

    // ---- RF-03 Cambiar estado ----

    [Fact]
    public async Task F002_CA10_BloquearYDesbloquear_CambiaElEstadoYSePersiste()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuenta = await AbrirCuentaAsync(http, clienteId);

        // Act
        var bloqueo = await http.PostAsync($"/cuentas/{cuenta.Id}/bloquear", null, Ct);
        var tras = await ConsultarCuentaAsync(http, cuenta.Id);
        var desbloqueo = await http.PostAsync($"/cuentas/{cuenta.Id}/desbloquear", null, Ct);
        var final = await ConsultarCuentaAsync(http, cuenta.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, bloqueo.StatusCode);
        Assert.Equal("Bloqueada", (await bloqueo.Content.ReadFromJsonAsync<CuentaDto>(Ct))!.Estado);
        Assert.Equal("Bloqueada", tras.Estado);
        Assert.Equal(HttpStatusCode.OK, desbloqueo.StatusCode);
        Assert.Equal("Activa", final.Estado);
    }

    [Theory]
    [InlineData("cerrar")]
    [InlineData("bloquear")]
    public async Task F002_CA11_CerrarOBloquearCuentaBloqueada_Responde422YSigueBloqueada(string accion)
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuenta = await AbrirCuentaAsync(http, clienteId);
        await http.PostAsync($"/cuentas/{cuenta.Id}/bloquear", null, Ct);

        // Act
        var respuesta = await http.PostAsync($"/cuentas/{cuenta.Id}/{accion}", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.TransicionNoPermitida, (await LeerProblemaAsync(respuesta)).Codigo);
        Assert.Equal("Bloqueada", (await ConsultarCuentaAsync(http, cuenta.Id)).Estado);
    }

    [Fact]
    public async Task F002_CA12_CerrarCuentaActivaSinSaldo_QuedaCerradaConFechaDeCierre()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuenta = await AbrirCuentaAsync(http, clienteId);

        // Act
        var respuesta = await http.PostAsync($"/cuentas/{cuenta.Id}/cerrar", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cerrada = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct);
        Assert.Equal("Cerrada", cerrada!.Estado);
        Assert.NotNull(cerrada.FechaCierre);
        Assert.InRange(cerrada.FechaCierre.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        var persistida = await ConsultarCuentaAsync(http, cuenta.Id);
        Assert.Equal("Cerrada", persistida.Estado);
        Assert.NotNull(persistida.FechaCierre);
    }

    [Theory]
    [InlineData("bloquear")]
    [InlineData("desbloquear")]
    [InlineData("cerrar")]
    public async Task F002_CA12_CambiarEstadoDeCuentaCerrada_Responde422(string accion)
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuenta = await AbrirCuentaAsync(http, clienteId);
        await http.PostAsync($"/cuentas/{cuenta.Id}/cerrar", null, Ct);

        // Act
        var respuesta = await http.PostAsync($"/cuentas/{cuenta.Id}/{accion}", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.TransicionNoPermitida, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F002_CA13_CerrarCuentaConSaldo_Responde422YSigueActivaSinFechaDeCierre()
    {
        // Arrange: sin RF-04 todavia, la cuenta con saldo se prepara con el dominio y se guarda directamente.
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var cuenta = Cuenta.Abrir(NumeroDeCuenta.Crear(NumeroDeCuentaUnico()), clienteId, Moneda.COP, DateTimeOffset.UtcNow);
        cuenta.Acreditar(Dinero.Crear(1_000m, Moneda.COP), DateTimeOffset.UtcNow);
        using (var alcance = api.Factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<CoreBancarioDbContext>();
            db.Cuentas.Add(cuenta);
            await db.SaveChangesAsync(Ct);
        }

        // Act
        var respuesta = await http.PostAsync($"/cuentas/{cuenta.Id}/cerrar", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.SaldoDistintoDeCero, (await LeerProblemaAsync(respuesta)).Codigo);
        var persistida = await ConsultarCuentaAsync(http, cuenta.Id);
        Assert.Equal("Activa", persistida.Estado);
        Assert.Null(persistida.FechaCierre);
        Assert.Equal("1000", persistida.Saldo.Monto);
    }

    [Theory]
    [InlineData("bloquear")]
    [InlineData("desbloquear")]
    [InlineData("cerrar")]
    public async Task F002_CL16_CambiarEstadoDeCuentaInexistente_Responde404(string accion)
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.PostAsync($"/cuentas/{Guid.NewGuid()}/{accion}", null, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    // ---- RF-05 Consultar cuenta ----

    [Fact]
    public async Task F002_CA17_ObtenerCuentaExistente_DevuelveNumeroMonedaEstadoSaldoYFechas()
    {
        // Arrange
        var (http, clienteId) = await ClienteNuevoAsync();
        using var _ = http;
        var abierta = await AbrirCuentaAsync(http, clienteId, "USD");

        // Act
        var respuesta = await http.GetAsync($"/cuentas/{abierta.Id}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuenta = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(Ct);
        Assert.Equal(abierta.Id, cuenta!.Id);
        Assert.Equal(abierta.Numero, cuenta.Numero);
        Assert.Equal("USD", cuenta.Moneda);
        Assert.Equal("Activa", cuenta.Estado);
        Assert.Equal("0.00", cuenta.Saldo.Monto);
        Assert.Equal(abierta.FechaApertura, cuenta.FechaApertura);
        Assert.Null(cuenta.FechaCierre);
    }

    [Fact]
    public async Task F002_CA17_ObtenerCuentaInexistente_Responde404()
    {
        // Arrange
        using var http = api.CrearCliente();

        // Act
        var respuesta = await http.GetAsync($"/cuentas/{Guid.NewGuid()}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.NoEncontrado, (await LeerProblemaAsync(respuesta)).Codigo);
    }
}

/// <summary>Generador falso: devuelve los numeros en orden y repite el ultimo al agotarlos.</summary>
internal sealed class GeneradorDeSecuencia(params NumeroDeCuenta[] numeros) : IGeneradorDeNumeroDeCuenta
{
    private int _siguiente;

    public NumeroDeCuenta Generar() => numeros[Math.Min(Interlocked.Increment(ref _siguiente) - 1, numeros.Length - 1)];
}
