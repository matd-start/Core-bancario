using CoreBancario.Application.Comun;
using CoreBancario.Application.Cuentas;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

public sealed class BuscarClientePorDocumentoHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas)
{
    /// <summary>
    /// Invalido si tipoDocumento falta o no es CC/CE/PA, o si numeroDocumento falta o queda vacío al normalizar.
    /// Si el número no cumple el formato de su tipo → RecursoNoEncontradoException (no puede existir un cliente así).
    /// </summary>
    /// <exception cref="RecursoNoEncontradoException">Documento no registrado (CL-18).</exception>
    public async Task<Resultado<FichaClienteDto>> EjecutarAsync(
        BuscarClientePorDocumentoConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var errores = new Dictionary<string, string[]>();

        if (!TiposDeDocumentoAceptados.TryInterpretar(consulta.TipoDocumento, out var tipo))
            errores["tipoDocumento"] = [TiposDeDocumentoAceptados.MensajeDeError];

        if (TiposDeDocumentoAceptados.EstaVacioAlNormalizar(consulta.NumeroDocumento))
            errores["numeroDocumento"] = ["El número de documento es obligatorio."];

        if (errores.Count > 0)
            return Resultado<FichaClienteDto>.Invalido(errores);

        if (!Documento.TryCrear(tipo, consulta.NumeroDocumento, out var documento))
            throw new RecursoNoEncontradoException("No existe un cliente con ese documento.");

        var cliente = await clientes.ObtenerPorDocumentoAsync(documento, cancellationToken)
            ?? throw new RecursoNoEncontradoException("No existe un cliente con ese documento.");

        var ficha = await FichaClienteDtoFabrica.CrearAsync(cliente, cuentas, cancellationToken);
        return Resultado<FichaClienteDto>.Exito(ficha);
    }
}
