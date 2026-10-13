// SDD: esqueleto creado por test-writer
using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.AccesosDigitales;

/// <summary>
/// Vínculo entre un cliente y su usuario en el sistema de identidad (ADR-0016). El estado del acceso (habilitado,
/// segundo factor) NO vive aquí, sino en el sistema de identidad (ADR-0017). Un cliente tiene como máximo un vínculo.
/// </summary>
public sealed class AccesoDigital
{
    public Guid ClienteId { get; }

    /// <summary>Identificador del usuario en el sistema de identidad (claim "sub" del token).</summary>
    public string Sub { get; }

    public DateTimeOffset FechaDeVinculo { get; }

    private AccesoDigital(Guid clienteId, string sub, DateTimeOffset fechaDeVinculo)
    {
        ClienteId = clienteId;
        Sub = sub;
        FechaDeVinculo = fechaDeVinculo;
    }

    /// <exception cref="ArgumentException">clienteId es Guid.Empty, o sub es null, vacío, solo espacios o tiene más de 255 caracteres.</exception>
    public static AccesoDigital Vincular(Guid clienteId, string sub, DateTimeOffset fechaDeVinculo) =>
        throw new NotImplementedException();

    /// <summary>
    /// Nombre de usuario con el que el cliente inicia sesión (RN-17): el código del tipo seguido del número ya
    /// normalizado, sin separador. CC 1.234.567 → "CC1234567"; PA ab-12345 → "PAAB12345".
    /// </summary>
    /// <exception cref="ArgumentNullException">documento es null.</exception>
    public static string NombreDeUsuarioDe(Documento documento) =>
        throw new NotImplementedException();
}
