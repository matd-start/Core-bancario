using CoreBancario.Application.Cuentas;

namespace CoreBancario.Application.Clientes;

/// <summary>Cuentas ordenadas por FechaApertura y después por Id. Lista vacía si no tiene (CL-19).</summary>
public sealed record FichaClienteDto(ClienteDto Cliente, IReadOnlyList<CuentaDto> Cuentas);
