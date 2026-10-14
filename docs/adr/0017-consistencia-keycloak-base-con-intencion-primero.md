# ADR-0017: Consistencia entre Keycloak y la base: intención primero

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api, bloque 003b (RF-13, RN-20)

## Contexto

Habilitar, restablecer o deshabilitar un acceso (RF-13) cambia **dos sistemas sin transacción común**: Keycloak, donde vive el usuario con su clave, su OTP y su estado, y nuestra base, donde viven el vínculo (ADR-0016) y el **registro permanente** de quién hizo qué y cuándo (RN-20). Cualquiera de los dos puede fallar a mitad de camino:

- la base cae después de que Keycloak aplicó el cambio;
- Keycloak tarda más que el tiempo de espera de la API, lo aplica de todos modos y la API nunca se entera;
- Keycloak responde con un error claro.

Además:

- la clave temporal debe salir **en la respuesta** al operador (CA-01) y **nunca guardarse** (RNF-03);
- una operación rechazada (cliente inexistente, CL-14) **no deja registro** (CA-04);
- el registro es **inmutable**.

Tres conceptos para leer esta ADR:

- **Intención primero:** antes de actuar en el sistema externo, se guarda y confirma "voy a hacer X". Después se actúa y, por último, se anota cómo terminó. Si algo se rompe en medio, queda una intención sin desenlace: una pista visible que permite averiguar qué pasó. Analogía: el cirujano marca en la historia clínica "voy a operar la rodilla izquierda" antes de entrar al quirófano. Si se va la luz, la historia dice qué se estaba haciendo.
- **Un tiempo de espera agotado no es un "no":** si la API no recibe respuesta, no sabe si el cambio se aplicó. Tratarlo como fallo puede dejar un cambio real sin rastro.
- **Reconciliar:** comparar lo que la base dice que se pidió con lo que el sistema externo muestra, y cerrar las diferencias.

## Opciones consideradas

1. **Keycloak primero, base después.** Simple. En contra: si la base falla después, **hay un cambio real en Keycloak sin ningún rastro** (por ejemplo, un acceso habilitado que nadie puede atribuir a un operador). Es justo lo que RN-20 prohíbe.
2. **Transacción de la base abierta alrededor de la llamada a Keycloak** (insertar, llamar y confirmar). Parece atómica, pero no lo es. Ante un tiempo de espera o una caída después de que Keycloak aplicó el cambio, el `ROLLBACK` **borra la evidencia de un cambio que sí ocurrió**. Además, retiene una conexión y bloqueos mientras se espera a un sistema externo.
3. **Outbox** (guardar la operación en la base y que un proceso aparte la ejecute en Keycloak). Es fiable para operaciones sin respuesta inmediata. En contra: la clave temporal tiene que salir en la respuesta (CA-01) y no puede guardarse en la base para que otro proceso la use (RNF-03). Además, exige construir el proceso en segundo plano.
4. **Compensación** (si la base falla, deshacer en Keycloak). En contra: deshacer no siempre es seguro. Deshacer un "deshabilitar" **volvería a habilitar a un cliente cuya clave fue robada**, y la propia compensación también puede fallar.
5. **Intención primero.** Validar → confirmar en la base una solicitud → llamar a Keycloak → escribir el resultado en **otra fila**. Un fallo después de confirmar la solicitud deja una solicitud **sin resultado**: detectable y reconciliable.

**Evidencia:** un experimento con Keycloak real y PostgreSQL probó las tres estrategias principales contra tres fallas (caída de la base después de Keycloak, tiempo de espera con el cambio aplicado y error de Keycloak). **Solo "intención primero" evitó cambios sin rastro en las tres.**

## Decisión

**Opción 5, intención primero**, con estas reglas:

1. **Validar sin escribir.** El cliente existe y la regla (CL-14) se cumple. Si algo falla, **no se registra nada** (CA-04).
2. **Confirmar la solicitud** en `solicitudes_de_acceso` (cliente, operador = `sub` del token, acción, fecha) con su propio `SaveChanges`. A partir de aquí, la operación usa `CancellationToken.None`, acotada por el tiempo de espera del `HttpClient`, para que una desconexión del operador no la deje a medias.
3. **Llamar a Keycloak** (Admin REST API, credencial de ADR-0014).
4. **Escribir el resultado** en `resultados_de_acceso`, una fila nueva vinculada a la solicitud y nunca una edición:
   - con respuesta correcta → **Completado** (junto con el vínculo, si faltaba, en el mismo `SaveChanges`);
   - con una respuesta de error clara de Keycloak → **Fallido**, con un motivo técnico sin el cuerpo de la respuesta, y el operador recibe 502;
   - **sin respuesta** (tiempo de espera o conexión) → **no se escribe resultado**: la solicitud queda **pendiente** y el operador recibe 503 ("quedó pendiente de confirmar; vuelve a intentarlo").

La regla para distinguir los dos últimos casos es sencilla: **¿llegó una respuesta HTTP?**

**El registro es inmutable.** Solo hay inserciones. La PK de `resultados_de_acceso` es `solicitud_id`, así que hay como máximo un resultado por solicitud. Un trigger `BEFORE UPDATE OR DELETE` rechaza cualquier edición o borrado (defensa en profundidad).

**El estado del acceso vive solo en Keycloak** (habilitado, segundo factor inscrito). Hay una sola fuente de verdad por dato. La base guarda el vínculo y el registro. CL-13 ("habilitar uno ya habilitado es restablecer") y CL-14 ("deshabilitar uno ya deshabilitado se rechaza") consultan Keycloak.

**Reconciliación:**

- **(a) Al reintentar.** Antes de decidir nada, el handler busca las solicitudes **pendientes del mismo cliente** y las cierra según lo que muestra Keycloak. Así se resuelve el caso más común (el operador reintenta tras un 503) sin infraestructura adicional.
- **(b) Consulta documentada** de "solicitudes sin resultado" (`LEFT JOIN … WHERE r.solicitud_id IS NULL`), en el README y con su prueba de integración, para tener visibilidad sin un proceso en segundo plano.

**Cómo sabe la reconciliación si una solicitud se aplicó: la marca.** *(Propuesta del planner para concretar "consulta Keycloak y la cierra"; el autor la confirma al aprobar el plan.)*

- Cada operación escribe en el usuario de Keycloak el atributo `ultima_solicitud_de_acceso = <Id de la solicitud>` **en su último paso**.
  - En "crear", todo va en una sola petición.
  - En "restablecer", el orden es: clave → borrar OTP → cerrar sesiones → habilitado + acciones + marca.
  - En "deshabilitar": cerrar sesiones → deshabilitado + marca.
- La regla es una sola para las tres acciones: **si la marca coincide con la solicitud pendiente, se aplicó entera → Completado; si no, no se completó → Fallido.**
- El atributo se declara en el perfil de usuario del realm como **visible y editable solo por `admin`**. El usuario no puede verlo ni cambiarlo, y no se usa para autorizar (eso es ADR-0016).
- Sin la marca, para "deshabilitar" bastaría con mirar `enabled`. Pero para "habilitar" o "restablecer" no hay forma fiable de saber si *esa* solicitud cambió la clave.

**Idempotencia de habilitar.** Si existe un usuario en Keycloak sin vínculo en la base (resto de un habilitar interrumpido), **se adopta** en vez de crearse otro: se restablece y se crea el vínculo.

Como habilitar y restablecer generan siempre una clave nueva, **el reintento de un habilitar responde con otra clave temporal**. La del intento anterior nunca llegó a nadie y deja de servir.

## Consecuencias

- **Cada fallo deja una solicitud sin resultado, nunca un cambio sin rastro.** El peor caso es una solicitud pendiente hasta el siguiente intento del operador o hasta que alguien ejecute la consulta (b).
- Un reintento de "deshabilitar" que ya se había aplicado responde 422 `acceso-no-habilitado`. El 422 es correcto, y no a ciegas: antes se cerró la solicitud anterior como Completada. Ese 422 no crea una solicitud nueva.
- El operador ve dos códigos nuevos con significados distintos:
  - 502 `identidad-error`: no se completó, reintenta;
  - 503 `identidad-sin-respuesta`: quedó pendiente, reintenta y el sistema lo resolverá.
- Las pruebas de Application usan un proveedor falso que puede responder con error, no responder, o **aplicar y no responder**. Este último es el caso que distingue esta estrategia de las demás (ADR-0018).
- **Límites aceptados:**
  - dos operadores sobre el mismo cliente a la vez: los dos quedan registrados, la última clave gana, y si chocan en un índice único uno recibe 409;
  - si Keycloak termina de aplicar *después* de que el reintento cerró la solicitud como Fallida, el registro diría Fallido de algo aplicado. Exige una petición de más de 10 s seguida de un reintento inmediato, y los eventos de administración de Keycloak dejan el rastro para investigarlo;
  - un usuario borrado a mano en Keycloak con el vínculo vivo da 500 y se corrige a mano.
- **Mejora futura:** un `BackgroundService` que ejecute la reconciliación (b) de forma periódica. Hoy queda fuera de alcance.

## Decisiones del autor

Por qué intención primero, en sus palabras: *"convierte los fallos de consistencia en situaciones detectables y reconciliables. La opción 2 reduce mucho el problema, pero todavía puede producir cambios reales en Keycloak sin evidencia persistida en la base cuando ocurre un timeout o un fallo después de que Keycloak haya aplicado el cambio"*.

Sobre la reconciliación (a): *"porque resuelve el caso más habitual sin infraestructura adicional"*. Sobre la (b): *"porque aporta visibilidad sin tener que construir todavía un proceso en segundo plano"*.

Evidencia: el experimento con Keycloak real y PostgreSQL (3 estrategias × 3 fallas) en el que solo "intención primero" evitó cambios sin rastro en las tres fallas.

Pendiente de confirmar al aprobar el plan: el mecanismo de **la marca** (`ultima_solicitud_de_acceso` en el último paso) como forma concreta de "consultar Keycloak y cerrar" la solicitud pendiente.
