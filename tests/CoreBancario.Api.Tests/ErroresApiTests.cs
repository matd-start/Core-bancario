using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CoreBancario.Api.Errores;
using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CoreBancario.Api.Tests.Ayudas;

namespace CoreBancario.Api.Tests;

public class ErroresApiTests(ApiFixture api)
{
    private static Dinero Cop(decimal monto) => Dinero.Crear(monto, Moneda.COP);

    public static TheoryData<Exception, int, string> ExcepcionesConocidas => new()
    {
        { new RecursoNoEncontradoException("no existe"), 404, CodigosDeError.NoEncontrado },
        { new DocumentoDuplicadoException(), 409, CodigosDeError.DocumentoDuplicado },
        { new ConflictoDeConcurrenciaException(), 409, CodigosDeError.ConflictoDeConcurrencia },
        { new TransicionNoPermitidaException(EstadoCuenta.Bloqueada, EstadoCuenta.Cerrada), 422, CodigosDeError.TransicionNoPermitida },
        { new SaldoDistintoDeCeroException(Cop(1m)), 422, CodigosDeError.SaldoDistintoDeCero },
        { new OperacionNoPermitidaException(EstadoCuenta.Cerrada, TipoMovimiento.Credito), 422, CodigosDeError.OperacionNoPermitida },
        { new SaldoInsuficienteException(Cop(1m), Cop(2m)), 422, CodigosDeError.SaldoInsuficiente },
        { new MontoNoPositivoException(), 422, CodigosDeError.MontoNoPositivo },
        { new MonedasDistintasException(Moneda.COP, Moneda.USD), 422, CodigosDeError.MonedasDistintas },
        { new MontoNegativoException(-1m, Moneda.COP), 422, CodigosDeError.MontoNegativo },
        { new PrecisionExcedidaException(1.5m, Moneda.COP), 422, CodigosDeError.PrecisionExcedida },
        { new BadHttpRequestException("JSON mal formado"), 400, CodigosDeError.DatosInvalidos },
        { new InvalidOperationException("boom"), 500, CodigosDeError.ErrorInesperado },
    };

    [Theory]
    [MemberData(nameof(ExcepcionesConocidas))]
    public void F002_RNF03_CatalogoDeErrores_ClasificaCadaExcepcion(Exception excepcion, int estado, string codigo)
    {
        // Arrange / Act
        var error = CatalogoDeErrores.Clasificar(excepcion);

        // Assert
        Assert.Equal(estado, error.Estado);
        Assert.Equal(codigo, error.Codigo);
    }

    [Fact]
    public void F002_RNF03_ConflictoDeConcurrencia_SeClasificaComo409()
    {
        // Arrange
        var excepcion = new ConflictoDeConcurrenciaException();

        // Act
        var error = CatalogoDeErrores.Clasificar(excepcion);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, error.Estado);
        Assert.Equal(CodigosDeError.ConflictoDeConcurrencia, error.Codigo);
    }

    [Fact]
    public void F002_RNF03_CadaReglaDeNegocioDelDominio_TieneCodigoPropio()
    {
        // Arrange
        var reglas = typeof(ReglaDeNegocioException).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ReglaDeNegocioException)) && !t.IsAbstract)
            .ToList();

        // Act: se clasifica por tipo; no hace falta construir la excepcion con sus argumentos.
        var conCodigoDeRespaldo = reglas
            .Where(t => CatalogoDeErrores.Clasificar((Exception)RuntimeHelpers.GetUninitializedObject(t)).Codigo
                == CodigosDeError.ReglaDeNegocio)
            .Select(t => t.Name)
            .ToList();

        // Assert
        Assert.NotEmpty(reglas);
        Assert.Empty(conCodigoDeRespaldo);
    }

    [Fact]
    public async Task F002_RNF03_JsonMalFormado_Responde400DatosInvalidos()
    {
        // Arrange
        using var http = api.CrearCliente();
        using var contenido = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");

        // Act
        var respuesta = await http.PostAsync("/clientes", contenido, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(CodigosDeError.DatosInvalidos, (await LeerProblemaAsync(respuesta)).Codigo);
    }

    [Fact]
    public async Task F002_RNF04_ErrorInesperado_Responde500SinDetallesInternos()
    {
        // Arrange: el reloj lanza una excepcion con texto que parece SQL; nada de eso debe llegar al cliente.
        const string mensajeInterno = "SELECT * FROM clientes WHERE secreto = 1";
        using var fabrica = api.ConServicios(servicios =>
        {
            servicios.RemoveAll<TimeProvider>();
            servicios.AddSingleton<TimeProvider>(new RelojQueLanza(mensajeInterno));
        });
        using var http = fabrica.CreateClient();

        // Act
        var respuesta = await http.PostAsJsonAsync("/clientes", SolicitudDeRegistro(), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(Ct);
        Assert.Equal(CodigosDeError.ErrorInesperado, JsonDocument.Parse(cuerpo).RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain("SELECT", cuerpo);
        Assert.DoesNotContain("Exception", cuerpo);
        Assert.DoesNotContain("   at ", cuerpo);
        Assert.DoesNotContain(mensajeInterno, cuerpo);
    }

    [Theory]
    [InlineData("RegistrarCliente", "201,400,409")]
    [InlineData("ObtenerCliente", "200,404")]
    [InlineData("BuscarClientePorDocumento", "200,400,404")]
    [InlineData("AbrirCuenta", "201,400,404")]
    [InlineData("ObtenerCuenta", "200,404")]
    [InlineData("BloquearCuenta", "200,404,409,422")]
    [InlineData("DesbloquearCuenta", "200,404,409,422")]
    [InlineData("CerrarCuenta", "200,404,409,422")]
    public async Task F002_RNF05_ContratoOpenApi_DescribeCadaOperacionYSusErrores(string operationId, string codigosEsperados)
    {
        // Arrange
        using var http = api.CrearCliente();
        var respuesta = await http.GetAsync("/openapi/v1.json", Ct);
        respuesta.EnsureSuccessStatusCode();
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));

        // Act
        var operacion = documento.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(ruta => ruta.Value.EnumerateObject())
            .Select(metodo => metodo.Value)
            .Single(o => o.TryGetProperty("operationId", out var id) && id.GetString() == operationId);
        var codigos = operacion.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order().ToList();

        // Assert
        Assert.Equal(codigosEsperados.Split(','), codigos);
    }
}

/// <summary>Reloj que falla al leer la hora: simula un error inesperado dentro de un caso de uso.</summary>
internal sealed class RelojQueLanza(string mensaje) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException(mensaje);
}
