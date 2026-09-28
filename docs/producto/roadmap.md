# Roadmap — Core Bancario

Dedicación: más de 15 h por semana. Sprints de 1 semana, salvo S3 y S5 (2 semanas). La fase 1 dura unas 12 semanas.

Cada sprint se ejecuta como 1-3 features del flujo SDD (`/sdd-spec` → `/sdd-design` → `/sdd-build` → `/sdd-review`) que citan los IDs de [spec-fase-1.md](spec-fase-1.md). El frontend vive en un repositorio aparte, `core-bancario-web` ([ADR-0005](../adr/0005-repositorios-separados-backend-y-frontend.md)).

## Fase 1 — Núcleo

| Sprint | Semanas | Contenido | IDs |
|---|---|---|---|
| S0 Fundaciones | 1 | Repositorio con kit SDD, ADRs de arquitectura, base de datos, acceso a datos y repositorios; solución vacía; Docker con PostgreSQL; CI; README y C4 nivel 1 | D-01, D-02, D-03, D-10 |
| S1 Dominio | 1 | `Dinero`, `Moneda`, `Cuenta` y sus estados, con pruebas unitarias; ADR de redondeo | RN-01…07, RN-10, D-07 |
| S2 Backoffice API | 1 | Clientes y cuentas, persistencia con EF Core, pruebas de integración con Testcontainers; ADR del dispatcher | RF-01…03, D-06 |
| S3 Auth + front base | 2 | JWT y roles; se crea `core-bancario-web` con el kit adaptado a TypeScript; login, rutas por rol y pantallas del operador | RF-05, D-08, D-09 |
| S4 Ventanilla y extracto | 1 | Depósitos y retiros idempotentes; extracto paginado con Dapper; ADR del mecanismo que protege el saldo | RF-04, RF-06, RN-11, D-02 (mecanismo) |
| S5 Transferencias ⭐ | 2 | Transferencia en la misma moneda, reintentos y consulta; escenarios de falla 1-5 | RF-07, RF-09, RF-11, RN-07, RN-08, RN-11, RN-12, D-04, D-05 |
| S6 Front de transferencias | 1 | Formulario en dos pasos, comprobante, clave de idempotencia de punta a punta | RF-07, RF-09, RF-11 |
| S7 Multimoneda | 1 | Tasas versionadas y conversión, backend y frontend; escenarios 6-7 | RF-08, RF-10, RN-09, RN-13 |
| S8 Observabilidad | 1 | OpenTelemetry, correlation id, métricas; escenario 8 (401/403); accesibilidad | RNF Observabilidad y Seguridad |
| S9 Cierre de fase 1 | 1 | README con trade-offs, C4 nivel 2, registro del uso de IA, prueba de clon limpio, demo | RNF Documentación y Operación |

## Fases siguientes

| Fase | Duración estimada | Contenido |
|---|---|---|
| 2 | ~3 semanas | Ledger de doble partida y CQRS: saldos como proyección, extractos por lotes, conciliación diaria |
| 3 | ~4-5 semanas | Interbancario asíncrono: Outbox, broker, antifraude (Strategy, Specification, Chain of Responsibility), Saga, Polly |
| 4 | ~2 semanas | Despliegue en nube emulada gratuita (D-11), trazas y métricas completas, pruebas de carga |

## Al cerrar cada sprint

- Demo corta de lo construido.
- Actualizar este roadmap: qué se terminó, qué se movió.
- Anotar lo aprendido en el `learning.md` de cada feature.
