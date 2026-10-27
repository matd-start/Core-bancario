---
name: sdd-build
description: Fase Build del flujo SDD. Comprueba que lo siguiente toca en este repo; después el subagente test-writer escribe las pruebas en rojo y el subagente coder implementa hasta que pasen.
argument-hint: "[NNN-slug]"
disable-model-invocation: true
---

# Fase Build: rojo → verde

Feature: $ARGUMENTS (carpeta `specs/$ARGUMENTS/`).

## Pasos

1. Verifica que `plan.md` dice `Estado: Aprobado`. Si no, detente y sugiere `/sdd-design`.

2. **¿Lo siguiente toca aquí?** Busca en `tasks.md` la primera tarea sin `[x]`.
   - Si su `Repo:` es `core-bancario-web`, mira si el PR del web que la contiene ya se mergeó: `gh pr list -R matd-start/core-bancario-web --state merged` y `construccion/$ARGUMENTS/avance.md` en el `main` del web (`../core-bancario-web`, actualizado con `git -C ../core-bancario-web pull`).
     - **Mergeado:** muestra al autor las tareas que lista `avance.md` y, con su visto bueno, márcalas `[x]` aquí en un commit `docs(NNN): tareas del web hechas (core-bancario-web#N)`. Vuelve a empezar este paso.
     - **Sin mergear:** detente y dile al autor: "La siguiente tarea es Txx, del web. Abre Claude en `core-bancario-web` (`C:\Users\miguel\source\repos\core-bancario-web`) y ejecuta allí `/sdd-build $ARGUMENTS`."
   - Si es de este repo (o la tarea no tiene `Repo:`), el **PR en curso** es la sección de `tasks.md` que la contiene (por ejemplo, "PR 1 de 004a — repo `core-bancario`"); si `tasks.md` no tiene secciones por PR, son todas las tareas. Dile al autor qué sección vas a construir.

3. Comprueba con `git status` que estás en la rama de la feature que nombra el plan (por defecto `feature/<feature>`) y que no hay cambios sin commit. Si los hay, pregunta al autor qué hacer antes de seguir.

4. **Rojo.** Delega al subagente `test-writer` con la ruta de la feature y el título de la sección del PR en curso.

5. **Verifica tú el rojo** con `dotnet build` y `dotnet test`:
   - la solución compila;
   - los tests nuevos fallan por `NotImplementedException` o por una aserción, no por errores de compilación;
   - los tests anteriores siguen pasando.

   Si algo no se cumple, reanuda al test-writer con SendMessage explicando el problema.

6. Haz commit `test(NNN): pruebas en rojo` (con `del bloque <bloque>` si el plan va por bloques). El reviewer usará este commit para comprobar que nadie cambió las pruebas después.

7. **Verde.** Delega al subagente `coder` con la ruta de la feature y el título de la sección del PR en curso.

8. Ejecuta `dotnet test`. Si falla algo, reanuda al coder con SendMessage pasándole los tests que fallan y sus mensajes de error. Máximo 3 rondas; si sigue fallando, detente y explica al autor qué ocurre.

9. Si el coder reporta que un test o el plan le parecen incorrectos, no lo resuelvas tú: muéstraselo al autor y que decida si se corrige la prueba (con el test-writer) o se vuelve a Design.

10. Con todo en verde, haz commit `feat(NNN): implementación` (con `del bloque <bloque>` si corresponde).

11. **Resumen para el autor**: tareas completadas, número de tests y su resultado, archivos principales y la ruta de `learning.md`. Recomiéndale leer `learning.md` antes de la revisión: ahí está explicado lo que se construyó.

12. Indica el siguiente paso: `/sdd-review <feature>`.
