namespace CoreBancario.Api.Autenticacion;

/// <summary>Sección "Autenticacion". La tolerancia de reloj NO es configurable: RNF-01 depende de ella (ADR-0015).</summary>
public sealed class OpcionesDeAutenticacion
{
    public const string Seccion = "Autenticacion";
    public static readonly TimeSpan ToleranciaDeReloj = TimeSpan.FromSeconds(30);
    public const string AlgoritmoPermitido = "RS256";
    public string Audiencia { get; set; } = "core-bancario-api";
    public OpcionesDeEmisor Clientes { get; set; } = new();
    public OpcionesDeEmisor Empleados { get; set; } = new();
}

public sealed class OpcionesDeEmisor
{
    /// <summary>URL del realm, igual al "iss" de sus tokens: http://localhost:8080/realms/clientes.</summary>
    public string Emisor { get; set; } = "";

    /// <summary>Documento de descubrimiento por el canal trasero (compose). null: se deriva de Emisor. No cambia ValidIssuer.</summary>
    public string? Metadatos { get; set; }
}
