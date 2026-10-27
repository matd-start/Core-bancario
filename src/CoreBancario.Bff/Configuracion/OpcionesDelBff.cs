namespace CoreBancario.Bff.Configuracion;

/// <summary>Sección "Bff". Los secretos van en user-secrets o en variables de entorno, nunca en appsettings.</summary>
public sealed class OpcionesDelBff
{
    public const string Seccion = "Bff";

    public OpcionesDeLaApi Api { get; set; } = new();

    public OpcionesDeArea Clientes { get; set; } = new();

    public OpcionesDeArea Empleados { get; set; } = new();

    // SDD: esqueleto creado por test-writer
    public OpcionesDeArea De(AreaDeSesion area) => throw new NotImplementedException();
}

public sealed class OpcionesDeLaApi
{
    /// <summary>Destino del proxy. Local: http://localhost:5096; compose: http://api:8080.</summary>
    public Uri Url { get; set; } = new("http://localhost:5096");
}

public sealed class OpcionesDeArea
{
    /// <summary>"iss" del realm, igual que en la API: http://localhost:8080/realms/clientes.</summary>
    public string Emisor { get; set; } = "";

    /// <summary>Documento de descubrimiento por el canal trasero. null: Emisor.</summary>
    public string? Metadatos { get; set; }

    public string ClientId { get; set; } = "core-bancario-web";

    public string ClientSecret { get; set; } = "";

    /// <summary>RN-22. appsettings.json: Clientes 00:05:00, Empleados 00:15:00.</summary>
    public TimeSpan Inactividad { get; set; }

    /// <summary>RN-22. appsettings.json: Clientes 00:30:00, Empleados 09:00:00.</summary>
    public TimeSpan VidaMaxima { get; set; }
}
