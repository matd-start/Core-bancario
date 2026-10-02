using System.Reflection;
using CoreBancario.Domain.Monetario;

namespace CoreBancario.Domain.Tests;

public class ArquitecturaDelDominioTests
{
    private const BindingFlags MiembrosPublicosPropios =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool EsDoubleOFloat(Type tipo) => tipo == typeof(double) || tipo == typeof(float);

    // Firme porque compara contra una lista exacta y mínima de nombres: cualquier
    // referencia nueva (p. ej. un paquete NuGet como System.Reactive) hace fallar la
    // prueba, a diferencia de aceptar todo lo que empiece por "System". Sin leer disco.
    private static readonly string[] ReferenciasPermitidas =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Runtime.Extensions",
        "System.Diagnostics.CodeAnalysis",
    ];

    [Fact]
    public void RNF01_EnsambladoDelDominio_SoloReferenciaElFramework()
    {
        // Arrange
        var referencias = typeof(Dinero).Assembly.GetReferencedAssemblies();

        // Act
        var noPermitidas = referencias
            .Select(r => r.Name ?? string.Empty)
            .Except(ReferenciasPermitidas)
            .ToList();

        // Assert
        Assert.Empty(noPermitidas);
    }

    [Fact]
    public void RNF04_MiembrosPublicosDelDominio_NoUsanDoubleNiFloat()
    {
        // Arrange
        var tipos = typeof(Dinero).Assembly.GetExportedTypes();

        // Act
        var tiposUsadosEnLaApi = tipos.SelectMany(t => TiposDeMiembrosPublicos(t)).ToList();
        var infractores = tiposUsadosEnLaApi.Where(EsDoubleOFloat).ToList();

        // Assert
        Assert.NotEmpty(tipos);
        Assert.Empty(infractores);
    }

    private static IEnumerable<Type> TiposDeMiembrosPublicos(Type tipo)
    {
        var propiedades = tipo.GetProperties(MiembrosPublicosPropios).Select(p => p.PropertyType);
        var campos = tipo.GetFields(MiembrosPublicosPropios).Select(f => f.FieldType);
        var retornos = tipo.GetMethods(MiembrosPublicosPropios).Select(m => m.ReturnType);
        var parametrosDeMetodos = tipo.GetMethods(MiembrosPublicosPropios)
            .SelectMany(m => m.GetParameters()).Select(p => p.ParameterType);
        var parametrosDeConstructores = tipo.GetConstructors(MiembrosPublicosPropios)
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

        return propiedades.Concat(campos).Concat(retornos)
            .Concat(parametrosDeMetodos).Concat(parametrosDeConstructores);
    }
}
