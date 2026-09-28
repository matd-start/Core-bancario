# ADR-0001: Guiar el desarrollo con SDD, ADRs y agentes especializados

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal

## Contexto

Este es un proyecto de portafolio y de aprendizaje. Quien lo evalúe va a mirar, además del código, las decisiones: requisitos, reglas de negocio, restricciones y escalabilidad. Programar directo con un asistente de IA produce código rápido, pero deja poca evidencia del porqué y facilita que el asistente decida cosas que debería decidir el autor.

## Opciones consideradas

1. **Programar directo con el asistente.** Rápido y sin ceremonia; las decisiones quedan dispersas en conversaciones y no se pueden revisar.
2. **Framework externo de SDD (Spec Kit, BMAD).** Probado y completo; trae más proceso y más artefactos de los que un proyecto de una persona necesita.
3. **Kit propio y ligero de SDD con agentes de Claude Code.** Cuatro fases (Spec, Design, Build, Analysis), artefactos en el repositorio, aprobación del autor entre fases y un agente por rol con contexto limpio.

## Decisión

Opción 3. Cada feature tiene su carpeta en `specs/` con spec, plan, tareas, notas de aprendizaje y revisión. Las decisiones significativas se registran como ADR en `docs/adr/`. Las pruebas se escriben antes de la implementación y un revisor independiente compara el resultado con la spec antes del merge.

## Consecuencias

- Cada feature toma más tiempo al inicio, pero queda trazabilidad desde el requisito hasta el test.
- El autor aprueba spec, plan y merge: las decisiones siguen siendo suyas.
- Riesgo: que la spec quede desactualizada respecto al código. Se mitiga revisando las desviaciones en la fase Analysis antes del merge.
- Para bugs y cambios pequeños se usa un carril rápido sin spec, para no pagar el proceso completo en todo.
