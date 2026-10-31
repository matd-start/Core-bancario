using Jint;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// Ejecuta documento.js (tema core-bancario-clientes) con Jint, un intérprete de JavaScript, y llama a sus funciones
/// puras (ADR-0018). El script se carga con un "document" de mentira porque, en un navegador, engancha eventos al
/// cargar. Lo que hace con esos eventos se prueba en PaginaDelTema; ninguna prueba lo ejecuta en un navegador real.
/// </summary>
internal static class ScriptDelTema
{
    public static string Ruta => Path.Combine(Ayudas.RaizDelRepositorio(), "infra", "keycloak", "themes",
        "core-bancario-clientes", "login", "resources", "js", "documento.js");

    private static Engine Cargar()
    {
        var motor = new Engine();
        motor.Execute(PaginaDelTema.DomDeMentira);
        motor.Execute(File.ReadAllText(Ruta));
        return motor;
    }

    /// <summary>Lo que el script escribe en el campo username: tipo + numero normalizado.</summary>
    public static string NormalizarUsuario(string tipo, string numero) =>
        Cargar().Invoke("normalizarUsuario", tipo, numero).AsString();

    /// <summary>true si la clave contiene el numero del documento (el usuario sin sus dos letras de tipo).</summary>
    public static bool ContieneNumeroDeDocumento(string usuario, string clave) =>
        Cargar().Invoke("contieneNumeroDeDocumento", usuario, clave).AsBoolean();
}
