using CoreBancario.Api.Errores;
using CoreBancario.Application.AccesosDigitales;
using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Api.Tests;

/// <summary>
/// Catálogo de errores de la 003b (se añade a la tabla de ADR-0011): 422 para el acceso no habilitado, y 502 / 503 para
/// los dos modos de falla del sistema de identidad (ADR-0017). Las pruebas comprueban estado y código, no textos.
/// </summary>
public class CatalogoDeErroresDeAccesoTests
{
    public static TheoryData<string, int, string> ErroresNuevos => new()
    {
        { nameof(AccesoNoHabilitadoException), 422, "acceso-no-habilitado" },
        { nameof(ErrorDelProveedorDeIdentidadException), 502, "identidad-error" },
        { nameof(ProveedorDeIdentidadSinRespuestaException), 503, "identidad-sin-respuesta" },
    };

    private static Exception Crear(string tipo) =>
        tipo switch
        {
            nameof(AccesoNoHabilitadoException) => new AccesoNoHabilitadoException(EstadoDeAcceso.Deshabilitado),
            nameof(ErrorDelProveedorDeIdentidadException) => new ErrorDelProveedorDeIdentidadException("HTTP 500 al restablecer la clave"),
            nameof(ProveedorDeIdentidadSinRespuestaException) => new ProveedorDeIdentidadSinRespuestaException(),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de excepción desconocido."),
        };

    [Theory]
    [MemberData(nameof(ErroresNuevos))]
    public void F003_RNF02_CatalogoDeErrores_ClasificaLosErroresNuevos(string tipo, int estado, string codigo)
    {
        // Arrange
        var excepcion = Crear(tipo);

        // Act
        var error = CatalogoDeErrores.Clasificar(excepcion);

        // Assert
        Assert.Equal(estado, error.Estado);
        Assert.Equal(codigo, error.Codigo);
    }
}
