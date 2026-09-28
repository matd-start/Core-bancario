---
name: sdd-build
description: Fase Build del flujo SDD. El subagente test-writer escribe las pruebas en rojo y después el subagente coder implementa hasta que pasen.
argument-hint: "[NNN-slug]"
disable-model-invocation: true
---

# Fase Build: rojo → verde

Feature: $ARGUMENTS (carpeta `specs/$ARGUMENTS/`).

## Pasos

1. Verifica que `plan.md` dice `Estado: Aprobado`. Si no, detente y sugiere `/sdd-design`.

2. Comprueba con `git status` que estás en la rama `feature/<feature>` y que no hay cambios sin commit. Si los hay, pregunta al autor qué hacer antes de seguir.

3. **Rojo.** Delega al subagente `test-writer` con la ruta de la feature.

4. **Verifica tú el rojo** con `dotnet build` y `dotnet test`:
   - la solución compila;
   - los tests nuevos fallan por `NotImplementedException` o por una aserción, no por errores de compilación;
   - los tests anteriores siguen pasando.

   Si algo no se cumple, reanuda al test-writer con SendMessage explicando el problema.

5. Haz commit `test(NNN): pruebas en rojo`. El reviewer usará este commit para comprobar que nadie cambió las pruebas después.

6. **Verde.** Delega al subagente `coder` con la ruta de la feature.

7. Ejecuta `dotnet test`. Si falla algo, reanuda al coder con SendMessage pasándole los tests que fallan y sus mensajes de error. Máximo 3 rondas; si sigue fallando, detente y explica al autor qué ocurre.

8. Si el coder reporta que un test o el plan le parecen incorrectos, no lo resuelvas tú: muéstraselo al autor y que decida si se corrige la prueba (con el test-writer) o se vuelve a Design.

9. Con todo en verde, haz commit `feat(NNN): implementación`.

10. **Resumen para el autor**: tareas completadas, número de tests y su resultado, archivos principales y la ruta de `learning.md`. Recomiéndale leer `learning.md` antes de la revisión: ahí está explicado lo que se construyó.

11. Indica el siguiente paso: `/sdd-review <feature>`.
