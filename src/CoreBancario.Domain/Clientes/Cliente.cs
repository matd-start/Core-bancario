namespace CoreBancario.Domain.Clientes;

/// <summary>Persona natural titular de cuentas. Inmutable en la fase 1: editar datos está fuera de alcance.</summary>
public sealed class Cliente
{
    public Guid Id { get; }
    public Documento Documento { get; }
    public NombreDePersona Nombres { get; }
    public NombreDePersona Apellidos { get; }
    public Correo Correo { get; }
    public Telefono Telefono { get; }
    public DateTimeOffset FechaRegistro { get; }

    // Solo para EF Core: Documento es una navegación owned y EF no la puede pasar por constructor.
#pragma warning disable CS8618
    private Cliente() { }
#pragma warning restore CS8618

    private Cliente(Guid id, Documento documento, NombreDePersona nombres, NombreDePersona apellidos,
        Correo correo, Telefono telefono, DateTimeOffset fechaRegistro)
    {
        Id = id;
        Documento = documento;
        Nombres = nombres;
        Apellidos = apellidos;
        Correo = correo;
        Telefono = telefono;
        FechaRegistro = fechaRegistro;
    }

    /// <summary>Id = Guid.CreateVersion7(fechaRegistro). La unicidad del documento (RN-17) la garantiza la base.</summary>
    /// <exception cref="ArgumentNullException">Algún value object es null.</exception>
    public static Cliente Registrar(Documento documento, NombreDePersona nombres, NombreDePersona apellidos,
        Correo correo, Telefono telefono, DateTimeOffset fechaRegistro)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(nombres);
        ArgumentNullException.ThrowIfNull(apellidos);
        ArgumentNullException.ThrowIfNull(correo);
        ArgumentNullException.ThrowIfNull(telefono);

        return new Cliente(Guid.CreateVersion7(fechaRegistro), documento, nombres, apellidos, correo, telefono, fechaRegistro);
    }
}
