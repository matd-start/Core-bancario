namespace CoreBancario.Bff.Identidad;

/// <summary>400 invalid_grant al renovar: la sesión de Keycloak ya no existe (vencida, cerrada o acceso deshabilitado).</summary>
public sealed class RenovacionRechazadaException : Exception
{
    public RenovacionRechazadaException()
    {
    }
}
