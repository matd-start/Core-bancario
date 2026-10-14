using CoreBancario.Domain.AccesosDigitales;

namespace CoreBancario.Application.AccesosDigitales;

/// <summary>Vínculo de la base (si hay) y usuario del proveedor (si hay).</summary>
public sealed record AccesoLocalizado(AccesoDigital? Vinculo, UsuarioDeIdentidad? Usuario)
{
    /// <summary>Usuario null → SinAcceso; Habilitado → Habilitado; si no → Deshabilitado.</summary>
    public EstadoDeAcceso Estado => Usuario switch
    {
        null => EstadoDeAcceso.SinAcceso,
        { Habilitado: true } => EstadoDeAcceso.Habilitado,
        _ => EstadoDeAcceso.Deshabilitado,
    };
}
