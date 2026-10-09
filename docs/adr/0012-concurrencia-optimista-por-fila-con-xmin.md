# ADR-0012: Concurrencia optimista por fila con `xmin` en las cuentas

- Estado: Aceptada
- Fecha: 2026-10-05
- Feature: 002-backoffice-clientes-cuentas (resuelve la parte de cambios de estado; **no** resuelve D-02, que decide en el S4 cómo se protege el saldo)

## Contexto

CL-15 y CA-14: si dos operadores cambian el estado de la misma cuenta a la vez, se aplica uno solo, el otro recibe un conflicto y **ninguna operación se pierde en silencio**. En la revisión de la spec, el autor añadió dos matices:

- Al reintentar, la regla (RN-14, RN-06) se **vuelve a evaluar sobre datos frescos**.
- La protección debe ser **por fila y no por columna**. En el S4, un cierre (escribe `estado`) y un depósito (escribe `saldo`) simultáneos tocan columnas distintas. Si solo se vigilara la columna que cada uno escribe, los dos pasarían y quedaría una cuenta Cerrada con saldo. Eso es *write skew*: dos operaciones que, cada una por separado, son correctas, pero que juntas rompen una regla (RN-06).

La spec de producto pide además "concurrencia optimista con versión por cuenta; ante conflicto, reintento acotado y luego 409".

Dos conceptos para leer esta ADR:

- **Concurrencia optimista:** no se bloquea nada al leer. Al guardar se comprueba que la fila sigue en la versión que se leyó (`UPDATE … WHERE id = @id AND version = @versionLeida`). Si otro la cambió entre medias, el `UPDATE` afecta a 0 filas y se informa un conflicto. Analogía: editar un documento compartido que te avisa "alguien guardó cambios desde que lo abriste" en lugar de sobrescribirlos.
- **`xmin`:** columna de sistema que PostgreSQL tiene en todas las tablas, sin crearla. Guarda el id de la transacción que escribió esa versión de la fila, así que **cambia con cualquier `UPDATE` de la fila**, sea cual sea la columna y venga de EF Core o de SQL escrito a mano.

## Opciones consideradas

1. **`xmin` como token de concurrencia** (propiedad sombra `uint` con `IsRowVersion()` en EF Core + Npgsql; ver la [documentación de Npgsql](https://www.npgsql.org/efcore/modeling/concurrency.html)).
   - A favor: protege la fila entera ante **cualquier** escritura, también las que el S4 haga con SQL directo, así que el write skew queda cubierto sin depender de que nadie recuerde incrementar nada. No añade columnas ni migración. El dominio no lo ve.
   - En contra: es específico de PostgreSQL (con SQL Server sería `rowversion`; ADR-0003 ya aceptó PostgreSQL); es una columna invisible, que hay que conocer para entender el SQL; es un contador de 32 bits que da la vuelta, algo irrelevante para comparar dos lecturas cercanas en el tiempo.
2. **Columna `version` explícita** (entero que se incrementa en cada cambio).
   - A favor: portable y visible en el esquema.
   - En contra: **toda** escritura de la fila tiene que incrementarla. Un `UPDATE` atómico del S4 (`SET saldo = saldo + @monto`) que la olvide reabre el write skew sin que ninguna prueba lo note. Requiere más código y una columna.
3. **Bloqueo pesimista** (`SELECT … FOR UPDATE` dentro de una transacción).
   - A favor: no hay conflictos; el segundo espera.
   - En contra: mantiene filas bloqueadas durante la petición, EF Core no lo expresa de forma nativa y el segundo operador acabaría aplicando su cambio sobre un estado que no vio (por ejemplo, "bloquear" una cuenta que el otro acaba de cerrar), lo que contradice CA-14 ("la otra recibe un conflicto").
4. **`UPDATE` condicional sobre la columna** (`… SET estado = 'Cerrada' WHERE id = @id AND estado = 'Activa'`).
   - En contra: solo vigila las columnas del `WHERE`. Un depósito concurrente sobre `saldo` no lo invalida (el write skew que el autor quiere evitar). Además, saca la regla RN-14 del dominio y la lleva al SQL. Esta familia de soluciones es la que D-02 compara para **el saldo** en el S4, no para el estado.

Sobre el reintento:

- **a) 409 directo, sin reintento automático.**
- **b) Reintento automático acotado** (volver a leer y reaplicar hasta N veces).

En un cambio de estado, reintentar en el servidor convierte el conflicto en otra cosa. Si "bloquear" pierde frente a "cerrar", al reintentar ve una cuenta Cerrada y responde 422 (regla rota), así que el operador nunca sabe que hubo una operación concurrente. CA-14 pide explícitamente que la otra operación **reciba un conflicto**, y el autor escribió que la segunda "debe volver a cargar la cuenta antes de continuar": la decisión es de una persona que tiene que ver el estado nuevo.

## Decisión

**Opción 1 (`xmin`) con 409 directo, sin reintento automático (a), en los cambios de estado de `Cuenta`.**

- `cuentas` tiene una propiedad sombra `uint "Version"` mapeada a `xmin` (`IsRowVersion()`, `HasColumnName("xmin")`, `HasColumnType("xid")`). EF Core la incluye en el `WHERE` de cada `UPDATE`.
- `DbUpdateConcurrencyException` se traduce en Infrastructure a `ConflictoDeConcurrenciaException` (Application), que la Api devuelve como 409 `conflicto-de-concurrencia` (ADR-0011).
- `clientes` no lleva token: en la fase 1 un cliente no se modifica nunca.

El motivo principal es que `xmin` es el único mecanismo que protege la fila completa frente a **cualquier** escritura sin depender de la disciplina de quien escribe, y eso es exactamente el write skew que el autor quiere cerrar antes del S4.

## Consecuencias

- **Relación con D-02 (S4).** Esta ADR deja la fila protegida. D-02 decidirá cómo se debitan y acreditan los saldos. En cualquiera de sus opciones, un depósito que modifique la fila cambia `xmin`, así que **un cierre que leyó la cuenta antes del depósito fallará con 409** (no puede quedar una cuenta Cerrada con saldo). Lo demuestra una prueba de integración de la 002 (`F002_CL15_CerrarConVersionViejaTrasCambioDeSaldo_LanzaConflicto`). El caso inverso (el cierre se confirma primero y el depósito llega después) lo debe cubrir el mecanismo de D-02: con EF Core y este mismo token, el depósito recibe un conflicto; con un `UPDATE` condicional, su `WHERE` debe incluir el estado (RN-05).
- **Reintento acotado de la spec de producto.** No se aplica a los cambios de estado. Queda como opción para las operaciones de dinero del S4, donde reintentar en el servidor sí conserva la intención (un débito vuelve a evaluar el saldo) y la decidirá la ADR de D-02. Conviene aclararlo en la spec de producto al escribir esa ADR.
- Las pruebas de concurrencia se escriben **intercalando dos `DbContext`** (los dos leen y luego guardan uno detrás de otro), no con peticiones en paralelo, para que sean deterministas.
- Hay que saber explicar en una entrevista qué es `xmin`, por qué no se ve en el esquema y su equivalente en SQL Server (`rowversion`).
- Hay que vigilar que la migración no intente crear una columna `xmin` o `version`, y que la configuración nombre la columna de forma explícita, porque la convención de snake_case podría renombrarla.

## Decisiones del autor

_Pendiente. Sugerido antes de aceptar: explicar con sus palabras por qué una columna `version` manual podría reabrir el write skew en el S4 y `xmin` no._
