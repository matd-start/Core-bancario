namespace CoreBancario.Application.AccesosDigitales;

/// <summary>ToString se sobrescribe para que NO incluya la clave (los records imprimen todas sus propiedades, RNF-03).</summary>
public sealed record AccesoHabilitadoDto(Guid SolicitudId, Guid ClienteId, string Accion, string ClaveTemporal, DateTimeOffset Fecha)
{
    public override string ToString() =>
        $"{nameof(AccesoHabilitadoDto)} {{ {nameof(SolicitudId)} = {SolicitudId}, {nameof(ClienteId)} = {ClienteId}, " +
        $"{nameof(Accion)} = {Accion}, {nameof(Fecha)} = {Fecha} }}";
}
