# Roadmap — Core Bancario

Dedicación: más de 15 h por semana. Sprints de 1 semana, salvo S3 y S5 (2 semanas). La fase 1 dura unas 12 semanas.

Cada sprint se ejecuta como 1-3 features del flujo SDD (`/sdd-spec` → `/sdd-design` → `/sdd-build` → `/sdd-review`) que citan los IDs de [spec-fase-1.md](spec-fase-1.md). El frontend vive en un repositorio aparte, `core-bancario-web` ([ADR-0005](../adr/0005-repositorios-separados-backend-y-frontend.md)).

## Fase 1 — Núcleo

Estudia los temas de "Para estudiar" **antes** de empezar cada sprint: son los conceptos que aparecerán en la entrevista de `/sdd-spec` y en las decisiones del planner.

| Sprint | Semanas | Contenido | IDs | Para estudiar |
|---|---|---|---|---|
| ✅ S0 Fundaciones | 1 | Repositorio con kit SDD, ADRs de arquitectura, base de datos, acceso a datos y repositorios; solución vacía; Docker con PostgreSQL; CI; README y C4 nivel 1 | D-01, D-02, D-03, D-10 | GitHub Flow y pull requests · Clean Architecture y la regla de dependencias · Docker Compose básico |
| ✅ S1 Dominio | 1 | `Dinero`, `Moneda`, `Cuenta` y sus estados y `Movimiento` inmutable, con 101 pruebas unitarias; ADR de redondeo (ADR-0007) y de errores de dominio (ADR-0008). Feature 001, PR #3 | RN-01…06, RN-10, RN-14…16, D-07 → ADR-0007 (RN-07 pasa a S5) | Entidades y value objects (DDD táctico) · invariantes de dominio · `decimal` frente a `double` · redondeo bancario (`MidpointRounding.ToEven`) frente a hacia arriba · pruebas unitarias con xUnit (Arrange/Act/Assert) · estudiados además en el diseño: métodos de fábrica · patrón State frente a condicionales · excepciones frente a `Result` |
| ✅ S2 Backoffice API | 1 | Clientes y cuentas (registro, apertura, bloquear/desbloquear/cerrar y consultas) con EF Core sobre PostgreSQL, migración inicial y 398 pruebas, las de integración con Testcontainers; ADRs del dispatcher (ADR-0009), validación (ADR-0010), errores HTTP (ADR-0011), concurrencia con `xmin` (ADR-0012) y número de cuenta (ADR-0013). Feature 002, PR #5 | RF-01…03, RF-05 (parte del operador), RF-12, RN-17, RN-18, D-06 → ADR-0009 | EF Core: `DbContext`, configuración de entidades y migraciones · inyección de dependencias en ASP.NET Core · Minimal APIs · patrón Mediator frente a handlers directos · Testcontainers · UUID v7 frente a v4 como clave primaria · patrón `Result` para validar formularios (ADR-0008) · estudiados además en el diseño y el build: `ProblemDetails` · concurrencia optimista con `xmin` y *write skew* · algoritmo de Luhn · fugas de abstracción (precisión de fechas en PostgreSQL) |
| S3 Auth + front base | 2 | JWT y roles; se crea `core-bancario-web` con el kit adaptado a TypeScript; login, rutas por rol y pantallas del operador; de la revisión de 002: publicar el contrato OpenAPI fuera de Development (o generarlo al compilar) y documentar el `500` en el contrato | RF-05, D-08, D-09 | Autenticación frente a autorización · JWT (estructura, firma, expiración) · OAuth 2.0 y OpenID Connect básicos · React + TypeScript · estado del servidor con TanStack Query · clientes generados desde OpenAPI |
| S4 Ventanilla y extracto | 1 | Depósitos y retiros idempotentes; extracto paginado con Dapper; ADR del mecanismo que protege el saldo; de la revisión de 001: monto máximo de entrada y extracto ordenado por fecha y secuencia (no solo por `Id`) | RF-04, RF-06, RN-11, D-02 (mecanismo) | Concurrencia optimista frente a pesimista · `xmin` en PostgreSQL · `UPDATE` condicional atómico · idempotencia · Dapper y SQL escrito a mano · paginación por offset frente a keyset |
| S5 Transferencias ⭐ | 2 | Transferencia en la misma moneda, reintentos y consulta; escenarios de falla 1-5 | RF-07, RF-09, RF-11, RN-07, RN-08, RN-11, RN-12, D-04, D-05 | Transacciones ACID y niveles de aislamiento · agregados y límites de consistencia (DDD) · claves de idempotencia en APIs de pago · deadlocks y orden de bloqueo · cómo probar concurrencia |
| S6 Front de transferencias | 1 | Formulario en dos pasos, comprobante, clave de idempotencia de punta a punta | RF-07, RF-09, RF-11 | Formularios con React Hook Form y validación con esquemas · errores de API con `ProblemDetails` · UX de confirmación y reintentos seguros |
| S7 Multimoneda | 1 | Tasas versionadas y conversión, backend y frontend; escenarios 6-7; vista previa del frontend redondeando al par (`roundingMode: "halfEven"`, ADR-0007) | RF-08, RF-10, RN-09, RN-13 | Conversión de monedas y dónde se pierde el centavo · datos versionados con fecha de vigencia · formato de moneda por locale (`Intl.NumberFormat`) |
| S8 Observabilidad | 1 | OpenTelemetry, correlation id, métricas; escenario 8 (401/403); accesibilidad | RNF Observabilidad y Seguridad | Los tres pilares: logs, métricas y trazas · OpenTelemetry · logs estructurados · correlation id · accesibilidad web básica (WCAG) |
| S9 Cierre de fase 1 | 1 | README con trade-offs, C4 nivel 2, registro del uso de IA, prueba de clon limpio, demo | RNF Documentación y Operación | Modelo C4 (nivel 2, contenedores) · cómo escribir y presentar trade-offs en una entrevista |

## Fases siguientes

| Fase | Duración estimada | Contenido | Para estudiar |
|---|---|---|---|
| 2 | ~3 semanas | Ledger de doble partida y CQRS: saldos como proyección, extractos por lotes, conciliación diaria | Contabilidad de doble partida · CQRS completo · proyecciones y modelos de lectura · `BackgroundService` en .NET |
| 3 | ~4-5 semanas | Interbancario asíncrono: Outbox, broker, antifraude (Strategy, Specification, Chain of Responsibility), Saga, Polly | Mensajería y brokers (RabbitMQ) · patrón Outbox · Sagas y compensación · resiliencia con Polly · patrones Strategy, Specification y Chain of Responsibility |
| 4 | ~2 semanas | Despliegue en nube emulada gratuita (D-11), trazas y métricas completas, pruebas de carga | Infraestructura en la nube y su emulación local · pruebas de carga (por ejemplo k6) · lectura de métricas bajo carga |

## Al cerrar cada sprint

- Demo corta de lo construido.
- Actualizar este roadmap: qué se terminó, qué se movió.
- Anotar lo aprendido en el `learning.md` de cada feature.
- Revisar si quedó algún tema de "Para estudiar" pendiente y moverlo al sprint siguiente.
