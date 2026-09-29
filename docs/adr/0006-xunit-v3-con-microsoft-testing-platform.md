# ADR-0006: xUnit v3 con Microsoft Testing Platform

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal

## Contexto

La spec exige pruebas de cada RN y RF, con integración contra PostgreSQL real. La plantilla `dotnet new xunit` del SDK 10 todavía genera xUnit v2 sobre VSTest. Cambiar de framework es barato ahora, que no hay pruebas, y caro después.

## Opciones consideradas

1. **xUnit v2 con VSTest** (lo que trae la plantilla). Conocido y estable. En contra: v2 solo recibe correcciones; las novedades llegan a v3.
2. **xUnit v3 con Microsoft Testing Platform (MTP).** Versión activa de xUnit. Cada proyecto de pruebas es un ejecutable, arranca más rápido y `dotnet test` usa MTP de forma nativa en .NET 10. En contra: algunas guías y extensiones antiguas asumen VSTest.
3. **NUnit o MSTest.** Válidos. En contra: xUnit es el más extendido en proyectos ASP.NET Core y ya es parte del stack del autor.

## Decisión

xUnit v3 (`xunit.v3.mtp-v2`) con Microsoft Testing Platform, activado en `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`). El motivo principal es empezar en la versión activa del framework sin coste de migración.

## Consecuencias

- Los proyectos de pruebas tienen `OutputType` `Exe` y un `xunit.runner.json`.
- Con MTP, un proyecto sin pruebas termina con código 8. Mientras un proyecto esté vacío, lleva `--ignore-exit-code 8` en `TestingPlatformCommandLineArguments`; se quita al añadir su primera prueba, para que el CI detecte si las pruebas dejan de ejecutarse.
- La cobertura, si se necesita, se añade con `Microsoft.Testing.Extensions.CodeCoverage`, no con `coverlet.collector` (que es de VSTest).
