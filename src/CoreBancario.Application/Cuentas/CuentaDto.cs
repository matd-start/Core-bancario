using CoreBancario.Application.Comun;
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

public sealed record CuentaDto(Guid Id, string Numero, Guid ClienteId, string Moneda, string Estado,
    DineroDto Saldo, DateTimeOffset FechaApertura, DateTimeOffset? FechaCierre)
{
    public static CuentaDto Desde(Cuenta cuenta)
    {
        ArgumentNullException.ThrowIfNull(cuenta);

        return new CuentaDto(
            cuenta.Id,
            cuenta.Numero.Valor,
            cuenta.ClienteId,
            cuenta.Moneda.Codigo,
            cuenta.Estado.ToString(),
            DineroDto.Desde(cuenta.Saldo),
            cuenta.FechaApertura,
            cuenta.FechaCierre);
    }
}
