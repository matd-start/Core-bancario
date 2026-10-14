using CoreBancario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CoreBancario.Infrastructure.Tests;

/// <summary>
/// El vinculo sub - cliente (ADR-0016) se inserta por SQL: asi estas pruebas no dependen de que la entidad
/// AccesoDigital ya este mapeada en el DbContext, y comprueban de paso el esquema real de la tabla.
/// </summary>
internal static class AyudasDeAcceso
{
    public static string SubUnico() => Guid.NewGuid().ToString();

    public static Task VincularAsync(CoreBancarioDbContext db, Guid clienteId, string sub) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO accesos_digitales (cliente_id, sub, fecha_de_vinculo) VALUES ({clienteId}, {sub}, {Ayudas.Instante})",
            TestContext.Current.CancellationToken);
}
