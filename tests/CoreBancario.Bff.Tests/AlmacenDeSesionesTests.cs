using CoreBancario.Bff.Configuracion;
using CoreBancario.Bff.InicioDeSesion;
using CoreBancario.Bff.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace CoreBancario.Bff.Tests;

/// <summary>AlmacenDeSesiones (ADR-0020): claves opacas, ticket sin tokens, caducidad y candado por sesion.</summary>
public class AlmacenDeSesionesTests
{
    private static (AlmacenDeSesiones Almacen, FakeTimeProvider Reloj) Crear()
    {
        var reloj = new FakeTimeProvider(Construir.Origen);
        return (new AlmacenDeSesiones(reloj, Construir.OpcionesEnvueltas()), reloj);
    }

    private static AuthenticationTicket TicketDePrueba(string access = "access-secreto", string refresh = "refresh-secreto",
        string id = "id-secreto") =>
        Construir.Ticket("sub-1", "sid-1", access, refresh, id, Construir.Origen, Construir.Origen.AddSeconds(270));

    // ---- RNF-01: clave opaca de 256 bits ----

    [Fact]
    public void F004_RNF01_NuevaClave_Tiene256BitsEnBase64UrlYNoSeRepite()
    {
        // Arrange / Act
        var primera = AlmacenDeSesiones.NuevaClave();
        var segunda = AlmacenDeSesiones.NuevaClave();

        // Assert
        var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(primera);
        Assert.Equal(32, bytes.Length);
        Assert.NotEqual(primera, segunda);
    }

    // ---- RNF-01: el ticket de la cookie no lleva tokens ----

    [Fact]
    public async Task F004_RNF01_StoreAsync_GuardaLaSesionConSusTokens()
    {
        // Arrange
        var (almacen, _) = Crear();

        // Act
        var clave = await almacen.StoreAsync(TicketDePrueba());

        // Assert
        var sesion = almacen.Obtener(clave);
        Assert.NotNull(sesion);
        Assert.Equal("access-secreto", sesion.Tokens.AccessToken);
        Assert.Equal("refresh-secreto", sesion.Tokens.RefreshToken);
        Assert.Equal("id-secreto", sesion.Tokens.IdToken);
    }

    [Fact]
    public async Task F004_RNF01_RetrieveAsync_DevuelveUnTicketSinTokensYConLaClave()
    {
        // Arrange
        var (almacen, _) = Crear();
        var clave = await almacen.StoreAsync(TicketDePrueba());

        // Act
        var ticket = await almacen.RetrieveAsync(clave);

        // Assert
        Assert.NotNull(ticket);
        Assert.Empty(ticket.Properties.GetTokens());
        Assert.Equal(clave, ticket.Properties.Items[PropiedadesDeSesion.Clave]);
    }

    [Fact]
    public async Task F004_RN22_StoreAsync_ConstruyeLaSesionConLosDatosDelTicket()
    {
        // Arrange
        var (almacen, _) = Crear();
        var access = Ayudas.CrearJwtSinFirma("cliente");

        // Act
        var clave = await almacen.StoreAsync(Construir.Ticket("sub-9", "sid-9", access, "r", "i",
            Construir.Origen.AddSeconds(-30), Construir.Origen.AddSeconds(270)));

        // Assert
        var sesion = almacen.Obtener(clave)!;
        Assert.Equal(AreaDeSesion.Clientes, sesion.Area);
        Assert.Equal("sub-9", sesion.Sub);
        Assert.Equal("cc1234567", sesion.Usuario);
        Assert.Equal("sid-9", sesion.Sid);
        Assert.Equal(RolDeSesion.Cliente, sesion.Rol);
        Assert.Equal(Construir.Origen.AddSeconds(-30), sesion.Inicio);
        Assert.Equal(Construir.Origen, sesion.UltimaActividad);
        Assert.Equal(Construir.Origen.AddSeconds(270), sesion.Tokens.AccessTokenVence);
    }

    [Fact]
    public async Task F004_RN22_RetrieveAsync_ConClaveInexistente_DevuelveNull()
    {
        // Arrange
        var (almacen, _) = Crear();

        // Act
        var ticket = await almacen.RetrieveAsync("no-existe");

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public async Task F004_RF14_RemoveAsync_BorraLaSesion()
    {
        // Arrange
        var (almacen, _) = Crear();
        var clave = await almacen.StoreAsync(TicketDePrueba());

        // Act
        await almacen.RemoveAsync(clave);

        // Assert
        Assert.Null(almacen.Obtener(clave));
    }

    // ---- RN-22: caducidad en Inicio + VidaMaxima ----

    [Fact]
    public void F004_RN22_Sesion_SigueEnElAlmacenUnSegundoAntesDeLaVidaMaxima()
    {
        // Arrange
        var (almacen, reloj) = Crear();
        almacen.Agregar(Construir.Sesion());

        // Act
        reloj.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(almacen.Obtener("clave-1"));
    }

    [Fact]
    public void F004_RN22_Sesion_SeLiberaAlCumplirseInicioMasVidaMaxima()
    {
        // Arrange
        var (almacen, reloj) = Crear();
        almacen.Agregar(Construir.Sesion());

        // Act
        reloj.Advance(TimeSpan.FromMinutes(30));

        // Assert
        Assert.Null(almacen.Obtener("clave-1"));
    }

    // ---- CL-12: una renovacion a la vez por sesion (refresh con rotacion, maxReuse 0) ----

    [Fact]
    public async Task F004_CL12_ConCandadoAsync_SerializaLasOperacionesDeUnaMismaSesion()
    {
        // Arrange
        var (almacen, _) = Crear();
        var simultaneas = 0;
        var maximo = 0;

        async Task<int> OperacionAsync()
        {
            var ahora = Interlocked.Increment(ref simultaneas);
            Interlocked.Exchange(ref maximo, Math.Max(Volatile.Read(ref maximo), ahora));
            await Task.Delay(50, Ayudas.Ct);
            Interlocked.Decrement(ref simultaneas);
            return ahora;
        }

        // Act
        await Task.WhenAll(
            almacen.ConCandadoAsync("k", OperacionAsync, Ayudas.Ct),
            almacen.ConCandadoAsync("k", OperacionAsync, Ayudas.Ct),
            almacen.ConCandadoAsync("k", OperacionAsync, Ayudas.Ct));

        // Assert
        Assert.Equal(1, maximo);
    }

    [Fact]
    public async Task F004_CL12_ConCandadoAsync_NoBloqueaLasOperacionesDeSesionesDistintas()
    {
        // Arrange
        var (almacen, _) = Crear();
        var bHaEmpezado = new TaskCompletionSource();

        // Act: A espera a que B haya empezado; si el candado fuera global, A y B se bloquearian entre si.
        var a = almacen.ConCandadoAsync("a", async () =>
        {
            await bHaEmpezado.Task.WaitAsync(TimeSpan.FromSeconds(5), Ayudas.Ct);
            return 1;
        }, Ayudas.Ct);
        var b = almacen.ConCandadoAsync("b", () =>
        {
            bHaEmpezado.SetResult();
            return Task.FromResult(2);
        }, Ayudas.Ct);

        // Assert
        var resultados = await Task.WhenAll(a, b);
        Assert.Equal(new[] { 1, 2 }, resultados);
    }
}
