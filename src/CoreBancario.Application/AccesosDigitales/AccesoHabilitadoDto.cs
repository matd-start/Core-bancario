namespace CoreBancario.Application.AccesosDigitales;

/// <summary>ToString se sobrescribe para que NO incluya la clave (los records imprimen todas sus propiedades, RNF-03).</summary>
public sealed record AccesoHabilitadoDto(Guid SolicitudId, Guid ClienteId, string Accion, string ClaveTemporal, DateTimeOffset Fecha)
{
    // SDD: esqueleto creado por test-writer
    // Forma de la implementación: "AccesoHabilitadoDto { SolicitudId = …, ClienteId = …, Accion = …, Fecha = … }"
    public override string ToString() => throw new NotImplementedException();
}
