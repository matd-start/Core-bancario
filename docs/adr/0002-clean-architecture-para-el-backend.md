# ADR-0002: Organizar el backend con Clean Architecture

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal (resuelve D-01 de la spec)

## Contexto

El core bancario tiene reglas de negocio que son el centro del portafolio: saldo nunca negativo, dinero sin mezcla de monedas, movimientos inmutables. Esas reglas deben poder probarse sin base de datos ni HTTP, y el código debe ser fácil de explicar en una entrevista. Además, el kit SDD (planner, test-writer, coder, reviewer) ordena el trabajo por capas.

## Opciones consideradas

1. **Clean Architecture.** Capas Domain, Application, Infrastructure y Api con dependencias hacia dentro. El dominio queda aislado y se prueba con pruebas unitarias puras; es el estilo más reconocido en .NET. En contra: más proyectos y archivos, y algo de ceremonia en features simples.
2. **Vertical slices.** Cada feature agrupa endpoint, lógica y acceso a datos. Menos ceremonia y cambios más locales. En contra: el dominio rico queda repartido entre slices y habría que adaptar el kit SDD, que asume capas.
3. **Hexagonal (puertos y adaptadores).** Equivalente en espíritu a Clean. En contra: el kit usa la nomenclatura de Clean y renombrarla no aporta valor.

## Decisión

Clean Architecture con cuatro proyectos (`CoreBancario.Domain`, `.Application`, `.Infrastructure`, `.Api`) y un proyecto de pruebas por capa. El motivo principal es aislar las reglas de negocio para probarlas sin infraestructura, y que el kit SDD funciona sin adaptaciones.

## Consecuencias

- Domain no referencia ningún paquete externo; las reglas RN-xx se prueban con pruebas unitarias rápidas.
- Application define puertos (interfaces) que Infrastructure implementa.
- Api se mantiene delgada: valida, delega en Application y traduce errores de dominio a respuestas HTTP.
- Riesgo: sobre-diseñar. Se mitiga con KISS y YAGNI (skill `csharp-clean-code`): ninguna abstracción sin un requisito que la pida.
