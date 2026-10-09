using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Application.Tests.Fakes;

namespace CoreBancario.Application.Tests.Cuentas;

public class ObtenerCuentaHandlerTests
{
    private readonly CuentaRepositorioEnMemoria _cuentas = new();

    [Fact]
    public async Task F002_CA17_CuentaExistente_DevuelveNumeroMonedaEstadoSaldoYFechas()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        var cuenta = Datos.Cuenta(clienteId);
        _cuentas.Agregar(cuenta);
        var handler = new ObtenerCuentaHandler(_cuentas);

        // Act
        var dto = await handler.EjecutarAsync(new ObtenerCuentaConsulta(cuenta.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(cuenta.Id, dto.Id);
        Assert.Equal("1234567897", dto.Numero);
        Assert.Equal(clienteId, dto.ClienteId);
        Assert.Equal("COP", dto.Moneda);
        Assert.Equal("Activa", dto.Estado);
        Assert.Equal(new DineroDto("0", "COP"), dto.Saldo);
        Assert.Equal(Datos.Ahora, dto.FechaApertura);
        Assert.Null(dto.FechaCierre);
    }

    [Fact]
    public async Task F002_CL18_CuentaInexistente_LanzaRecursoNoEncontrado()
    {
        // Arrange
        var handler = new ObtenerCuentaHandler(_cuentas);

        // Act
        var accion = () => handler.EjecutarAsync(new ObtenerCuentaConsulta(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(accion);
    }
}
