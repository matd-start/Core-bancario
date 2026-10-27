namespace CoreBancario.Bff;

/// <summary>
/// Tipo del ensamblado para WebApplicationFactory&lt;MarcadorDelBff&gt;. El Program de primer nivel del BFF queda
/// internal, así que no choca con el Program público de la API cuando una prueba referencia los dos proyectos.
/// </summary>
public sealed class MarcadorDelBff
{
    private MarcadorDelBff() { }
}
