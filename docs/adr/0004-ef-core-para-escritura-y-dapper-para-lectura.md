# ADR-0004: EF Core para escribir y Dapper para leer

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal (resuelve D-03 de la spec)

## Contexto

El lado de escritura necesita transacciones, control de concurrencia y migraciones: abrir cuentas, depósitos, transferencias. El lado de lectura tiene consultas que no necesitan entidades ni seguimiento de cambios: extracto paginado por fechas (RF-06), listados del backoffice. La fase 2 introduce CQRS, donde lectura y escritura se separan del todo.

## Opciones consideradas

1. **Solo EF Core.** Una sola herramienta; las lecturas se optimizan con `AsNoTracking` y proyecciones. En contra: para consultas de reporte el SQL generado es menos controlable.
2. **EF Core para escribir y Dapper para leer.** Cada herramienta donde rinde mejor, y prepara el terreno para CQRS en la fase 2. En contra: dos herramientas desde el principio y dos formas de mapear la misma tabla.
3. **Solo Dapper.** SQL explícito y control total. En contra: concurrencia, migraciones y mapeo del dominio a mano, justo en las partes más delicadas.

## Decisión

EF Core para todo el lado de escritura (comandos, migraciones, concurrencia) y Dapper para las consultas de lectura que lo justifiquen, empezando por el extracto. El motivo principal es usar EF Core donde protege la consistencia y SQL explícito donde importa el rendimiento de lectura, anticipando la separación de la fase 2.

## Consecuencias

- El esquema lo define y lo migra solo EF Core; Dapper nunca escribe.
- Las consultas Dapper viven en Infrastructure detrás de puertos de lectura de Application, igual que los repositorios.
- Cada consulta Dapper necesita su propia prueba de integración, porque el SQL escrito a mano no lo valida el compilador.
- Si una lectura es sencilla, se hace con EF Core y `AsNoTracking`: Dapper solo entra cuando una consulta concreta lo justifica (YAGNI).
