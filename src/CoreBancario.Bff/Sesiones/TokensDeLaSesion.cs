namespace CoreBancario.Bff.Sesiones;

/// <summary>Tokens que el BFF guarda y NUNCA salen del servidor (RNF-01). ToString no los muestra.</summary>
public sealed record TokensDeLaSesion(string AccessToken, string RefreshToken, string IdToken, DateTimeOffset AccessTokenVence)
{
    // SDD: esqueleto creado por test-writer
    public override string ToString() => throw new NotImplementedException();
}
