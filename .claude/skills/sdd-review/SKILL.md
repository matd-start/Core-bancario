---
name: sdd-review
description: Fase Analysis del flujo SDD. El subagente reviewer compara el cambio con la spec y el plan, da un veredicto y cierra con un punto de comprensión antes del pull request.
argument-hint: "[NNN-slug]"
disable-model-invocation: true
---

# Fase Analysis: veredicto

Feature: $ARGUMENTS (carpeta `specs/$ARGUMENTS/`).

## Pasos

1. **Delega al subagente `reviewer`** con la ruta de la feature. No le pases tu opinión ni el razonamiento de la fase Build: debe juzgar con contexto limpio.

2. Guarda su respuesta completa, sin editarla, en `specs/<feature>/review.md`.

3. **Si el veredicto es CAMBIOS REQUERIDOS:**
   - Muestra al autor los hallazgos obligatorios.
   - Pregúntale con AskUserQuestion (selección múltiple) cuáles quiere corregir.
   - Delega cada corrección al responsable que indicó el reviewer: el `coder` para código, el `test-writer` para pruebas que faltan.
   - Ejecuta `dotnet test`, haz commit `fix(NNN): correcciones de la revisión` (o `test(NNN): ...` si solo cambiaron pruebas) y vuelve al paso 1.
   - Máximo 2 ciclos; después, decide el autor.

4. **Si el veredicto es APROBADO:**
   - **Spec viva.** Si el reviewer listó desviaciones entre spec/plan y código, propón al autor cómo actualizar los documentos. Con su visto bueno, actualízalos y haz commit `docs(NNN): spec y plan actualizados`.
   - **Punto de comprensión.** Muestra las preguntas de comprensión del reviewer y pide al autor que las responda con sus palabras. Dale retroalimentación breve y honesta: qué entendió bien y qué le falta, señalando el archivo o la sección de `learning.md` donde está la respuesta. Si prefiere saltarse este paso, respétalo.
   - **Pull request.** Explica cómo abrirlo: `git push -u origin feature/<feature>` y `gh pr create --fill` (o desde GitHub). Ofrece ejecutarlos, pero solo con su confirmación. El merge a `main` lo hace el autor.
