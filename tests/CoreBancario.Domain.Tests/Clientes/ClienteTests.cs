using CoreBancario.Domain.Clientes;

namespace CoreBancario.Domain.Tests.Clientes;

public class ClienteTests
{
    private static readonly DateTimeOffset Instante = new(2026, 10, 5, 14, 3, 11, TimeSpan.Zero);

    private static Documento Doc => Documento.Crear(TipoDocumento.CC, "1234567");
    private static NombreDePersona Nombre => NombreDePersona.Crear("María José");
    private static NombreDePersona Apellido => NombreDePersona.Crear("Núñez");
    private static Correo Mail => Correo.Crear("maria@example.com");
    private static Telefono Tel => Telefono.Crear("+573001234567");

    [Fact]
    public void CA01_Registrar_ConservaTodosLosDatosYLaFechaDeRegistro()
    {
        // Arrange / Act
        var cliente = Cliente.Registrar(Doc, Nombre, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.Equal(Doc, cliente.Documento);
        Assert.Equal(Nombre, cliente.Nombres);
        Assert.Equal(Apellido, cliente.Apellidos);
        Assert.Equal(Mail, cliente.Correo);
        Assert.Equal(Tel, cliente.Telefono);
        Assert.Equal(Instante, cliente.FechaRegistro);
    }

    [Fact]
    public void CA01_Registrar_AsignaUnIdNoVacio()
    {
        // Arrange / Act
        var cliente = Cliente.Registrar(Doc, Nombre, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.NotEqual(Guid.Empty, cliente.Id);
    }

    [Fact]
    public void CA01_Registrar_IdEsVersion7DeLaFechaDeRegistro()
    {
        // Arrange / Act
        var cliente = Cliente.Registrar(Doc, Nombre, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.Equal(7, cliente.Id.Version);
        Assert.Equal(Instante.ToUnixTimeMilliseconds(), MilisegundosDelGuidV7(cliente.Id));
    }

    [Fact]
    public void CA01_RegistrarDosVeces_GeneraIdsDistintos()
    {
        // Arrange / Act
        var primero = Cliente.Registrar(Doc, Nombre, Apellido, Mail, Tel, Instante);
        var segundo = Cliente.Registrar(Doc, Nombre, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.NotEqual(primero.Id, segundo.Id);
    }

    [Fact]
    public void CA01_RegistrarSinDocumento_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cliente.Registrar(null!, Nombre, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void CA01_RegistrarSinNombres_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cliente.Registrar(Doc, null!, Apellido, Mail, Tel, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void CA01_RegistrarSinApellidos_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cliente.Registrar(Doc, Nombre, null!, Mail, Tel, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void CA01_RegistrarSinCorreo_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cliente.Registrar(Doc, Nombre, Apellido, null!, Tel, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    [Fact]
    public void CA01_RegistrarSinTelefono_LanzaArgumentNullException()
    {
        // Arrange / Act
        var accion = () => Cliente.Registrar(Doc, Nombre, Apellido, Mail, null!, Instante);

        // Assert
        Assert.Throws<ArgumentNullException>(accion);
    }

    // Un UUID v7 lleva en sus primeros 48 bits (big-endian) los milisegundos desde 1970.
    internal static long MilisegundosDelGuidV7(Guid id)
    {
        var bytes = id.ToByteArray(bigEndian: true);
        long milisegundos = 0;
        for (var i = 0; i < 6; i++)
            milisegundos = (milisegundos << 8) | bytes[i];
        return milisegundos;
    }
}
