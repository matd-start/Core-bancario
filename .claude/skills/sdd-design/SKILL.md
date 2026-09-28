---
name: sdd-design
description: Fase Design del flujo SDD. Delega al subagente planner para convertir una spec aprobada en plan técnico, tareas y ADRs, y pide la aprobación del autor.
argument-hint: "[NNN-slug]"
disable-model-invocation: true
---

# Fase Design: cómo

Feature: $ARGUMENTS (carpeta `specs/$ARGUMENTS/`). Si no se indicó, usa la carpeta más reciente de `specs/` y confírmalo con el autor.

## Pasos

1. Verifica que `specs/<feature>/spec.md` existe y dice `Estado: Aprobado`. Si no, detente y sugiere `/sdd-spec`.

2. **Delega al subagente `planner`** con un prompt que incluya:
   - la ruta de la carpeta de la feature;
   - las plantillas: `${CLAUDE_SKILL_DIR}/plan-template.md`, `${CLAUDE_SKILL_DIR}/tasks-template.md` y `${CLAUDE_SKILL_DIR}/adr-template.md`.

3. Si el planner devuelve preguntas abiertas, házselas al autor con AskUserQuestion, usando las opciones y trade-offs que propuso. Reanuda al mismo planner con SendMessage pasándole las respuestas. Repite hasta que no queden preguntas.

4. **Presenta el diseño al autor.** Está aprendiendo, así que explica en lenguaje claro:
   - las decisiones de diseño principales y el porqué de cada una;
   - las ADRs creadas;
   - cuántas tareas hay y en qué orden van las capas.

5. **Puerta de aprobación.** Pregunta con AskUserQuestion si aprueba el plan, quiere cambios o prefiere revisarlo en los archivos. Si pide cambios, reanuda al planner con ellos.

6. **Al aprobar**: pon `Estado: Aprobado (AAAA-MM-DD)` en `plan.md`, cambia las ADRs nuevas de `Propuesta` a `Aceptada` y haz commit `docs(NNN): plan y ADRs aprobados`.

7. Indica el siguiente paso: `/sdd-build <feature>`.
