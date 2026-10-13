// SDD: esqueleto creado por test-writer
using CoreBancario.Domain.Cuentas;

namespace CoreBancario.Application.Cuentas;

/// <summary>
/// Puerto de LECTURA de las cuentas de un titular identificado por su sub (RN-12, ADR-0016). El filtro de dueño va
/// DENTRO de la consulta: una cuenta ajena y una inexistente devuelven lo mismo (null), por construcción (CA-15).
/// </summary>
public interface ICuentasDelTitular
{
    /// <summary>Todas, incluidas las Cerradas (CL-18), por FechaApertura y después Id. Vacía si no tiene cuentas o no hay vínculo.</summary>
    Task<IReadOnlyList<Cuenta>> ListarAsync(string sub, CancellationToken cancellationToken);

    /// <summary>null si no existe O si no es del titular.</summary>
    Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub, CancellationToken cancellationToken);
}
