# Guía del flujo SDD con agentes

Este repositorio usa Spec-Driven Development con cuatro agentes de Claude Code. Cada feature pasa por cuatro fases: Spec, Design, Build y Analysis. Entre fase y fase hay una aprobación tuya, y todo lo que se decide queda escrito en `specs/` y `docs/adr/`.

## Instalación

1. Copia el contenido del kit en la raíz del repositorio, donde está la solución (`.sln`). Si ya tenías un `CLAUDE.md`, fusiona el contenido.
2. Requisitos:
   - Git (con Git Bash en Windows).
   - .NET 8 SDK.
   - Node.js en el PATH, porque lo usa el hook del coder.
   - Opcional: GitHub CLI (`gh`) para abrir pull requests desde la terminal.
3. `CLAUDE.md` es una plantilla: completa las partes entre `< >` con el nombre, el propósito y el stack del proyecto. Lo que todavía no decidas, déjalo en "Pendiente de ADR"; la fase Design lo convertirá en ADRs.
4. Abre `claude` en la raíz del repositorio y acepta la confianza del proyecto cuando te la pida. Sin ella no se ejecuta el hook del coder.
5. Comprueba la instalación:
   - `/agents` debe mostrar `planner`, `test-writer`, `coder` y `reviewer`.
   - `/skills` debe mostrar `sdd-spec`, `sdd-design`, `sdd-build`, `sdd-review` y `csharp-clean-code`.
6. En GitHub, crea una regla de protección para `main` que exija pull request.
7. Antes de la primera feature, crea la solución vacía con la estructura de `CLAUDE.md`: `dotnet new sln`, `dotnet new classlib` para Domain, Application e Infrastructure, un proyecto web para Api, un proyecto de pruebas por capa en `tests/` y `dotnet sln add` para cada uno. Hacerlo tú es un buen primer ejercicio para entender la estructura. Si el framework de pruebas o el estilo de API siguen pendientes, xUnit (`dotnet new xunit`) y `dotnet new web` son el punto de partida más común. Si falta algún proyecto, el `test-writer` también puede crearlo cuando lo necesite.

## Una feature de principio a fin

| Paso | Comando | Quién trabaja | Qué produce | Tu parte |
|---|---|---|---|---|
| 1 | `/sdd-spec crear pedido con validación de stock` | Tú + sesión principal | `spec.md` | Responder la entrevista y aprobar |
| 2 | `/sdd-design 001-crear-pedido` | `planner` (opus) | `plan.md`, `tasks.md`, ADRs | Resolver las preguntas abiertas y aprobar |
| 3 | `/sdd-build 001-crear-pedido` | `test-writer` → `coder` (sonnet) | Pruebas, código, `learning.md` | Leer `learning.md` |
| 4 | `/sdd-review 001-crear-pedido` | `reviewer` (opus) | `review.md` | Responder las preguntas de comprensión, abrir el PR y hacer el merge |

El historial de la rama queda así:

```
docs(001): spec aprobada
docs(001): plan y ADRs aprobados
test(001): pruebas en rojo
feat(001): implementación
fix(001): correcciones de la revisión     (solo si hizo falta)
```

Puedes hacer `/clear` entre fases: todo lo que la fase siguiente necesita está en los archivos.

## Carril rápido

Para un bug o un cambio pequeño que no toca contratos públicos no hace falta spec:

1. Activa plan mode (`Shift+Tab`) y pide un test que reproduzca el problema.
2. Aplica el arreglo y comprueba que el test pasa.
3. Ejecuta `/code-review` antes del commit `fix: ...`.

## Cómo aprender con el kit

- **`learning.md`** en cada feature: mapa del código, recorrido de una petición, conceptos clave con analogías y ejercicios Copiar → Modificar → Recrear.
- **Preguntas de comprensión** del reviewer: respóndelas antes de hacer merge. Si no puedes, todavía no es momento de integrar.
- **Los commits cuentan la historia**: `git show` sobre el commit de rojo muestra qué se pidió antes de que existiera el código; el de implementación muestra cómo se resolvió.
- **Progresión sugerida**: después de algunas features, haz tú el Build de una pequeña. Deja que el `test-writer` escriba las pruebas, escribe tú el código hasta ponerlas en verde y pasa por `/sdd-review`.

## Por qué está armado así

- **Comandos** (`.claude/skills/sdd-*`): corren en tu sesión y tienen `disable-model-invocation: true`, así Claude nunca avanza de fase por su cuenta. Desde tu sesión delegan en el subagente de cada fase y luego te piden la aprobación.
- **Agentes** (`.claude/agents/`): cada uno arranca con contexto limpio, con herramientas limitadas a su rol y con la skill `csharp-clean-code` precargada. El `reviewer` no tiene herramientas de edición: solo puede evaluar.
- **Hook** (`.claude/hooks/coder-guard.js`): impide que el `coder` edite pruebas, specs, ADRs o la configuración de `.claude/`. Si una prueba le parece mal, tiene que decirlo en vez de cambiarla.
- **Permisos** (`.claude/settings.json`): `dotnet build`, `dotnet test` y los comandos básicos de git corren sin preguntar; `git push --force` y `git reset --hard` están bloqueados. Hay reglas para Bash y para PowerShell porque en Windows Claude Code usa Git Bash si está instalado y PowerShell si no.
- **`specs/` y `docs/adr/`**: son la memoria compartida entre agentes y, a la vez, la evidencia de tus decisiones para el portafolio.

## Personalizar

- **Modelos**: campo `model` de cada agente (`opus`, `sonnet`, `haiku`). `opus` en `planner` y `reviewer` consume más de tu límite de uso; bájalos a `sonnet` si lo necesitas.
- **Skills de plugins**: si instalaste `dotnet-test` o `dotnet-data`, agrega sus skills a la lista `skills:` del agente que las use. Los nombres exactos aparecen en `/skills`.
- **Versión TypeScript**:
  - Cambia `csharp-clean-code` por `ts-clean-code` en `skills:`.
  - Cambia los comandos `dotnet` por los de npm en agentes, comandos y `settings.json`.
  - Adapta la sección "Esqueletos" del `test-writer`: en TS son funciones que lanzan `new Error('Not implemented')`.

## Límites conocidos

- El hook cubre las herramientas de edición, no los comandos de shell. Por eso el `reviewer` también comprueba con git que las pruebas no cambiaron después del commit de rojo.
- Cada fase arranca subagentes con contexto nuevo, así que el flujo completo gasta más que trabajar en una sola sesión. Empieza con una feature pequeña para medirlo.
- Si el código se aparta de la spec y nadie la actualiza, el siguiente agente trabaja con información falsa. No te saltes el paso de spec viva de la revisión.
