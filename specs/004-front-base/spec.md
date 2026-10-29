# 004 — Front base: inicio de sesión, sesión segura y pantallas del operador

Estado: Aprobado (2026-10-26)
Fecha: 2026-10-26

## 1. Contexto y objetivo

La 003 protegió la API, pero no hay forma de usarla sin herramientas técnicas: un cliente no puede ver sus cuentas y un operador no puede atender a nadie. Esta feature crea la aplicación web (`core-bancario-web`, ADR-0005) con dos áreas, la banca del cliente y el backoffice. Las dos inician sesión con Keycloak (ADR-0014) y mantienen la sesión **sin que ningún código JavaScript pueda leer los tokens**. Además, la sesión vence de verdad cuando el usuario deja de usar la app (RN-22). Es la base sobre la que el S4, el S6 y el S7 añaden el extracto, las transferencias y las tasas.

La spec vive en este repositorio, con numeración global (decisión del autor); el repositorio web enlaza a ella. La 004 también toca la API: el endpoint de RF-16 y lo que pida la D-12.

## 2. Actores

- **Cliente:** entra por la raíz de la app. Inicia y cierra sesión, ve sus cuentas y el detalle de cada una (RF-05) y cambia su clave (RF-15).
- **Operador de backoffice:** entra por `/backoffice`. Busca y registra clientes, ve la ficha con sus cuentas y el estado del acceso digital, abre, bloquea, desbloquea y cierra cuentas, y habilita, restablece o deshabilita el acceso digital.
- **Keycloak (sistema):** muestra las pantallas de inicio de sesión, primer ingreso, segundo factor y cambio de clave, que ya existen desde la 003. La app no las reconstruye.

## 3. Requisitos funcionales

IDs globales de `docs/producto/spec-fase-1.md`. RF-16 se añadió allí a partir de esta entrevista. La 004 hace la parte de **la app** de cada RF; la regla y la API ya existen (002 y 003), salvo RF-16.

**Comunes (bloque 004a)**

- RF-14: Iniciar y cerrar sesión desde la app.
  - El cliente entra por la raíz (realm `clientes`) y el operador por `/backoffice` (realm `empleados`).
  - El primer ingreso con clave temporal (RN-21) y el segundo factor (RN-24) pasan por las pantallas de Keycloak.
  - Cerrar sesión termina también la sesión en Keycloak y la cierra en todas las pestañas.
- Rutas por rol: cada área solo se ve con una sesión de su rol (RN-19). Una ruta interna pedida sin sesión se recuerda y se abre después del inicio de sesión (CL-08).
- Sesión: inactividad y vida máxima según RN-22, medidas por la actividad real del usuario (RN-25), con aviso 60 s antes de cerrar, compartidas entre pestañas y con una sola sesión por navegador (RN-26).

**Cliente (bloque 004a)**

- RF-05 (cliente): "Mis cuentas" lista todas las cuentas del cliente, también las Cerradas, con número, moneda, estado y saldo. El detalle de una cuenta muestra además sus fechas. Una cuenta ajena o inexistente muestra la misma pantalla de "no encontrada".
- RF-15: botón "Cambiar clave", que lleva al flujo de Keycloak y regresa a la app con un mensaje de confirmación.

**Operador (bloque 004b)**

- RF-12: buscar un cliente por tipo y número de documento, escrito con o sin puntos, y ver su ficha con sus cuentas. Si no está registrado, se ofrece registrarlo.
- RF-01: registrar un cliente.
- RF-02: abrir una cuenta en COP o USD desde la ficha.
- RF-03: bloquear, desbloquear y cerrar una cuenta. Solo se ofrecen las transiciones que permite su estado (RN-14), y cerrar pide confirmación.
- RF-16 (nuevo, incluye cambio en la API): la ficha muestra el estado del acceso digital del cliente. Con ese estado, la app ofrece "Habilitar", "Restablecer" o "Deshabilitar" según corresponda.
- RF-13: habilitar, restablecer y deshabilitar el acceso digital, con confirmación. La clave temporal se muestra **una sola vez**, en un diálogo (CL-17).

## 4. Datos

La app no tiene datos propios. Muestra los de la API y guarda solo el estado mínimo de la sesión.

| Entidad | Campo | Tipo | Obligatorio | Reglas |
|---|---|---|---|---|
| Estado del acceso digital (RF-16) | Estado | Sin habilitar · Habilitado · Deshabilitado | Sí | "Sin habilitar": el cliente nunca tuvo acceso. Lo calcula la API; la app no lo deduce. |
| Estado del acceso digital (RF-16) | Primer ingreso pendiente | Sí · No | Solo si está Habilitado | Indica si la clave temporal aún no se usó: Keycloak conserva `UPDATE_PASSWORD` o `CONFIGURE_TOTP` (confirmado en Design y probado contra Keycloak real en el Build). |
| Dinero mostrado | Monto | Texto (`DineroDto.monto`) | Sí | Llega como texto y **nunca se convierte a número de coma flotante**. Se muestra exacto con el formato es-CO: COP sin decimales (`$ 1.234.567`) y USD con 2 (`US$ 1.234,56`). |
| Sesión en el navegador | Área | Cliente · Backoffice | Sí | Una sola a la vez por navegador (RN-26). |
| Sesión en el navegador | Última actividad real | Instante | Sí | Solo la actualiza una interacción deliberada (RN-25). La comparten todas las pestañas. |
| Sesión en el navegador | Inicio | Instante | Sí | Para la vida máxima (RN-22). |
| Ruta de regreso | Ruta | Texto | No | Solo una ruta interna de la misma área (CL-08). |
| Formulario de registro de cliente | Campos | — | — | Las mismas reglas de la spec 002, sección 4, para avisar en el navegador. La API sigue siendo la que decide. |

**Lo que nunca se guarda ni se muestra** en el navegador, ni en la URL ni en el historial: tokens de acceso, de refresco o de identidad (RNF-01) y la clave temporal, salvo en su diálogo (CL-17). Tampoco el número de documento, que no aparece en la URL (RNF-04, propuesto).

## 5. Reglas de negocio

IDs globales de `docs/producto/spec-fase-1.md`. RN-25 y RN-26 se añadieron allí a partir de esta entrevista.

- RN-14: Solo se ofrecen las transiciones de estado permitidas: Activa → Bloquear o Cerrar; Bloqueada → Desbloquear; Cerrada → ninguna.
- RN-19: Una credencial tiene un solo rol. Cada área de la app exige el rol que le corresponde, y la identidad sale siempre de la sesión.
- RN-20 y RF-13: Habilitar a quien ya tiene acceso es restablecer. La app lo muestra con su nombre ("Restablecer") gracias a RF-16, y la confirmación explica sus consecuencias: nueva clave temporal, segundo factor borrado y sesiones cerradas.
- RN-21 y RN-24: El primer ingreso y el segundo factor los gestiona Keycloak. La app no ofrece nada hasta que la sesión es completa.
- RN-22: Cliente, 5 min de inactividad y 30 min de vida máxima. Operador, 15 min de inactividad y 9 h de vida máxima.
- RN-25 (nueva): La inactividad de RN-22 se mide por la **interacción deliberada** del usuario con la app (clic, tecla o cambio de pantalla) en cualquiera de sus pestañas. Mover el ratón y hacer scroll no cuentan. Las peticiones que la app hace sola (recargas automáticas, renovación en segundo plano) **nunca** cuentan como actividad.
- RN-26 (nueva): Un navegador tiene como máximo una sesión abierta a la vez. Para entrar con otra credencial, por ejemplo en la otra área, primero se cierra la actual.

## 6. Requisitos no funcionales

**Seguridad**

- RNF-01: Ningún token de acceso, de refresco ni de identidad es legible por el código JavaScript que corre en la app. No aparece en cookies accesibles por script, en `localStorage`, en `sessionStorage`, en la memoria de la app ni en las respuestas que recibe. Es la garantía elegida frente a XSS: un script malicioso puede actuar mientras la página está abierta, pero no puede llevarse una credencial reutilizable (sección 11). Cómo se cumple lo decide D-12 en Design.
- RNF-02: Una petición que cambia datos solo tiene efecto si la inició la propia app. Una petición que llega de otro sitio o de otro origen se rechaza sin efecto (CSRF). Esto incluye un subdominio hermano, que es del "mismo sitio".
- RNF-03 (propuesto): La app se sirve con una política de seguridad de contenido (CSP) que no permite scripts en línea ni scripts de otros orígenes. Es la primera defensa contra el XSS.
- RNF-04: La clave temporal, los tokens y el número de documento no aparecen en la URL, en el historial ni en los logs de la app. El número de documento es propuesto: la búsqueda no lo pone en la URL.
- RNF-05: El regreso del inicio de sesión se acepta solo si su `state` coincide con el que inició ese flujo y su `iss` es el emisor del área. El canje del código usa PKCE con `S256`.

**Sesión**

- RNF-06: Una sesión inactiva se cierra como máximo **10 s** después del límite de RN-22 (propuesto), aunque la pestaña siga abierta y la app haga peticiones automáticas.
- RNF-07: Cerrar sesión, o que la sesión venza, se refleja en **todas** las pestañas abiertas en ≤ 5 s (propuesto), sin mostrar datos del usuario después.

**Calidad de la interfaz**

- RNF-08: Accesibilidad **WCAG 2.1 AA**: cero fallos de nivel AA en la revisión automática de cada pantalla. Además, los flujos de inicio de sesión, aviso de sesión y clave temporal se recorren solo con teclado y con el foco visible. El aviso de sesión cumple WCAG 2.2.1: avisa y da al menos 20 s para alargarla.
- RNF-09: El área del cliente funciona desde **360 px** de ancho sin scroll horizontal. El backoffice funciona desde **1280 px**.
- RNF-10 (propuesto): Soporta las dos últimas versiones de Chrome, Edge, Firefox y Safari. Los recorridos en navegador real del CI usan uno de ellos.
- RNF-11 (propuesto): Tras volver del inicio de sesión, la pantalla inicial del área se ve en ≤ 2 s en el entorno local.
- RNF-12: Los errores de la API se muestran como mensajes de negocio a partir de su `codigo` (ADR-0011), nunca del texto de `detail`. Cada código del catálogo vigente tiene un mensaje en español. Un código desconocido o un 500 muestra un mensaje genérico con el identificador de correlación (`traceId`).

**Contrato, pruebas y operación**

- RNF-13: Los tipos de la app se generan desde el contrato OpenAPI versionado de la API (ADR-0019), con TypeScript en modo estricto. Un cambio de contrato (RF-16 y lo que pida D-12) se hace primero en la API (ADR-0005).
- RNF-14: Cada criterio de aceptación tiene al menos una prueba automatizada que se vio en rojo antes de implementarse.
- RNF-15: En el CI corren dos recorridos en un **navegador real contra Keycloak real**: cliente (inicio de sesión con segundo factor → mis cuentas → cerrar sesión) y operador (inicio de sesión → buscar cliente → bloquear cuenta). Cierran el riesgo 2 de la 003.
- RNF-16 (propuesto): Un solo comando levanta en local la app, la API, Keycloak y PostgreSQL (ADR-0005, consecuencia pendiente del S3), y el README lo explica.

## 7. Errores y casos límite

**Inicio de sesión y regreso**

- CL-01: Si el regreso del inicio de sesión trae un `state` que no coincide, un `iss` de otro emisor o un `error` (por ejemplo, el de PKCE del estudio, P10), no se crea sesión. Se muestra un mensaje y se ofrece empezar de nuevo.
- CL-02: Si se reutiliza un regreso del inicio de sesión ya usado (atrás en el navegador, o recargar la página de regreso), no se crea una segunda sesión ni se muestra un error técnico.
- CL-03: Un cliente que entra por `/backoffice` llega al inicio de sesión de empleados, y su credencial no sirve ahí (mensaje genérico de Keycloak, CL-01 de la 003). Lo mismo al revés.
- CL-04: Una credencial con sesión válida pero sin el rol del área (la API responde 403) ve una pantalla "No tienes permiso para esta área", sin datos y con la opción de cerrar sesión.
- CL-05: Si Keycloak no responde al iniciar sesión, la app muestra "El inicio de sesión no está disponible; intenta más tarde", no una pantalla en blanco.

**Sesión**

- CL-06: 60 s antes del cierre por inactividad aparece un diálogo accesible con "Seguir conectado" y "Cerrar sesión". Elegir "Seguir conectado" cuenta como actividad. Si no hay respuesta, la sesión se cierra y la app lleva al inicio de sesión con el mensaje "Tu sesión se cerró por inactividad".
- CL-07: 60 s antes de la vida máxima aparece un aviso **sin opción de alargar**. Al vencer, lleva al inicio de sesión con el mensaje "Tu sesión terminó; vuelve a iniciar sesión".
- CL-08: Una ruta interna pedida sin sesión (por ejemplo `/mis-cuentas/{id}`) se abre después del inicio de sesión **solo si pertenece a la misma área**. Una dirección externa, una ruta de la otra área o una mal formada llevan al inicio del área (sin redirección abierta).
- CL-09: Pasada la inactividad, entrar de nuevo pide otra vez credenciales: la cookie de Keycloak no reabre la sesión. Si el BFF ya cerró la sesión de Keycloak (el caso normal, CA-10), pide usuario, clave y código. Si la sesión de Keycloak siguiera viva, `prompt=login` muestra la pantalla de reautenticación: Keycloak ya conoce al usuario y pide la clave y el código, pero nunca emite el código sin ellos (ADR-0021, segunda barrera; ajuste del Build, 2026-10-27). Se verificó a mano en el estudio de PKCE (P11, riesgo 4 de la 003) y aquí se automatiza.
- CL-10: Con una sesión abierta en un área, entrar a la otra ofrece cerrar primero la sesión actual (RN-26). No se abren dos sesiones.
- CL-11: Después de cerrar sesión, el botón atrás del navegador no muestra datos del usuario (saldos, fichas): lleva al inicio de sesión.
- CL-12: Si el operador deshabilita o restablece el acceso de un cliente con sesión abierta, el cliente pierde la sesión como máximo en 5 min (RNF-01 de la 003), y la app lo lleva al inicio de sesión con un mensaje genérico.
- CL-13: Si la sesión vence mientras se envía un formulario, lo escrito se pierde (no se guardan datos personales en el navegador). La app lleva al inicio de sesión con el mensaje de CL-06 o CL-07. El aviso de 60 s es la protección.

**Datos y acciones**

- CL-14: Un saldo con muchos dígitos o con decimales se muestra exacto. Por ejemplo, `12345678901234.56` USD se ve `US$ 12.345.678.901.234,56`, sin redondeos de coma flotante.
- CL-15: Mientras una acción que cambia datos está en curso, su botón queda desactivado, y la app **nunca** reintenta sola una acción que cambia datos. Un doble clic en "Abrir cuenta" envía una sola petición. Es un **límite conocido**: dos pestañas o un reintento de la red pueden abrir dos cuentas; la idempotencia de RN-11 llega en el S4 y el S5.
- CL-16: Un conflicto de concurrencia (409 `conflicto-de-concurrencia`, ADR-0012), como dos operadores que bloquean a la vez, muestra "La cuenta cambió mientras tanto" y recarga la ficha con el estado actual.
- CL-17: La clave temporal aparece en un diálogo con un botón de copiar y otro de "Ya la entregué". El diálogo no se cierra con Esc ni al hacer clic fuera. Cerrado, la clave ya no se puede ver: la única salida es restablecer otra vez.
- CL-18: Cerrar una cuenta con saldo distinto de cero muestra el mensaje de negocio de `saldo-distinto-de-cero`, y la cuenta sigue como estaba.
- CL-19: Si el sistema de identidad no responde al gestionar el acceso (502 `identidad-error` o 503 `identidad-sin-respuesta`), la app dice que se puede reintentar sin riesgo, porque la solicitud queda registrada (ADR-0017). Si responde al reintentar, entrega una clave temporal nueva.
- CL-20: Si no se puede obtener el estado del acceso (RF-16) porque el sistema de identidad no responde, la ficha se ve igual, con "Estado del acceso no disponible", y las acciones de acceso quedan desactivadas.
- CL-21: Si el estado del acceso que ve un operador está desactualizado (otro operador acaba de habilitarlo), el botón dice "Habilitar", pero la API restablece. La confirmación avisa de lo que pasará **si el cliente ya tiene acceso**, y el diálogo de la clave muestra la acción que de verdad hizo la API (`accion` en la respuesta).
- CL-22: Buscar un documento no registrado muestra "No hay un cliente con ese documento" y ofrece registrarlo con el tipo y el número ya escritos.
- CL-23: Al registrar un cliente con un documento ya registrado (409 `documento-duplicado`), la app ofrece ir a la ficha de ese documento.
- CL-24: Si la API no responde o falla de forma inesperada (500), las consultas muestran un mensaje con "Reintentar" y las acciones muestran el mensaje genérico con el `traceId` (RNF-12). La pantalla nunca queda en blanco.

## 8. Criterios de aceptación

**Bloque 004a: inicio de sesión y sesión (RF-14)**

- CA-01 (RF-14, RN-19, RNF-15): Dado un cliente con acceso habilitado y segundo factor inscrito, cuando abre la raíz de la app sin sesión, inicia sesión con documento, clave y código y luego cierra sesión, entonces ve "Mis cuentas" con sus cuentas. Después de cerrar, volver a entrar pide credenciales otra vez. Se prueba en navegador real contra Keycloak real.
- CA-02 (RF-14, RN-19): Dado un operador, cuando abre `/backoffice` sin sesión e inicia sesión, entonces llega al inicio del backoffice con rol Operador.
- CA-03 (RF-14, RN-21, RN-24): Dado un cliente con clave temporal, cuando entra por primera vez, entonces Keycloak le pide cambiar la clave e inscribir el segundo factor, y luego la app lo lleva a "Mis cuentas".
- CA-04 (RNF-05, CL-01): Dados un regreso con `state` distinto, uno con `iss` de otro emisor y uno con `error`, cuando la app procesa cada uno, entonces ninguno crea sesión y los tres muestran un mensaje con la opción de empezar de nuevo.
- CA-05 (RNF-01): Dado un usuario con sesión, cuando se revisa todo lo que el código de la página puede leer (cookies accesibles por script, `localStorage`, `sessionStorage` y el cuerpo de cada respuesta que recibe la app), entonces no aparece ningún token de acceso, de refresco ni de identidad.
- CA-06 (RNF-02): Dada una sesión abierta, cuando otro sitio, o otro origen del mismo sitio, envía una petición que cambia datos, entonces se rechaza y no tiene efecto.
- CA-07 (RN-25, RNF-06): Dado un cliente con sesión y la pestaña abierta, cuando pasan 5 min sin interacción deliberada mientras la app hace peticiones automáticas, entonces la sesión se cierra. Y dado un cliente que hace clic a los 4 min, entonces la sesión sigue abierta a los 6 min.
- CA-08 (CL-06, RNF-08): Dado un cliente inactivo, cuando faltan 60 s para el cierre, entonces aparece el aviso accesible y se puede manejar con el teclado. "Seguir conectado" mantiene la sesión. Sin respuesta, la sesión se cierra con el mensaje de inactividad.
- CA-09 (RN-22, CL-07): Dado un cliente activo sin parar, cuando llega a los 29 min, entonces ve el aviso de fin de sesión sin opción de alargar, y a los 30 min la sesión se cierra.
- CA-10 (CL-09): Dado un cliente cuya sesión se cerró por inactividad, cuando vuelve a entrar, entonces debe escribir otra vez usuario, clave y código.
- CA-11 (RN-25, RNF-07): Dadas dos pestañas del mismo cliente, cuando solo hay interacción en la primera, entonces la segunda tampoco vence. Y cuando cierra sesión en la primera, la segunda deja de mostrar datos y lleva al inicio de sesión en ≤ 5 s.
- CA-12 (CL-11): Dado un cliente que cerró sesión, cuando pulsa atrás en el navegador, entonces no ve saldos ni datos de sus cuentas.
- CA-13 (CL-08): Dado un enlace a `/mis-cuentas/{id}` abierto sin sesión, cuando el cliente inicia sesión, entonces llega a ese detalle. Y dada una ruta de regreso externa o del backoffice, después del inicio de sesión llega a "Mis cuentas".
- CA-14 (RN-26, CL-10): Dado un navegador con sesión de cliente, cuando abre `/backoffice`, entonces la app ofrece cerrar la sesión de cliente antes de iniciar la de operador, y nunca hay dos sesiones abiertas.
- CA-15 (CL-04): Dada una credencial con sesión pero sin el rol del área, cuando entra al área, entonces ve "No tienes permiso para esta área" y ningún dato.
- CA-16 (CL-12): Dado un cliente con sesión, cuando el operador deshabilita su acceso, entonces en ≤ 5 min la app lo lleva al inicio de sesión y ya no puede volver a entrar.
- CA-17 (RNF-15, CL-02 de la 003): Dado un cliente con documento CC 1234567, cuando inicia sesión en el navegador real escribiendo `1.234.567`, entonces entra como ese cliente.

**Bloque 004a: cliente (RF-05, RF-15)**

- CA-18 (RF-05): Dado un cliente con una cuenta Activa en COP y una Cerrada en USD, cuando abre "Mis cuentas", entonces ve las dos con número, moneda, estado y saldo en formato es-CO. Mientras carga ve un indicador, si no tiene cuentas ve un estado vacío y si la API falla ve un error con "Reintentar".
- CA-19 (RF-05, CL-14): Dada una cuenta con saldo `12345678901234.56` USD, cuando el cliente la ve, entonces el saldo aparece como `US$ 12.345.678.901.234,56`. Y un saldo de `1234567` COP aparece como `$ 1.234.567`.
- CA-20 (RF-05): Dadas una cuenta propia, una ajena y un id inexistente, cuando el cliente abre el detalle de cada una, entonces la propia muestra número, moneda, estado, saldo y fechas, y las otras dos muestran la misma pantalla de "Cuenta no encontrada".
- CA-21 (RF-15): Dado un cliente con sesión, cuando usa "Cambiar clave" y completa el flujo de Keycloak, entonces vuelve a la app con el mensaje "Clave actualizada", y su siguiente inicio de sesión funciona solo con la nueva.

**Bloque 004b: operador (RF-01, RF-02, RF-03, RF-12, RF-13, RF-16)**

- CA-22 (RF-12, CL-22): Dado un cliente con CC 1234567, cuando el operador busca `1.234.567`, entonces ve su ficha con sus cuentas, y el número de documento no aparece en la URL. Y dado un documento no registrado, ve "No hay un cliente con ese documento" con la opción de registrarlo.
- CA-23 (RF-01, CL-23): Dado el formulario de registro, cuando el operador envía un teléfono sin código de país, entonces ve el error junto al campo, y uno que solo detecta la API (400) también aparece junto a su campo. Con un documento ya registrado, se ofrece ir a su ficha. Con datos válidos, se abre la ficha del cliente nuevo.
- CA-24 (RF-02, CL-15): Dado un cliente, cuando el operador abre una cuenta en USD con un doble clic, entonces se envía una sola petición, y la ficha muestra una cuenta Activa nueva con saldo `US$ 0,00`.
- CA-25 (RF-03, RN-14): Dadas una cuenta Activa, una Bloqueada y una Cerrada, cuando el operador ve la ficha, entonces la Activa ofrece Bloquear y Cerrar, la Bloqueada solo Desbloquear y la Cerrada ninguna acción.
- CA-26 (RF-03, CL-18): Dada una cuenta Activa con saldo distinto de cero, cuando el operador confirma cerrarla, entonces ve el mensaje de negocio de `saldo-distinto-de-cero` y la cuenta sigue Activa.
- CA-27 (CL-16): Dada una cuenta que otro operador bloqueó mientras tanto, cuando el operador intenta una acción sobre ella y la API responde 409 `conflicto-de-concurrencia`, entonces ve "La cuenta cambió mientras tanto" y la ficha muestra el estado actual.
- CA-28 (RF-16): Dados clientes sin acceso, con acceso Habilitado y con acceso Deshabilitado, cuando el operador abre cada ficha, entonces ve el estado y las acciones "Habilitar" para el primero, "Restablecer" y "Deshabilitar" para el segundo y "Habilitar" para el tercero.
- CA-29 (RF-16, API): Dado el endpoint nuevo del estado del acceso, cuando lo llama un operador, entonces responde el estado del cliente. Con un cliente inexistente responde no encontrado, y para un cliente responde prohibido. Queda documentado en el contrato OpenAPI con 401, 403, 404 y 500.
- CA-30 (RF-13, CL-17, RNF-04): Dado un cliente sin acceso, cuando el operador lo habilita, entonces ve la clave temporal en un diálogo que no se cierra con Esc ni al hacer clic fuera. Al pulsar "Ya la entregué" el diálogo se cierra y la clave no queda en la URL, en el almacenamiento del navegador ni en ninguna pantalla.
- CA-31 (RF-13, RN-20, CL-21): Dado un cliente con acceso Habilitado, cuando el operador elige "Restablecer", entonces la confirmación explica que se genera una clave nueva, se borra el segundo factor y se cierran las sesiones. El diálogo de la clave dice "Restablecido".
- CA-32 (CL-19, CL-20): Dado que el sistema de identidad no responde, cuando el operador abre una ficha, entonces la ve con "Estado del acceso no disponible" y las acciones de acceso desactivadas. Y si falla al habilitar, ve que puede reintentar sin riesgo.
- CA-33 (RNF-15): Dado un operador y un cliente con una cuenta Activa, cuando en el navegador real el operador inicia sesión, busca al cliente y bloquea la cuenta, entonces la ficha muestra la cuenta Bloqueada.

**Transversales**

- CA-34 (RNF-12, CL-24): Dado cada `codigo` del catálogo de errores vigente, cuando la API lo devuelve, entonces la app muestra su mensaje de negocio. Y dado un código desconocido o un 500, muestra el mensaje genérico con el `traceId`.
- CA-35 (RNF-08): Dada cada pantalla de la 004, cuando se pasa la revisión automática de accesibilidad, entonces no hay fallos de nivel AA. Y los flujos de inicio de sesión, aviso de sesión y clave temporal se completan solo con teclado.
- CA-36 (RNF-09): Dada el área del cliente a 360 px de ancho y el backoffice a 1280 px, cuando se recorre cada pantalla, entonces no hay scroll horizontal y todo el contenido se puede usar.

## 9. Fuera de alcance

- **Extracto** (RF-06), **depósitos y retiros** (RF-04): S4.
- **Transferencias** y su clave de idempotencia de punta a punta: S5 y S6.
- **Tasas de cambio** y conversión: S7.
- **Idempotencia en el servidor de "abrir cuenta"** y del resto de acciones del operador. La SPA solo evita el doble clic (CL-15, límite conocido).
- **Conservar borradores** de formularios tras el vencimiento de la sesión (CL-13).
- **Backoffice en el celular** (RNF-09).
- **Dos sesiones a la vez** en el mismo navegador (RN-26). Para la demo se usan dos navegadores o perfiles.
- **Consultar el registro de accesos** desde la app (sigue fuera, como en la 003), gestionar operadores y el autorregistro del cliente.
- **Recuperar la clave sin el operador**, códigos de respaldo y passkeys (como en la 003).
- **Otros idiomas:** solo español, con formato es-CO.
- **Observabilidad completa** (trazas del navegador a la base, métricas): S8. La 004 solo muestra el `traceId` en los errores.
- **Despliegue y HTTPS de punta a punta** fuera de local: D-11 (fase 4).
- **Crear `core-bancario-web` con su kit SDD** para TypeScript (agentes, skills, `ts-clean-code`) y su CI. Es la tarea `chore/kit-web` del repo nuevo, antes de `/sdd-build` (decisión del autor).

## 10. Preguntas para Design

- **D-12 (ADR): cómo mantiene el navegador la sesión sin que JavaScript lea los tokens (RNF-01, RNF-02).** El autor fijó la garantía, así que la opción de tokens en memoria de la SPA queda descartada por la spec. Design decide:
  - **dónde vive el componente intermedio** (BFF): un proyecto nuevo en este repo, dentro de la API o un servicio aparte;
  - **si la API deja de exponerse** al navegador y solo la ve el BFF (P14 del estudio);
  - **con qué piezas**: `AddOpenIdConnect` + `AddCookie`, proxy con YARP o una librería BFF de terceros (verificar su licencia);
  - **dónde se guardan los tokens** en el servidor (en la cookie cifrada o en un almacén de sesiones) y el tamaño de la cookie;
  - **cómo atiende dos realms** (dos esquemas OIDC, uno por área) y cómo se cumple RN-26;
  - si el cliente `core-bancario-web` de Keycloak pasa a ser **confidencial**, con su `redirectUris` y `webOrigins`. Cambiar los realm JSON exige recrear el volumen (riesgo 10 de la 003).
- **CSRF (RNF-02):** `SameSite` de la cookie más un encabezado anti-CSRF propio (P13). Cómo se comprueba en CA-06, incluido el subdominio hermano.
- **CORS:** con el BFF en el mismo origen de la SPA puede que la API no lo necesite (riesgo 12 de la 003). Confirmarlo.
- **Actividad real (RN-25) frente a la renovación:** cómo distingue el BFF una petición del usuario de una automática (por ejemplo, un encabezado que la SPA solo pone en las acciones deliberadas, o que la SPA informe de la actividad). Cómo se alinea el reloj de la SPA, que avisa a los 60 s, con el de Keycloak, que cierra la sesión.
- **Pestañas (RNF-07):** cómo se comparten la actividad y el cierre entre pestañas.
- **D-09 (ADR): cómo se construye el frontend:** herramienta de build, estado del servidor (TanStack Query, según el roadmap), formularios, generación del cliente desde OpenAPI y librería de componentes accesibles.
- **RF-16 en la API:**
  - ruta y forma del endpoint;
  - de dónde sale el estado (la tabla del vínculo de ADR-0016, Keycloak o ambos);
  - si "primer ingreso pendiente" se puede obtener;
  - qué hacer si Keycloak no responde (CL-20).
  Se hace antes que la app, porque es un cambio de contrato (ADR-0005).
- **Probar la sesión sin esperar 5 min ni 30 min** (CA-07, CA-09, CA-10): un realm o una configuración de pruebas con tiempos cortos, o un reloj controlable en la SPA.
- **Segundo factor en los recorridos de navegador real** (RNF-15): generar el código TOTP desde un secreto conocido, como en Keycloak.Tests (ADR-0018).
- **Montos exactos** (CL-14): cómo se formatea un texto decimal con `Intl.NumberFormat` sin pasar por `number`.
- **Cerrar sesión:** cierre iniciado por la app en Keycloak (`id_token_hint`, `post_logout_redirect_uri`) y limpieza de la caché del estado del servidor (CL-11).
- **CSP (RNF-03):** cabeceras y cómo se prueban.
- **Un solo comando para todo** (RNF-16): un compose que combine los dos repos, o uno en cada repo.
- **Bloques:** 004a (inicio de sesión de las dos áreas, sesión, D-12 y área del cliente) y 004b (backoffice y RF-16), cada uno con su PR (decisión del autor). Confirmar que 004a se puede mergear solo.

## 11. Decisiones del autor

Entrevista del 2026-10-26.

- **Alcance:** entran todas las pantallas del operador (clientes, cuentas y acceso digital) y "Mis cuentas" del cliente, además de iniciar y cerrar sesión y "Cambiar clave".
- **Dos entradas:** la raíz para el cliente y `/backoffice` para los empleados. El cliente nunca ve el backoffice, como en los bancos reales.
- **El operador ve el estado del acceso digital** (RF-16, endpoint nuevo). Así se evita restablecer sin querer el acceso de un cliente (CL-13 de la 003), y el botón dice "Habilitar" o "Restablecer" según el caso.
- **Spec aquí, con numeración global.** Las features del front siguientes harán lo mismo.
- **Actividad real = interacción deliberada** (clic, tecla o cambio de pantalla). Mover el ratón y hacer scroll no cuentan, y las peticiones automáticas nunca cuentan (RN-25).
- **Aviso 60 s antes** del cierre por inactividad, con "Seguir conectado", y aviso sin alargar antes de la vida máxima (WCAG 2.2.1).
- **Una sesión para todas las pestañas:** la actividad y el cierre valen en todas.
- **El token no llega al navegador (RNF-01, decide la dirección de D-12).** En sus palabras: *"Lo que gana el banco es limitar el impacto del XSS. El atacante puede hacer daño mientras está dentro de la página, pero no puede convertir ese acceso temporal en una sesión reutilizable fuera del navegador de la víctima"*. Y: *"reduce el valor que un atacante obtiene de un XSS: puede actuar mientras la sesión está abierta, pero no puede extraer credenciales reutilizables. A cambio acepto el coste de introducir un BFF y protegerlo frente a CSRF mediante cookies seguras y mecanismos anti-CSRF"*.
  - **Pendiente de comprobar en el ADR de D-12:** el mecanismo de CSRF. El navegador adjunta la cookie sola, aunque la petición la inicie otro sitio, mientras que el encabezado `Authorization` solo lo pone el código de la app. En la entrevista quedó explicado por Claude, no por el autor.
- **Doble envío: protección solo en la SPA** (botón desactivado, sin reintentos automáticos de acciones que cambian datos). La idempotencia en el servidor llega con RN-11 en el S4 y el S5. Límite conocido: dos pestañas o un reintento de red (CL-15).
- **Regreso tras el inicio de sesión a la ruta pedida, solo si es interna y de la misma área** (sin redirección abierta).
- **Clave temporal en un diálogo con confirmación** ("Ya la entregué"), que no se cierra por accidente y no se guarda en el navegador.
- **Si la sesión vence con un formulario a medio llenar, se pierde lo escrito.** El aviso de 60 s lo previene, y no se guardan datos personales en el navegador.
- **Accesibilidad WCAG 2.1 AA revisada**, con revisión automática y recorridos con teclado.
- **Recorridos en navegador real contra Keycloak real**, uno por área, en el CI. Cierran el riesgo 2 de la 003.
- **Dos bloques, 004a y 004b**, cada uno con su PR.
- **El kit SDD del repo web es un `chore/kit-web` antes del Build**, no parte de la 004.
- **"Cambiar clave" entra** en el área del cliente, con el flujo de Keycloak.
- **Cliente en el celular (desde 360 px), backoffice en escritorio (desde 1280 px).**
- **Una sola sesión por navegador** (RN-26). Para la demo, dos navegadores o perfiles.
