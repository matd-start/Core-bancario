using System.Security.Claims;
using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Identidad;
using CoreBancario.Bff.Sesiones;

namespace CoreBancario.Bff.Tests;

/// <summary>SesionDelNavegador, TokensDeLaSesion y PoliticaDeSesion: reglas puras de RN-22, RN-25 y RNF-01.</summary>
public class SesionDelNavegadorTests
{
    private static SesionDelNavegador CrearSesion(string clave = "k", string sub = "s", string usuario = "u", string sid = "x",
        DateTimeOffset? inicio = null, DateTimeOffset? ultimaActividad = null) =>
        new(clave, AreaDeSesion.Clientes, sub, usuario, sid, RolDeSesion.Cliente,
            inicio ?? Construir.Origen, ultimaActividad ?? Construir.Origen,
            Construir.Tokens(Construir.Origen.AddMinutes(4)), new ClaimsPrincipal(new ClaimsIdentity("pruebas")));

    // ---- RN-22: guardas ----

    [Theory]
    [InlineData("", "s", "u", "x")]
    [InlineData("k", "", "u", "x")]
    [InlineData("k", "s", "", "x")]
    [InlineData("k", "s", "u", "")]
    public void F004_RN22_SesionDelNavegador_ConDatosVacios_LanzaArgumentException(string clave, string sub, string usuario, string sid)
    {
        // Arrange / Act
        var crear = () => CrearSesion(clave, sub, usuario, sid);

        // Assert
        Assert.ThrowsAny<ArgumentException>(crear);
    }

    [Fact]
    public void F004_RN25_SesionDelNavegador_UltimaActividadAnteriorAlInicio_LanzaArgumentException()
    {
        // Arrange / Act
        var crear = () => CrearSesion(inicio: Construir.Origen, ultimaActividad: Construir.Origen.AddSeconds(-1));

        // Assert
        Assert.Throws<ArgumentException>(crear);
    }

    // ---- RN-25: la actividad ----

    [Fact]
    public void F004_RN25_RegistrarActividad_MueveLaUltimaActividad()
    {
        // Arrange
        var sesion = CrearSesion();
        var ahora = Construir.Origen.AddMinutes(2);

        // Act
        sesion.RegistrarActividad(ahora);

        // Assert
        Assert.Equal(ahora, sesion.UltimaActividad);
    }

    [Fact]
    public void F004_RN25_RegistrarActividad_NuncaRetrocede()
    {
        // Arrange
        var sesion = CrearSesion();
        sesion.RegistrarActividad(Construir.Origen.AddMinutes(3));

        // Act
        sesion.RegistrarActividad(Construir.Origen.AddMinutes(1));

        // Assert
        Assert.Equal(Construir.Origen.AddMinutes(3), sesion.UltimaActividad);
    }

    [Fact]
    public void F004_RN25_RegistrarActividad_NoCambiaElInicio()
    {
        // Arrange
        var sesion = CrearSesion();

        // Act
        sesion.RegistrarActividad(Construir.Origen.AddMinutes(4));

        // Assert
        Assert.Equal(Construir.Origen, sesion.Inicio);
    }

    [Fact]
    public void F004_CL12_ReemplazarTokens_CambiaLosTokensDeLaSesion()
    {
        // Arrange
        var sesion = CrearSesion();
        var nuevos = Construir.Tokens(Construir.Origen.AddMinutes(9), "2");

        // Act
        sesion.ReemplazarTokens(nuevos);

        // Assert
        Assert.Equal(nuevos, sesion.Tokens);
    }

    // ---- RNF-01: ToString no muestra tokens ----

    [Fact]
    public void F004_RNF01_TokensDeLaSesion_ToStringNoLosMuestra()
    {
        // Arrange
        var tokens = new TokensDeLaSesion("eyJ-access-secreto", "refresh-secreto", "id-secreto", Construir.Origen);

        // Act
        var texto = tokens.ToString();

        // Assert
        Assert.DoesNotContain("access-secreto", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secreto", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("id-secreto", texto, StringComparison.Ordinal);
        Assert.Contains("ocultos", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void F004_RNF01_TokensRenovados_ToStringNoLosMuestra()
    {
        // Arrange
        var tokens = new TokensRenovados("eyJ-access-secreto", "refresh-secreto", "id-secreto", Construir.Origen);

        // Act
        var texto = tokens.ToString();

        // Assert
        Assert.DoesNotContain("access-secreto", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secreto", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("id-secreto", texto, StringComparison.Ordinal);
    }

    // ---- RN-22 / RNF-06: PoliticaDeSesion ----

    [Theory]
    // Clientes: 5 min de inactividad, 30 min de vida maxima. Segundos desde el inicio hasta la ultima actividad y hasta "ahora".
    [InlineData(AreaDeSesion.Clientes, 0, 299, EstadoDeSesion.Vigente)]
    [InlineData(AreaDeSesion.Clientes, 0, 300, EstadoDeSesion.VencidaPorInactividad)]
    [InlineData(AreaDeSesion.Clientes, 0, 301, EstadoDeSesion.VencidaPorInactividad)]
    [InlineData(AreaDeSesion.Clientes, 1500, 1799, EstadoDeSesion.Vigente)]
    [InlineData(AreaDeSesion.Clientes, 1700, 1799, EstadoDeSesion.Vigente)]
    [InlineData(AreaDeSesion.Clientes, 1700, 1800, EstadoDeSesion.VencidaPorVidaMaxima)]
    [InlineData(AreaDeSesion.Clientes, 1700, 2000, EstadoDeSesion.VencidaPorVidaMaxima)]
    // Si vencen las dos a la vez, gana la vida maxima.
    [InlineData(AreaDeSesion.Clientes, 0, 1800, EstadoDeSesion.VencidaPorVidaMaxima)]
    // Empleados: 15 min y 9 h.
    [InlineData(AreaDeSesion.Empleados, 0, 899, EstadoDeSesion.Vigente)]
    [InlineData(AreaDeSesion.Empleados, 0, 900, EstadoDeSesion.VencidaPorInactividad)]
    [InlineData(AreaDeSesion.Empleados, 32000, 32399, EstadoDeSesion.Vigente)]
    [InlineData(AreaDeSesion.Empleados, 32000, 32400, EstadoDeSesion.VencidaPorVidaMaxima)]
    public void F004_RN22_PoliticaDeSesion_Evaluar_LosLimitesSonInclusivos(
        AreaDeSesion area, int segundosHastaLaUltimaActividad, int segundosHastaAhora, EstadoDeSesion esperado)
    {
        // Arrange
        var opciones = area == AreaDeSesion.Clientes ? Construir.OpcionesDeClientes() : Construir.OpcionesDeEmpleados();
        var sesion = Construir.Sesion(area, Construir.Origen, Construir.Origen.AddSeconds(segundosHastaLaUltimaActividad));

        // Act
        var estado = PoliticaDeSesion.Evaluar(sesion, opciones, Construir.Origen.AddSeconds(segundosHastaAhora));

        // Assert
        Assert.Equal(esperado, estado);
    }

    [Fact]
    public void F004_CA07_PoliticaDeSesion_ActividadALosCuatroMinutos_SigueVigenteALosSeis()
    {
        // Arrange
        var sesion = Construir.Sesion();
        sesion.RegistrarActividad(Construir.Origen.AddMinutes(4));

        // Act
        var estado = PoliticaDeSesion.Evaluar(sesion, Construir.OpcionesDeClientes(), Construir.Origen.AddMinutes(6));

        // Assert
        Assert.Equal(EstadoDeSesion.Vigente, estado);
    }

    [Fact]
    public void F004_RN22_PoliticaDeSesion_Plazos_SonLasDuracionesQueFaltanParaCadaCierre()
    {
        // Arrange
        var sesion = Construir.Sesion(ultimaActividad: Construir.Origen.AddSeconds(60));

        // Act
        var plazos = PoliticaDeSesion.Plazos(sesion, Construir.OpcionesDeClientes(), Construir.Origen.AddSeconds(120));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(240), plazos.HastaInactividad);
        Assert.Equal(TimeSpan.FromSeconds(1680), plazos.HastaVidaMaxima);
    }

    [Fact]
    public void F004_RNF06_PoliticaDeSesion_Plazos_NuncaSonNegativos()
    {
        // Arrange
        var sesion = Construir.Sesion();

        // Act
        var plazos = PoliticaDeSesion.Plazos(sesion, Construir.OpcionesDeClientes(), Construir.Origen.AddHours(2));

        // Assert
        Assert.Equal(TimeSpan.Zero, plazos.HastaInactividad);
        Assert.Equal(TimeSpan.Zero, plazos.HastaVidaMaxima);
    }
}
