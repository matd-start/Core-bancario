# ADR-0003: Usar PostgreSQL como base de datos

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal (resuelve la parte de base de datos de D-02 de la spec)

## Contexto

El sistema necesita transacciones ACID para que una transferencia debite y acredite en conjunto (RN-08), un mecanismo de control de concurrencia por cuenta y levantarse con un solo comando en Docker (RNF Operación). El proyecto no tiene presupuesto: todo debe correr gratis en local y, en la fase 4, en una nube emulada.

## Opciones consideradas

1. **PostgreSQL.** Libre, imagen Docker ligera y muy extendida en la nube. EF Core (Npgsql) soporta la columna de sistema `xmin` como token de concurrencia sin columnas extra, y Testcontainers tiene módulo oficial. En contra: menos presente que SQL Server en la banca tradicional.
2. **SQL Server.** Estándar en bancos con .NET y trae `rowversion`. En contra: imagen Docker pesada (~1.5 GB, 2 GB de RAM mínimo) y opciones gratuitas de nube más limitadas.

## Decisión

PostgreSQL, por costo cero, ligereza en Docker y buen soporte en EF Core, Dapper y Testcontainers.

El mecanismo concreto que protege el saldo (concurrencia optimista con `xmin` o `UPDATE` atómico condicional) **no se decide aquí**: se decide con su propia ADR en el Sprint 4, cuando aparecen los débitos concurrentes.

## Consecuencias

- `docker compose up -d` levanta PostgreSQL para desarrollo; las pruebas de integración usan Testcontainers contra la misma imagen.
- Las pruebas de integración corren contra PostgreSQL real, no contra SQLite ni el proveedor en memoria, para que la concurrencia y las transacciones se comporten como en producción.
- En la entrevista hay que saber justificar la elección frente a SQL Server, porque es la pregunta obvia en un contexto bancario.
