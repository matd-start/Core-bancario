namespace CoreBancario.Bff.Identidad;

/// <summary>Tokens que devuelve una renovación. IdToken null si Keycloak no lo reenvía (se conserva el anterior).</summary>
public sealed record TokensRenovados(string AccessToken, string RefreshToken, string? IdToken, DateTimeOffset AccessTokenVence)
{
    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
