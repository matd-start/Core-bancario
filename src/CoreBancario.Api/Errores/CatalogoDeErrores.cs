using CoreBancario.Application.Clientes;
using CoreBancario.Application.Comun;
using CoreBancario.Domain;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Api.Errores;

public static class CatalogoDeErrores
{
    private const string TituloReglaDeNegocio = "Regla de negocio rota";

    /// <summary>Tabla "Catálogo de errores" del plan (ADR-0011). Una excepción no listada → 500 ErrorInesperado.</summary>
    public static ErrorHttp Clasificar(Exception excepcion)
    {
        ArgumentNullException.ThrowIfNull(excepcion);

        return excepcion switch
        {
            BadHttpRequestException => new ErrorHttp(StatusCodes.Status400BadRequest, CodigosDeError.DatosInvalidos, "Datos inválidos"),

            RecursoNoEncontradoException => new ErrorHttp(StatusCodes.Status404NotFound, CodigosDeError.NoEncontrado, "Recurso no encontrado"),

            DocumentoDuplicadoException => new ErrorHttp(StatusCodes.Status409Conflict, CodigosDeError.DocumentoDuplicado, "Conflicto"),
            ConflictoDeConcurrenciaException => new ErrorHttp(StatusCodes.Status409Conflict, CodigosDeError.ConflictoDeConcurrencia, "Conflicto"),

            TransicionNoPermitidaException => Regla(CodigosDeError.TransicionNoPermitida),
            SaldoDistintoDeCeroException => Regla(CodigosDeError.SaldoDistintoDeCero),
            OperacionNoPermitidaException => Regla(CodigosDeError.OperacionNoPermitida),
            SaldoInsuficienteException => Regla(CodigosDeError.SaldoInsuficiente),
            MontoNoPositivoException => Regla(CodigosDeError.MontoNoPositivo),
            MonedasDistintasException => Regla(CodigosDeError.MonedasDistintas),
            MontoNegativoException => Regla(CodigosDeError.MontoNegativo),
            PrecisionExcedidaException => Regla(CodigosDeError.PrecisionExcedida),

            // Respaldo: una prueba exige que ninguna regla del dominio caiga aquí, para que cada una tenga su código.
            ReglaDeNegocioException => Regla(CodigosDeError.ReglaDeNegocio),

            _ => new ErrorHttp(StatusCodes.Status500InternalServerError, CodigosDeError.ErrorInesperado, "Error inesperado"),
        };
    }

    private static ErrorHttp Regla(string codigo) =>
        new(StatusCodes.Status422UnprocessableEntity, codigo, TituloReglaDeNegocio);
}
