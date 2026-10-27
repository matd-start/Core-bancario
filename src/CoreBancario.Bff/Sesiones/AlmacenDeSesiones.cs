using CoreBancario.Bff.Configuracion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CoreBancario.Bff.Sesiones;

/// <summary>
/// Almacén EN MEMORIA de las sesiones (ADR-0020). Implementa ITicketStore para el manejador de cookies: la cookie solo
/// lleva la clave. Cada entrada se libera sola al cumplirse Inicio + VidaMaxima. Singleton.
/// </summary>
public sealed class AlmacenDeSesiones : ITicketStore
{
    // SDD: esqueleto creado por test-writer
    public AlmacenDeSesiones(TimeProvider reloj, IOptions<OpcionesDelBff> opciones)
    {
    }

    /// <summary>Clave opaca: 32 bytes aleatorios en Base64Url.</summary>
    public static string NuevaClave() => throw new NotImplementedException();

    public Task<string> StoreAsync(AuthenticationTicket ticket) => throw new NotImplementedException();

    public Task RenewAsync(string key, AuthenticationTicket ticket) => throw new NotImplementedException();

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => throw new NotImplementedException();

    public Task RemoveAsync(string key) => throw new NotImplementedException();

    public void Agregar(SesionDelNavegador sesion) => throw new NotImplementedException();

    public SesionDelNavegador? Obtener(string clave) => throw new NotImplementedException();

    public void Eliminar(string clave) => throw new NotImplementedException();

    /// <summary>Serializa las operaciones sobre UNA sesión (renovaciones con rotación, maxReuse 0).</summary>
    public Task<T> ConCandadoAsync<T>(string clave, Func<Task<T>> operacion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
