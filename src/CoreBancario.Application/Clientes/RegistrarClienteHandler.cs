using CoreBancario.Application.Comun;
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Application.Clientes;

public sealed class RegistrarClienteHandler(IClienteRepositorio clientes, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>
    /// 1) Valida TODOS los campos y acumula errores; si hay alguno, devuelve Invalido sin tocar repositorio ni
    ///    unidad de trabajo. 2) Cliente.Registrar(…, reloj.AhoraEnMicrosegundos()). 3) Agregar + GuardarCambiosAsync.
    /// Sin consulta previa de existencia: el duplicado lo detecta el índice único de la base.
    /// </summary>
    /// <exception cref="DocumentoDuplicadoException">CL-01, CL-03, CL-08.</exception>
    public async Task<Resultado<ClienteDto>> EjecutarAsync(RegistrarClienteComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var errores = new Dictionary<string, string[]>();

        var documento = ValidarDocumento(comando, errores);
        var nombres = ValidarNombre(comando.Nombres, "nombres", errores);
        var apellidos = ValidarNombre(comando.Apellidos, "apellidos", errores);

        if (!Correo.TryCrear(comando.Correo, out var correo))
            errores["correo"] = ["Debe tener la forma usuario@dominio (máximo 254 caracteres)."];

        if (!Telefono.TryCrear(comando.Telefono, out var telefono))
            errores["telefono"] = ["Debe estar en formato internacional: + y de 8 a 15 dígitos con el código de país."];

        // Cada value object es null solo si su campo falló. El compilador no lo deduce del diccionario,
        // así que la condición de salida se escribe sobre los propios objetos.
        if (documento is null || nombres is null || apellidos is null || correo is null || telefono is null)
            return Resultado<ClienteDto>.Invalido(errores);

        var cliente = Cliente.Registrar(documento, nombres, apellidos, correo, telefono, reloj.AhoraEnMicrosegundos());

        clientes.Agregar(cliente);
        await unidadDeTrabajo.GuardarCambiosAsync(cancellationToken);

        return Resultado<ClienteDto>.Exito(ClienteDto.Desde(cliente));
    }

    private static Documento? ValidarDocumento(RegistrarClienteComando comando, Dictionary<string, string[]> errores)
    {
        var tipoValido = TiposDeDocumentoAceptados.TryInterpretar(comando.TipoDocumento, out var tipo);
        if (!tipoValido)
            errores["tipoDocumento"] = [TiposDeDocumentoAceptados.MensajeDeError];

        if (string.IsNullOrWhiteSpace(comando.NumeroDocumento))
        {
            errores["numeroDocumento"] = ["El número de documento es obligatorio."];
            return null;
        }

        // Si el tipo es inválido, el número no se evalúa: su formato depende del tipo.
        if (!tipoValido)
            return null;

        if (Documento.TryCrear(tipo, comando.NumeroDocumento, out var documento))
            return documento;

        errores["numeroDocumento"] = [$"El número no cumple el formato de {tipo}."];
        return null;
    }

    private static NombreDePersona? ValidarNombre(string? valor, string campo, Dictionary<string, string[]> errores)
    {
        if (NombreDePersona.TryCrear(valor, out var nombre))
            return nombre;

        errores[campo] = ["Debe tener de 1 a 100 letras, espacios, apóstrofos o guiones."];
        return null;
    }
}
