# ADR-0018: Pruebas de autenticación en cuatro niveles

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api (RNF-07, RNF-08; complementa a ADR-0006)

## Contexto

La 003 introduce un sistema externo (Keycloak, ADR-0014) que interviene en casi todo: valida identidades, aplica sesiones y bloqueos y recibe las operaciones de gestión de accesos. Hay que decidir cómo se prueba:

- RNF-07 exige al menos una prueba automatizada por criterio de aceptación (22 CA).
- RNF-08 exige que al menos una prueba recorra el flujo real (iniciar sesión → llamar a la API) y que **las demás pruebas de la API no necesiten arrancar el sistema de identidad real**.
- El primer arranque de Keycloak tardó unos **188 s** en el experimento.
- Los caminos de error de "intención primero" (ADR-0017) son casi imposibles de provocar contra un Keycloak real de forma repetible. Pasa con "aplicó el cambio y no respondió" o "la base cayó después de Keycloak".
- Algunas reglas solo existen en Keycloak (bloqueo, OTP, mensajes del login) y solo se pueden comprobar contra Keycloak real.

Un concepto para leer esta ADR: **un *fake* (doble de prueba)** es una implementación sencilla de un puerto que se comporta como el real, pero en memoria y bajo control de la prueba. Puede, por ejemplo, "aplicar el cambio y no responder" a propósito. Analogía: un simulador de vuelo, donde se puede provocar una falla de motor sin estrellar un avión.

## Opciones consideradas

1. **Todo contra Keycloak real.** Máxima fidelidad. En contra: cada ejecución de pruebas tarda minutos; los caminos de error de ADR-0017 no son deterministas; contradice RNF-08.
2. **Nada contra Keycloak real (todo con dobles).** Rápido. En contra: nadie comprobaría que la configuración del realm hace lo que la spec pide (bloqueo, OTP, mensajes), ni que el adaptador llama bien a la Admin REST API. Incumple RNF-08.
3. **Cuatro niveles, cada uno para una pregunta distinta**, y solo el último arranca Keycloak.

## Decisión

**Opción 3:**

| Nivel | Pregunta | Cómo | Proyecto |
|---|---|---|---|
| **Application** | ¿Intención primero deja siempre rastro? ¿La reconciliación cierra bien? | El puerto `IProveedorDeIdentidad` con un fake en memoria que puede responder con error, no responder o **aplicar y no responder**. Es determinista y tarda milisegundos | `CoreBancario.Application.Tests` |
| **API** | ¿La API autentica, autoriza y cumple el contrato (401/403/404, roles, ventana de 5 min)? | PostgreSQL real (Testcontainers, como en la 002) + **tokens firmados con claves RSA propias** para los dos emisores, inyectadas como configuración OIDC fija. Sin Keycloak | `CoreBancario.Api.Tests` |
| **Adaptador** | ¿Las llamadas de administración hacen en Keycloak lo que el adaptador cree? | Contra Keycloak real (Testcontainers) | `CoreBancario.Keycloak.Tests` |
| **Punta a punta** | ¿El flujo real funciona: login con OTP, bloqueo, sesiones, tema, y luego la API? | Keycloak real + API, recorriendo la página de login con `HttpClient` | `CoreBancario.Keycloak.Tests` |

Detalles:

- **Un solo contenedor de Keycloak** para el adaptador y el punta a punta, en un proyecto propio. Así las otras baterías siguen rápidas y `dotnet test` local no espera a Keycloak en cada cambio pequeño. Es una excepción consciente a "un proyecto de tests por capa", y CLAUDE.md la anota.
- **Los realms de las pruebas son los de producción.** El fixture importa los mismos JSON y, tras arrancar, **solo baja los tiempos** por la Admin API (inactividad, espera del bloqueo) para que CA-09 y CA-10 no tarden 15 min. No hay una segunda copia del realm que pueda desincronizarse. Una prueba de configuración, que no arranca nada, lee los JSON de producción y verifica los valores reales: 270 s, 5/30 min, 15 min/9 h y 5 fallos → 15 min.
- **Usuarios de prueba en el bloque 003a**, que se construye antes que la gestión de accesos (RF-13). El fixture usa la Admin REST API con el administrador de `master` del contenedor para crear al cliente en `clientes`, con el mismo nombre de usuario, clave temporal y acciones obligatorias que usará la API, y al operador en `empleados`. El vínculo `sub` ↔ `ClienteId` lo inserta directamente en la base. En el bloque 003b, la prueba del flujo completo (RNF-08) crea al cliente con el endpoint de habilitar.
- **TOTP:** la prueba lee el secreto del formulario de inscripción y calcula el código (RFC 6238). Antes de un segundo ingreso espera al siguiente periodo de 30 s, porque Keycloak no acepta reutilizar un código.
- **Cookies:** las del login llevan `Secure` y el `CookieContainer` no las reenvía por HTTP (hallazgo 7). Un `DelegatingHandler` de las pruebas las maneja a mano.
- **Lógica del navegador (CA-12 y el aviso del número de documento):** las funciones puras de `documento.js` (`normalizarUsuario`, `contieneNumeroDeDocumento`) se ejecutan con un intérprete de JavaScript (Jint) y se comparan con el dominio. El punta a punta comprueba que las páginas cargan el script y tienen los campos que necesita, y lo que hace el servidor: acepta el usuario normalizado, rechaza la clave que contiene el usuario completo y acepta la que contiene solo el número. Esto último documenta el hueco aceptado.
- **Nombres:** `FNNN_ID_Escenario_Resultado`. Las pruebas de la 002 que ahora llevan un token de operador siguen siendo `F002_…`; el comportamiento nuevo va en `F003_…`.

El motivo principal es que cada nivel responde a una pregunta que los otros no pueden responder bien, y solo el último paga el costo de arrancar Keycloak.

## Consecuencias

- La batería habitual (Domain, Application, Infrastructure y Api) sigue tardando lo mismo que en la 002, más el cifrado RSA de los tokens.
- `CoreBancario.Keycloak.Tests` tarda varios minutos (arranque, esperas de OTP y tiempos cortos). En el CI corre en el mismo job al principio. Si molesta, se mueve a un job paralelo.
- Un cambio en el realm JSON se prueba en dos sitios: la prueba de configuración (valores) y el punta a punta (comportamiento).
- El fake de Application puede divergir del Keycloak real. Las pruebas del adaptador son las que lo detectan.
- Hueco aceptado: ninguna prueba ejecuta `documento.js` **dentro** de un navegador real, ni la normalización ni el aviso del número de documento. Playwright lo cerraría a costa de descargar navegadores en el CI. Queda como mejora.
  - *Actualización (2026-10-31, `fix/normalizacion-documento-login`):* el enganche de eventos ya se prueba con Jint sobre un DOM de mentira (`PaginaDelTema`), que simula la captura en `document` y el envío antes y después de `DOMContentLoaded`. Sigue sin probarse en un navegador real; el recorrido con red y navegador reales lo cubre el `e2e:real` de `core-bancario-web`, que fue el que encontró la carrera.

## Decisiones del autor

En sus palabras: *"cada nivel puede responder a una pregunta distinta… evita depender de Keycloak real para toda la batería de pruebas y mantiene razonables los tiempos de ejecución."*

El autor eligió los cuatro niveles, un solo contenedor compartido, el realm de pruebas con tiempos cortos más la prueba de configuración del realm de producción, y el cálculo de TOTP con RFC 6238.

La forma concreta de los "tiempos cortos" (los mismos JSON de producción, ajustados por la Admin API tras arrancar, en vez de un segundo JSON) es una propuesta del planner. Su fin es que no haya dos copias del realm.
