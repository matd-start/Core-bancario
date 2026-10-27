using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.Errores;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Sesiones;

namespace CoreBancario.Bff.Tests;

/// <summary>Funciones puras del BFF: rutas de regreso (CL-08), rol de la interfaz (CL-04), areas, avisos y codigos.</summary>
public class ReglasPurasTests
{
    // ---- CL-08 / CA-13: RutaDeRegreso.Normalizar ----

    [Theory]
    // Clientes: rutas internas validas se conservan.
    [InlineData(AreaDeSesion.Clientes, "/mis-cuentas/abc", "/mis-cuentas/abc")]
    [InlineData(AreaDeSesion.Clientes, "/mis-cuentas", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/mis-cuentas?x=1", "/mis-cuentas?x=1")]
    // Clientes: nada externo, nada mal formado, nada del backoffice ni de /bff o /api.
    [InlineData(AreaDeSesion.Clientes, null, "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "https://x.com", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "//x.com", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/\\x.com", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "javascript:alert(1)", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "mis-cuentas", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/a\\b", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/a\u0001b", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/x?next=http://evil.example", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/backoffice", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/backoffice/clientes/1", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/BackOffice/clientes", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/bff/sesion", "/mis-cuentas")]
    [InlineData(AreaDeSesion.Clientes, "/api/clientes", "/mis-cuentas")]
    // Empleados: solo /backoffice y lo que cuelga de el (sin distinguir mayusculas).
    [InlineData(AreaDeSesion.Empleados, "/backoffice", "/backoffice")]
    [InlineData(AreaDeSesion.Empleados, "/backoffice/clientes/1", "/backoffice/clientes/1")]
    [InlineData(AreaDeSesion.Empleados, "/BACKOFFICE/x", "/BACKOFFICE/x")]
    [InlineData(AreaDeSesion.Empleados, "/mis-cuentas/abc", "/backoffice")]
    [InlineData(AreaDeSesion.Empleados, "/", "/backoffice")]
    [InlineData(AreaDeSesion.Empleados, "/backofficeX", "/backoffice")]
    [InlineData(AreaDeSesion.Empleados, "https://x.com", "/backoffice")]
    [InlineData(AreaDeSesion.Empleados, "//x.com", "/backoffice")]
    public void F004_CL08_RutaDeRegreso_Normalizar(AreaDeSesion area, string? regreso, string esperada)
    {
        // Arrange / Act
        var ruta = RutaDeRegreso.Normalizar(area, regreso);

        // Assert
        Assert.Equal(esperada, ruta);
    }

    [Fact]
    public void F004_CL08_RutaDeRegreso_DeExactamente2048Caracteres_SeConserva()
    {
        // Arrange
        var larga = "/mis-cuentas/" + new string('a', RutaDeRegreso.LongitudMaxima - "/mis-cuentas/".Length);

        // Act
        var ruta = RutaDeRegreso.Normalizar(AreaDeSesion.Clientes, larga);

        // Assert
        Assert.Equal(RutaDeRegreso.LongitudMaxima, larga.Length);
        Assert.Equal(larga, ruta);
    }

    [Fact]
    public void F004_CL08_RutaDeRegreso_DeMasDe2048Caracteres_VaAlInicioDelArea()
    {
        // Arrange
        var demasiado = "/mis-cuentas/" + new string('a', RutaDeRegreso.LongitudMaxima);

        // Act
        var ruta = RutaDeRegreso.Normalizar(AreaDeSesion.Clientes, demasiado);

        // Assert
        Assert.Equal("/mis-cuentas", ruta);
    }

    // ---- CL-04: LectorDeRol ----

    [Theory]
    [InlineData(AreaDeSesion.Clientes, "cliente", RolDeSesion.Cliente)]
    [InlineData(AreaDeSesion.Clientes, "cliente,offline_access", RolDeSesion.Cliente)]
    [InlineData(AreaDeSesion.Empleados, "operador", RolDeSesion.Operador)]
    [InlineData(AreaDeSesion.Empleados, "cliente,operador", RolDeSesion.Operador)]
    public void F004_CL04_LectorDeRol_RolDelAreaPresente_DevuelveElRol(AreaDeSesion area, string roles, RolDeSesion esperado)
    {
        // Arrange
        var token = Ayudas.CrearJwtSinFirma(roles.Split(','));

        // Act
        var rol = LectorDeRol.Desde(area, token);

        // Assert
        Assert.Equal(esperado, rol);
    }

    [Theory]
    [InlineData(AreaDeSesion.Clientes, "operador")]
    [InlineData(AreaDeSesion.Clientes, "")]
    [InlineData(AreaDeSesion.Empleados, "cliente")]
    [InlineData(AreaDeSesion.Empleados, "")]
    public void F004_CL04_LectorDeRol_SinElRolDelArea_DevuelveNull(AreaDeSesion area, string roles)
    {
        // Arrange
        var token = Ayudas.CrearJwtSinFirma(roles.Split(',', StringSplitOptions.RemoveEmptyEntries));

        // Act
        var rol = LectorDeRol.Desde(area, token);

        // Assert
        Assert.Null(rol);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJub25lIn0.sin-cuerpo-valido.")]
    public void F004_CL04_LectorDeRol_TokenIlegible_DevuelveNullSinLanzar(string token)
    {
        // Arrange / Act
        var rol = LectorDeRol.Desde(AreaDeSesion.Clientes, token);

        // Assert
        Assert.Null(rol);
    }

    // ---- RF-14: AreasDeSesion ----

    [Theory]
    [InlineData("clientes", AreaDeSesion.Clientes)]
    [InlineData("CLIENTES", AreaDeSesion.Clientes)]
    [InlineData("empleados", AreaDeSesion.Empleados)]
    [InlineData("Empleados", AreaDeSesion.Empleados)]
    public void F004_RF14_AreasDeSesion_TryParse_ReconoceLasDosAreasSinDistinguirMayusculas(string segmento, AreaDeSesion esperada)
    {
        // Arrange / Act
        var resultado = AreasDeSesion.TryParse(segmento, out var area);

        // Assert
        Assert.True(resultado);
        Assert.Equal(esperada, area);
    }

    [Theory]
    [InlineData("otra")]
    [InlineData("")]
    [InlineData(null)]
    public void F004_RF14_AreasDeSesion_TryParse_RechazaNullVacioYOtroTexto(string? segmento)
    {
        // Arrange / Act
        var resultado = AreasDeSesion.TryParse(segmento, out _);

        // Assert
        Assert.False(resultado);
    }

    [Theory]
    [InlineData(AreaDeSesion.Clientes, "clientes", "/", "/mis-cuentas", "OidcClientes", "/bff/signin-oidc/clientes")]
    [InlineData(AreaDeSesion.Empleados, "empleados", "/backoffice", "/backoffice", "OidcEmpleados", "/bff/signin-oidc/empleados")]
    public void F004_RF14_AreasDeSesion_CadaAreaTieneSusNombresYRutas(
        AreaDeSesion area, string segmento, string prefijo, string inicio, string esquema, string regresoOidc)
    {
        // Arrange / Act / Assert
        Assert.Equal(segmento, AreasDeSesion.Segmento(area));
        Assert.Equal(prefijo, AreasDeSesion.PrefijoDeLaSpa(area));
        Assert.Equal(inicio, AreasDeSesion.InicioDeLaSpa(area));
        Assert.Equal(esquema, AreasDeSesion.EsquemaOidc(area));
        Assert.Equal(regresoOidc, AreasDeSesion.RutaDeRegresoOidc(area));
    }

    [Fact]
    public void F004_RN22_OpcionesDelBff_De_DevuelveLasOpcionesDeCadaArea()
    {
        // Arrange
        var opciones = Construir.Opciones();

        // Act / Assert
        Assert.Same(opciones.Clientes, opciones.De(AreaDeSesion.Clientes));
        Assert.Same(opciones.Empleados, opciones.De(AreaDeSesion.Empleados));
    }

    // ---- CL-05 / CL-10: avisos ----

    [Theory]
    [InlineData(AreaDeSesion.Clientes, Avisos.InicioFallido, "/?aviso=inicio-fallido")]
    [InlineData(AreaDeSesion.Clientes, Avisos.OtraSesionAbierta, "/?aviso=otra-sesion-abierta")]
    [InlineData(AreaDeSesion.Empleados, Avisos.IdentidadNoDisponible, "/backoffice?aviso=identidad-no-disponible")]
    [InlineData(AreaDeSesion.Empleados, Avisos.OtraSesionAbierta, "/backoffice?aviso=otra-sesion-abierta")]
    public void F004_CL10_Avisos_UrlDe_UsaElPrefijoDeLaSpaDelArea(AreaDeSesion area, string aviso, string esperada)
    {
        // Arrange / Act
        var url = Avisos.UrlDe(area, aviso);

        // Assert
        Assert.Equal(esperada, url);
    }

    // ---- RNF-12: codigos del BFF ----

    [Theory]
    [InlineData(MotivoDeCierre.SinSesion, "sin-sesion")]
    [InlineData(MotivoDeCierre.Inactividad, "sesion-vencida-por-inactividad")]
    [InlineData(MotivoDeCierre.VidaMaxima, "sesion-vencida-por-vida-maxima")]
    [InlineData(MotivoDeCierre.Revocada, "sesion-revocada")]
    public void F004_RNF12_CodigosDelBff_De_TraduceCadaMotivoDeCierre(MotivoDeCierre motivo, string codigo)
    {
        // Arrange / Act
        var resultado = CodigosDelBff.De(motivo);

        // Assert
        Assert.Equal(codigo, resultado);
    }
}
