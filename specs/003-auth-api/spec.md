# 003 — Autenticación y autorización en la API

Estado: Aprobado (2026-10-09)
Fecha: 2026-10-09

## 1. Contexto y objetivo

La 002 dejó una API de backoffice que cualquiera puede llamar: no sabe quién la usa ni qué puede hacer. Esta feature protege la API. Cada petición tiene que identificar quién la hace y su rol, Cliente u Operador. El operador gestiona el acceso digital de los clientes, y el cliente entra con su documento, su clave y un segundo factor para ver solo sus cuentas. Es la base sobre la que la 004 (front base, en `core-bancario-web`) construye el inicio de sesión y las rutas por rol, y sobre la que el S5 aplicará RN-12 (un cliente solo transfiere desde sus cuentas).

## 2. Actores

- **Cliente:** inicia y cierra sesión con su documento, su clave y un segundo factor; cambia su clave; consulta sus cuentas y saldos (RF-05).
- **Operador de backoffice:** inicia y cierra sesión con su usuario de empleado, su clave y un segundo factor; habilita, restablece y deshabilita el acceso digital de los clientes (RF-13); sigue haciendo todo lo de la 002, ahora identificado.
- **Administrador del sistema de identidad (fuera de la API):** da de alta y de baja a los operadores. No es un rol de la API; ningún RF de la fase 1 pide gestionar operadores desde ella.

Una credencial tiene un solo rol (RN-19). Un empleado que también es cliente del banco tiene dos credenciales distintas.

## 3. Requisitos funcionales

IDs globales de `docs/producto/spec-fase-1.md`. RF-13, RF-14 y RF-15 se añadieron allí a partir de esta entrevista.

- RF-05 (parte del cliente): el cliente consulta la lista de sus cuentas, incluidas las Cerradas, con su estado, y el detalle de una de ellas. Pedir una cuenta ajena responde igual que una inexistente. La parte del operador ya se hizo en la 002.
- RF-13: Gestionar el acceso digital de un cliente. El operador **habilita** el acceso de un cliente registrado, lo **restablece** (clave olvidada o celular perdido) o lo **deshabilita** (por ejemplo, si el cliente reporta que le robaron la clave). Habilitar y restablecer entregan una clave temporal nueva; cada operación queda registrada con quién la hizo y cuándo (RN-20).
- RF-14: Iniciar y cerrar sesión. Cliente y operador inician sesión con su usuario, su clave y un segundo factor (RN-24). En el primer ingreso con una clave temporal, el usuario debe cambiar la clave e inscribir el segundo factor antes de cualquier otra cosa (RN-21). Cerrar sesión termina la sesión.
- RF-15: Cambiar la clave. El cliente cambia su clave cuando quiera, además del cambio obligatorio del primer ingreso.

Las operaciones de la 002 (RF-01, RF-02, RF-03, RF-12 y la consulta de cuenta del operador) pasan a exigir el rol Operador.

## 4. Datos

| Entidad | Campo | Tipo | Obligatorio | Reglas |
|---|---|---|---|---|
| Acceso digital | Cliente | Referencia a `Cliente` | Sí | Un cliente tiene como máximo un acceso digital. Solo existe para clientes registrados. |
| Acceso digital | Usuario | Documento | Sí | El tipo y el número de documento del cliente, normalizados como en RN-17. No se elige ni cambia. |
| Acceso digital | Estado | Habilitado · Deshabilitado | Sí | Solo el operador lo cambia (RN-20). |
| Acceso digital | Segundo factor | Inscrito · Sin inscribir | Sí | Nace sin inscribir; restablecer el acceso lo borra. |
| Clave | Texto | — | Sí | Mínimo 12 caracteres y máximo 64 (propuesto). No puede contener el número de documento (propuesto). Nunca se guarda ni se muestra en texto plano. |
| Clave temporal | Texto | — | Sí | La genera el sistema al azar, con 16 caracteres como mínimo (propuesto). Se muestra **una sola vez**, en la respuesta al operador. Sirve solo para el primer ingreso y vence a las 72 h si no se usa (propuesto). |
| Registro de acceso | Cliente | Referencia a `Cliente` | Sí | Cliente afectado. |
| Registro de acceso | Operador | Identificador del operador | Sí | Quién hizo la operación; sale de su sesión, no de la petición. |
| Registro de acceso | Acción | Habilitar · Restablecer · Deshabilitar | Sí | — |
| Registro de acceso | Fecha | Instante | Sí | La pone el sistema. |
| Operador | Usuario, rol | — | — | Vive fuera de la API, en el sistema de identidad. La API solo conoce su identificador y su rol por la sesión. |

Los registros de acceso son inmutables: no se editan ni se borran.

## 5. Reglas de negocio

IDs globales de `docs/producto/spec-fase-1.md`. RN-19 a RN-24 se añadieron allí a partir de esta entrevista.

- RN-12: Un cliente solo usa como origen sus propias cuentas. En esta feature se aplica a la consulta: un cliente solo ve sus cuentas.
- RN-17: El documento se normaliza antes de comparar. Esto aplica también al usuario con el que el cliente inicia sesión.
- RN-19: Una credencial tiene un solo rol, Cliente u Operador. El cliente inicia sesión con su documento; el operador, con su usuario de empleado. La identidad de quien hace una operación sale siempre de su sesión, nunca de los datos de la petición.
- RN-20: Solo el operador habilita, restablece o deshabilita el acceso digital de un cliente. Habilitar y restablecer generan una clave temporal nueva y borran el segundo factor inscrito; restablecer y deshabilitar terminan las sesiones abiertas del cliente. Cada operación queda registrada de forma permanente con el cliente, el operador, la acción y la fecha.
- RN-21: Una clave temporal sirve solo para el primer ingreso. Con ella, el usuario solo puede cambiar su clave e inscribir su segundo factor; no accede a nada más hasta completar ambos pasos.
- RN-22: Una sesión termina por inactividad o por vida máxima, lo que ocurra primero. Cliente: 5 min de inactividad y 30 min de vida máxima. Operador: 15 min de inactividad y 9 h de vida máxima.
- RN-23: Tras 5 intentos fallidos seguidos de inicio de sesión, la credencial se bloquea 15 min y luego se libera sola.
- RN-24: El segundo factor es obligatorio para clientes y operadores: una clave dinámica de 6 dígitos que genera una app autenticadora en el celular del usuario (estándar TOTP). Se inscribe en el primer ingreso.

## 6. Requisitos no funcionales

- RNF-01: Desde que se deshabilita un acceso (o se da de baja a un operador) hasta que la API rechaza su última petición pasan como máximo **5 min, contando la tolerancia de reloj**. Es un límite conocido y aceptado (ver sección 11).
- RNF-02: Las respuestas distinguen los tres casos del escenario de falla 8 de la spec de producto: sin sesión válida (sin credencial, vencida, con firma inválida o emitida para otra API) → no autenticado (401); rol incorrecto → prohibido (403); un cliente que pide una cuenta ajena → no encontrado, **idéntico** a una cuenta inexistente.
- RNF-03: Ninguna clave, clave temporal, secreto del segundo factor ni credencial de sesión aparece en logs, respuestas de error ni en el repositorio. La clave temporal solo aparece en la respuesta de la operación que la genera.
- RNF-04 (propuesto): Cada petición rechazada como no autenticada deja en el log el motivo (vencida, firma inválida, emisor o audiencia incorrectos), sin la credencial. La respuesta al cliente no revela el motivo.
- RNF-05: Cada operación del operador que cambia datos (registrar cliente, abrir cuenta, cambiar estado, gestionar acceso) deja en el log el identificador del operador que la hizo.
- RNF-06: El contrato OpenAPI está disponible fuera de Development (publicado o generado y versionado), describe cómo autenticarse y documenta en cada operación sus respuestas 401 y 403, y el error inesperado 500 (tareas de la review 002).
- RNF-07: Cada criterio de aceptación de la sección 8 tiene al menos una prueba automatizada que se vio en rojo antes de implementarla.
- RNF-08 (propuesto): Al menos una prueba de punta a punta recorre el flujo real (iniciar sesión con el sistema de identidad → llamar a la API). Las demás pruebas de la API no necesitan arrancar el sistema de identidad real.

## 7. Errores y casos límite

**Iniciar sesión**

- CL-01: Documento inexistente, clave incorrecta, acceso deshabilitado o credencial bloqueada por intentos fallidos responden **el mismo mensaje genérico** (propuesto), para no revelar si un documento es cliente del banco.
- CL-02: El documento se normaliza al iniciar sesión: `1.234.567` y `1234567` son el mismo usuario (RN-17).
- CL-03: Un código del segundo factor incorrecto o vencido cuenta como intento fallido (RN-23).
- CL-04: Con una clave temporal, cualquier operación distinta de cambiar la clave e inscribir el segundo factor se rechaza (RN-21).
- CL-05: Una clave temporal vencida (más de 72 h sin usar, propuesto) ya no sirve; el operador debe restablecer el acceso.
- CL-06: La nueva clave que no cumple la política (sección 4) se rechaza indicando qué regla rompe.

**Sesión**

- CL-07: Sin credencial, con una credencial vencida, con firma inválida o emitida para otra API, toda operación protegida responde no autenticado (RNF-02).
- CL-08: Un cliente que llama una operación del operador (registrar cliente, abrir cuenta, gestionar acceso…) recibe prohibido, sin importar si el recurso existe.
- CL-09: Un operador que llama "mis cuentas" recibe prohibido: esa operación es solo del rol Cliente.
- CL-10: Después de cerrar sesión, la sesión no se puede renovar. La credencial de acceso que ya se entregó sigue siendo aceptada hasta que vence (≤ 5 min, RNF-01).
- CL-11: Pasado el tiempo de inactividad o la vida máxima (RN-22), la sesión no se renueva y el usuario debe iniciar sesión de nuevo.

**Gestionar acceso (operador)**

- CL-12: Habilitar el acceso de un cliente que no existe responde no encontrado y no registra nada.
- CL-13: Habilitar el acceso de un cliente que ya lo tiene habilitado es un **restablecimiento**: nueva clave temporal, segundo factor borrado, sesiones terminadas y registro con la acción Restablecer.
- CL-14: Deshabilitar un acceso que no existe o que ya está deshabilitado se rechaza como regla de negocio rota (propuesto, igual que repetir un estado en RN-14).
- CL-15: Después de deshabilitar o restablecer, el cliente no puede renovar su sesión; su credencial de acceso vigente se acepta como máximo hasta 5 min (RNF-01).
- CL-16: Un acceso deshabilitado se vuelve a habilitar con la acción Habilitar, que entrega una clave temporal nueva.

**Consultar cuentas (cliente)**

- CL-17: Un cliente que consulta una cuenta ajena recibe exactamente la misma respuesta que con una cuenta inexistente.
- CL-18: Un cliente sin cuentas ve su lista vacía. Un cliente con cuentas Cerradas las ve con su estado.

## 8. Criterios de aceptación

**RF-13 Gestionar acceso**

- CA-01 (RF-13, RN-20): Dado un cliente registrado sin acceso, cuando el operador lo habilita, entonces la respuesta contiene una clave temporal, el acceso queda Habilitado con el segundo factor sin inscribir y queda un registro con el cliente, el operador, la acción Habilitar y la fecha.
- CA-02 (RF-13, RN-20, CL-13): Dado un cliente con el acceso habilitado y el segundo factor inscrito, cuando el operador lo habilita de nuevo, entonces recibe una clave temporal distinta, el segundo factor queda sin inscribir y se registra la acción Restablecer.
- CA-03 (RF-13, RN-20, CL-15): Dado un cliente con una sesión abierta, cuando el operador deshabilita su acceso, entonces la sesión no se puede renovar, el cliente no puede iniciar sesión de nuevo y se registra la acción Deshabilitar.
- CA-04 (RF-13, CL-12, CL-14): Dado un id de cliente inexistente, cuando el operador intenta habilitarlo, entonces responde no encontrado; y dado un acceso ya deshabilitado, cuando lo deshabilita de nuevo, se rechaza como regla de negocio rota. En ningún caso se crea un registro.
- CA-05 (RN-19, RNF-02, CL-08): Dado un usuario con rol Cliente, cuando intenta habilitar el acceso de cualquier cliente, entonces recibe prohibido y no se registra nada.

**RF-14 Iniciar y cerrar sesión**

- CA-06 (RF-14, RN-21, RN-24): Dado un cliente con clave temporal, cuando inicia sesión por primera vez, entonces debe cambiar su clave e inscribir el segundo factor; después, al iniciar sesión con su clave nueva y su código, obtiene una sesión con rol Cliente.
- CA-07 (RF-14, RN-21, CL-04): Dado un cliente que inició sesión con una clave temporal y aún no completó los dos pasos, cuando consulta sus cuentas, entonces la operación se rechaza.
- CA-08 (RF-14, CL-01): Dados un documento inexistente, una clave incorrecta y un acceso deshabilitado, cuando se intenta iniciar sesión con cada uno, entonces los tres reciben el mismo mensaje genérico.
- CA-09 (RN-23, CL-03): Dado un cliente con acceso habilitado, cuando falla 5 veces seguidas, entonces el sexto intento con la clave y el código correctos se rechaza; y pasados 15 min, el inicio de sesión vuelve a funcionar.
- CA-10 (RN-22, CL-11): Dado un cliente con sesión, cuando pasan más de 5 min sin actividad, entonces la sesión no se puede renovar; y dado un operador con sesión, eso ocurre a los 15 min de inactividad.
- CA-11 (RF-14, CL-10): Dado un usuario con sesión, cuando cierra sesión, entonces la sesión no se puede renovar.
- CA-12 (RN-19, CL-02): Dado un cliente con documento CC 1234567, cuando inicia sesión escribiendo `1.234.567`, entonces entra como ese cliente.

**RF-15 Cambiar clave**

- CA-13 (RF-15, CL-06): Dado un cliente con sesión, cuando cambia su clave dando la actual y una nueva válida, entonces el siguiente inicio de sesión funciona con la nueva y no con la anterior; y una clave nueva que rompe la política se rechaza indicando la regla.

**RF-05 Consultar cuentas (cliente)**

- CA-14 (RF-05, CL-18): Dado un cliente con una cuenta Activa y una Cerrada, cuando consulta "mis cuentas", entonces obtiene las dos con número, moneda, estado y saldo, y ninguna cuenta de otro cliente.
- CA-15 (RF-05, RN-12, CL-17): Dadas una cuenta de otro cliente y un id de cuenta inexistente, cuando el cliente consulta cada una, entonces las dos respuestas son idénticas (no encontrado), salvo el identificador de correlación.
- CA-16 (RF-05): Dada una cuenta propia, cuando el cliente la consulta por su id, entonces obtiene número, moneda, estado, saldo y fechas.
- CA-17 (RF-05, CL-18): Dado un cliente sin cuentas, cuando consulta "mis cuentas", entonces obtiene una lista vacía.

**Protección de la API**

- CA-18 (RNF-02, CL-07): Dada cada operación protegida, cuando se llama sin credencial, con una vencida, con una de firma inválida o con una emitida para otra API, entonces responde no autenticado.
- CA-19 (RNF-02, CL-08, CL-09): Dado un usuario con rol Cliente, cuando llama cada operación del operador de la 002, entonces responde prohibido; y dado un operador, cuando llama "mis cuentas", responde prohibido.
- CA-20 (RF-01…03, RF-12): Dado un operador con sesión, cuando usa las operaciones de la 002, entonces funcionan como en la 002 y el log de cada operación que cambia datos incluye su identificador (RNF-05).
- CA-21 (RNF-01): Dada una credencial de acceso, cuando han pasado 5 min desde su emisión, entonces la API la rechaza aunque los relojes difieran dentro de la tolerancia configurada.
- CA-22 (RNF-06): Dado el contrato OpenAPI, cuando se obtiene fuera de Development, entonces describe el esquema de autenticación y las respuestas 401, 403 y 500 de cada operación protegida.

## 9. Fuera de alcance

- Recuperar la clave olvidada sin el operador (por correo o SMS). La recuperación es el restablecimiento de RF-13.
- Entregar la clave temporal por un canal que el operador no ve (correo, SMS, sobre). Es un **límite conocido** (sección 11).
- Códigos de respaldo del segundo factor y otros factores (SMS, correo, llaves de seguridad o passkeys).
- Gestionar operadores desde la API, un rol Administrador y cualquier rol más allá de Cliente y Operador.
- Revocación inmediata de las credenciales de acceso (RNF-01).
- Autorregistro del cliente.
- Consultar el registro de accesos desde la API (queda guardado y se consulta en la base).
- Guardar en la base quién hizo las operaciones de la 002 (solo queda en el log, RNF-05).
- Ver y cerrar las sesiones abiertas en otros dispositivos.
- Frontend: pantalla de inicio de sesión, rutas por rol y manejo de tokens en la SPA (feature 004, en `core-bancario-web`).
- Extracto (RF-06, S4) y transferencias (S5).
- Observabilidad completa (correlation id de punta a punta, métricas, trazas): S8.

## 10. Preguntas para Design

- **D-08 (ADR): quién emite las credenciales.** Keycloak en Docker · la propia API · ASP.NET Core Identity. **Esta spec pesa en la decisión:** el segundo factor TOTP obligatorio con inscripción en el primer ingreso (RN-24), el bloqueo temporal (RN-23), las sesiones con dos relojes (RN-22) y las claves temporales (RN-21) son funciones que un emisor dedicado puede traer listas y que la API tendría que construir y asegurar por su cuenta. Hallazgos del experimento con Keycloak (repaso del S3): el primer arranque tardó 188 s; los roles vienen en `realm_access.roles` y ASP.NET no los mapea solo; el *audience mapper* es imprescindible; el `kid` permite rotar claves.
- **Vínculo entre la identidad y el `Cliente`:** el identificador del usuario en el emisor (`sub`) no es el `ClienteId`. ¿Cómo se relacionan y quién guarda el vínculo?
- **RNF-01 con la tolerancia de reloj:** el `ClockSkew` por defecto de ASP.NET es de 5 min, lo que haría la ventana real de hasta 10. Fijar la vida de la credencial de acceso y la tolerancia para que sumen ≤ 5 min. Fijar también `ValidAlgorithms`.
- **Dónde vive la autorización por propiedad (RN-12):** filtrar por dueño en la consulta (BOLA/IDOR, OWASP API1) frente a comprobar después de cargar. Debe responder igual en los dos casos de CA-15.
- **Registro de acceso (RN-20):** ¿tabla propia en PostgreSQL? ¿Cómo se garantiza que el cambio en el emisor y el registro en la base no queden a medias si uno de los dos falla (dos sistemas, sin transacción común)?
- **Gestionar acceso desde la API:** si el emisor es externo, la API necesita llamarlo para crear usuarios, poner claves temporales y deshabilitarlos. ¿Con qué credencial de servicio y con qué permisos mínimos? ¿Cómo se prueba sin arrancar el emisor real (RNF-08)?
- **Pruebas:** firmar credenciales de prueba con una clave RSA propia para las pruebas de la API y dejar el emisor real para la prueba de punta a punta (RNF-08). Una de las pruebas de la 002 que se adapte para usar sesión sigue llamándose `F002_…`; el comportamiento nuevo va en pruebas `F003_…`.
- **`RequireHttpsMetadata`:** desactivado solo en Development.
- **OpenAPI fuera de Development (RNF-06):** ¿publicarlo en producción o generar `openapi.json` al compilar (`Microsoft.Extensions.ApiDescription.Server`) y versionarlo? Añadir `ProducesProblem(500)`.
- **Rutas nuevas:** "mis cuentas" (`/mis-cuentas` o `/clientes/yo/cuentas`); ¿el cliente usa la misma ruta `/cuentas/{id}` que el operador?
- **Logs del motivo del 401 (RNF-04):** eventos de `JwtBearerEvents` u otra vía.

## 11. Decisiones del autor

Entrevista del 2026-10-09.

- **Acceso digital como acción aparte del registro (RF-13):** sirve para los clientes ya registrados en la 002 y para restablecer una clave olvidada o un celular perdido.
- **Clave temporal generada por el sistema**, que el cliente cambia en su primer ingreso, sin servicio de correo. En sus palabras: *"Un operador con prisa tenderá a inventar claves fáciles de recordar, patrones repetidos o variaciones predecibles. Un generador del sistema puede crear claves aleatorias con una complejidad uniforme para todos los usuarios. La diferencia es que el sistema no elige claves pensando en comodidad, sino en seguridad"*. Matiz añadido: además, una persona apurada tiende a usar la misma clave para todos, y si se filtra una caen todas.
- **Operadores gestionados fuera de la API:** ningún RF lo pide; la API solo valida el rol.
- **Sesiones:** cliente 5 min de inactividad y 30 min de vida máxima, como la banca en línea colombiana; operador 15 min y 9 h, una jornada laboral.
- **Ventana de revocación de hasta 5 min (RNF-01).** En sus palabras: *"existe un equilibrio entre seguridad y operatividad. Exigir revocación inmediata de todos los access tokens obligaría a que la API consultara constantemente al servidor de identidad o mantuviera mecanismos adicionales de invalidación. En cambio, usar access tokens de vida corta limita el riesgo a una ventana pequeña y predecible"*. Matices añadidos: validar sin consultar al emisor también permite que la API siga funcionando si el emisor se cae, y la ventana de 5 min debe incluir la tolerancia de reloj (con el `ClockSkew` por defecto serían 10).
- **Bloqueo temporal tras 5 intentos fallidos (15 min):** frena la fuerza bruta sin permitir que alguien que conoce un documento bloquee al cliente para siempre.
- **Amenaza interna aceptada como límite conocido, con auditoría:** el operador ve la clave temporal y podría entrar como el cliente; un banco real la entregaría por otro canal. Se registra en la base quién habilitó, restableció o deshabilitó cada acceso, para que sea detectable y defendible.
- **Auditoría de las operaciones de la 002 solo en logs:** no cambia el modelo de datos; el S8 completa la observabilidad.
- **El operador puede deshabilitar el acceso de un cliente** (clave robada).
- **Una credencial, un rol:** un empleado que es cliente tiene dos credenciales; cada acción tiene un papel claro.
- **El cliente ve también sus cuentas Cerradas**, con su estado.
- **Las tareas de la review 002 sobre el OpenAPI entran en la 003:** la 003 cambia el contrato y la 004 lo consume justo después.
- **Autoservicio:** cambiar la clave y cerrar sesión entran; recuperar la clave sin el operador queda fuera.
- **Segundo factor obligatorio para clientes y operadores, con app autenticadora (TOTP), dentro de la 003.** Lo eligió aunque la recomendación era dejarlo fuera de la 003. En sus palabras: *"una contraseña sola protege contra el robo de credenciales, pero no contra el uso de esas credenciales una vez robadas. Si un atacante obtiene la contraseña mediante phishing, reutilización de contraseñas o una filtración, todavía le faltaría el segundo factor"*. Matices añadidos: la clave es algo que se *sabe* y se copia sin que el dueño lo note, y el celular es algo que se *tiene*; TOTP no frena el phishing en tiempo real (la página falsa pide también el código y lo usa en el momento). Solo las passkeys resisten eso. Para estudiar: *adversary-in-the-middle phishing* y WebAuthn.
- **Inscripción del segundo factor en el primer ingreso; si pierde el celular, el operador restablece el acceso.** Sin códigos de respaldo.
