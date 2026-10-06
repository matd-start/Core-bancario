using CoreBancario.Application.Comun;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

public sealed record CuentaDto(Guid Id, string Numero, Guid ClienteId, string Moneda, string Estado,
    DineroDto Saldo, DateTimeOffset FechaApertura, DateTimeOffset? FechaCierre)
{
    // SDD: esqueleto creado por test-writer
    public static CuentaDto Desde(Cuenta cuenta) => throw new NotImplementedException();
}
