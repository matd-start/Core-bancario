---
name: sdd-spec
description: Fase Spec del flujo SDD. Entrevista al autor y escribe specs/NNN-slug/spec.md con requisitos, reglas de negocio, casos límite y criterios de aceptación.
argument-hint: "[descripción breve de la feature]"
disable-model-invocation: true
---

# Fase Spec: qué y por qué

Feature a especificar: $ARGUMENTS

El objetivo es dejar escrito QUÉ debe hacer la feature y POR QUÉ, sin decidir todavía el CÓMO (eso es la fase Design). Esta fase la haces tú en la sesión principal, entrevistando al autor; no la delegues a un subagente.

## Pasos

1. **Prepara la carpeta.**
   - Mira las carpetas de `specs/`. El número nuevo es el mayor existente + 1, con tres dígitos (`001`, `002`, ...).
   - Crea `specs/NNN-slug/` con un slug corto en kebab-case derivado de la feature.
   - Si el repositorio usa git y estás en `main`, crea la rama `feature/NNN-slug` y cámbiate a ella.

2. **Lee el contexto**: `CLAUDE.md` y las ADRs de `docs/adr/`, para no preguntar lo que ya está decidido.

3. **Entrevista al autor** con la herramienta AskUserQuestion, en rondas de hasta 4 preguntas. Cubre, en este orden:
   - Objetivo y actores: quién usa esto y qué problema resuelve.
   - Datos: entidades, campos, tipos, obligatorios y rangos válidos.
   - Reglas de negocio y restricciones.
   - Errores y casos límite: datos inválidos, duplicados, concurrencia, fallos de servicios externos.
   - Requisitos no funcionales medibles: rendimiento, idempotencia, disponibilidad, seguridad, observabilidad.
   - Fuera de alcance.

   Reglas de la entrevista:
   - No hagas preguntas obvias; busca lo difícil que el autor quizá no pensó.
   - Cuando el autor no sepa qué responder, ofrécele 2-3 opciones con su trade-off en una línea cada una y deja que él elija. La decisión es suya; anótala en "Decisiones del autor".
   - No propongas tecnologías ni diseño. Si aparece una decisión técnica, anótala en "Preguntas para Design".

4. **Escribe `spec.md`** con la plantilla [spec-template.md](spec-template.md) y `Estado: Borrador`.
   - Todo requisito y criterio lleva ID (RF-01, RN-01, RNF-01, CL-01, CA-01) para rastrearlo en el plan, los tests y la revisión.
   - Criterios de aceptación en formato Dado / Cuando / Entonces, verificables con un test.
   - RNF medibles: número + condición. Si el autor no da un número, propón uno razonable y márcalo "(propuesto)".

5. **Puerta de aprobación.** Muestra un resumen de 5-8 líneas y pregunta con AskUserQuestion si aprueba la spec, quiere cambios o prefiere revisarla él mismo en el archivo.

6. **Al aprobar**: cambia a `Estado: Aprobado (AAAA-MM-DD)` con la fecha de hoy y haz commit `docs(NNN): spec aprobada`.

7. Indica el siguiente paso: `/sdd-design NNN-slug`.
