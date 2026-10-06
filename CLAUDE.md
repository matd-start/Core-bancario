# Core Bancario

Core bancario simplificado (backend) con cuentas en COP y USD, transferencias internas idempotentes y saldos consistentes bajo concurrencia, operado por clientes y un backoffice. Es un proyecto de portafolio: además de funcionar, debe dejar evidencia de las decisiones (spec de producto en `docs/producto/`, specs de feature en `specs/`, ADRs en `docs/adr/`).

## Stack

- .NET 10, C# con nullable activado; versión del SDK fijada en `global.json`.
- Arquitectura: Clean Architecture (ADR-0002); los endpoints llaman a los handlers de Application directamente, sin mediador (ADR-0009).
- Persistencia: PostgreSQL (ADR-0003); EF Core para escribir y migrar, Dapper solo para lecturas que lo justifiquen (ADR-0004).
- Pruebas: xUnit v3 sobre Microsoft Testing Platform (ADR-0006); integración contra PostgreSQL real con Testcontainers. Al añadir la primera prueba a un proyecto de tests, quitar su `--ignore-exit-code 8`.
- Frontend en otro repositorio, `core-bancario-web` (ADR-0005); el contrato es el OpenAPI de la API.
- Pendiente de ADR (ver "Decisiones abiertas" en la spec de producto): D-02 mecanismo que protege el saldo (S4), D-04 dos agregados por transacción (S5), D-05 idempotencia (S5), D-08 emisor de tokens (S3), D-11 despliegue (fase 4).

## Estructura

```
src/
  CoreBancario.Domain/          entidades, value objects, reglas de negocio; sin dependencias externas
  CoreBancario.Application/     casos de uso y puertos (interfaces)
  CoreBancario.Infrastructure/  implementación de los puertos: EF Core, Dapper, servicios externos
  CoreBancario.Api/             endpoints delgados que delegan a Application
tests/
  CoreBancario.<Capa>.Tests/    un proyecto de tests por capa
docs/
  producto/                     spec de producto (fuente de verdad de RN y RF) y roadmap
  adr/                          decisiones de arquitectura
specs/                          una carpeta por feature del flujo SDD
```

Las dependencias apuntan hacia dentro: Api e Infrastructure → Application → Domain.

## Comandos

- Compilar: `dotnet build`
- Tests: `dotnet test`
- Base de datos local: `docker compose up -d` (PostgreSQL; variables en `.env`, plantilla en `.env.example`).

## Flujo de trabajo (SDD)

- IMPORTANTE: toda feature pasa por `/sdd-spec` → `/sdd-design` → `/sdd-build` → `/sdd-review`. No se escribe código de producción sin `spec.md` y `plan.md` en `Estado: Aprobado`.
- Carril rápido (bug o cambio pequeño que no cambia contratos públicos): plan mode, un test que reproduzca el problema, el arreglo y `/code-review` antes del commit.
- Cada feature vive en `specs/NNN-slug/`; cada decisión significativa, en `docs/adr/NNNN-titulo.md`, numerada por orden de creación.
- GitHub Flow: `main` es la única rama permanente, está protegida y a ella solo se llega por pull request con el CI en verde. No hay ramas `develop` ni `test`: los ambientes los decide el despliegue, no las ramas.
- Ramas cortas, una por trabajo, y se borran tras el merge: `feature/NNN-slug` (features SDD), `fix/slug` (carril rápido) y `chore/slug` (configuración, infraestructura, dependencias).
- Commits: `tipo(NNN): mensaje` con tipo `docs`, `test`, `feat`, `fix`, `refactor` o `chore`.
- Qué toca cada sprint está en `docs/producto/roadmap.md`.

### IDs de la spec de producto

- Las RN, RF y los escenarios de falla de `docs/producto/spec-fase-1.md` son **globales**. Una feature que los implementa usa el **mismo ID** (por ejemplo RN-01), no crea uno nuevo.
- Si en la entrevista de `/sdd-spec` aparece una regla o requisito nuevo, se añade primero a la spec de producto con el siguiente número libre y luego se cita en la feature.
- CL, CA y RNF específicos de una feature son locales a su `spec.md`.
- Las decisiones abiertas de la spec de producto (D-01…D-11) se resuelven con ADRs; la spec anota qué ADR resolvió cada una.

## Estilo

- Principios de diseño: skill `csharp-clean-code`.
- Dinero siempre con el value object `Dinero` y `decimal`; nunca `double` ni `float`.
- Vocabulario del dominio en español, igual que el lenguaje ubicuo de la spec de producto (`Cuenta`, `Dinero`, `Movimiento`…); el resto del código (infraestructura, sufijos técnicos) puede ir en inglés.
- El autor está aprendiendo arquitectura: explica el porqué de las decisiones no obvias y, al cerrar cada fase, di qué debe revisar él.

## Aprendizaje antes de decidir

IMPORTANTE: el autor no es un programador experto y este proyecto también es su entorno de aprendizaje. Una decisión que no puede defender con sus palabras no sirve para el portafolio.

- Antes de pedirle que elija entre opciones técnicas (en `/sdd-spec`, en las preguntas abiertas del planner, en una ADR o en cualquier conversación), identifica los conceptos que quizá no conoce y explica cada uno en 2-4 líneas: qué es, una analogía simple y por qué importa para esta decisión.
- Si un concepto es grande (por ejemplo CQRS, Outbox o concurrencia optimista), dilo claramente ("antes de decidir conviene estudiar X"), sugiere qué estudiar y ofrece investigarlo juntos. Pregunta si quiere estudiarlo primero o decidir ya.
- Un "sí" rápido a una recomendación no significa que la entienda. En decisiones importantes, pídele que explique el porqué con sus palabras antes de cerrar la ADR, y anótalo en "Decisiones del autor".
