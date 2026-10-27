# ADR-0020: BFF propio en .NET para la sesión del navegador

- Estado: Aceptada
- Fecha: 2026-10-26
- Feature: 004-front-base (resuelve D-12 de la spec de producto)

## Contexto

La 004 crea la aplicación web (`core-bancario-web`, ADR-0005) con dos áreas, cliente y backoffice, que inician sesión con Keycloak (ADR-0014). La spec fijó la garantía antes de elegir el mecanismo:

- **RNF-01:** ningún token de acceso, de refresco ni de identidad es legible por el JavaScript de la app (ni en cookies accesibles por script, ni en `localStorage`/`sessionStorage`, ni en memoria, ni en las respuestas que recibe). Por eso la opción "tokens en memoria de la SPA" quedó **descartada por la spec**.
- **RNF-02:** una petición que cambia datos solo tiene efecto si la inició la propia app; se rechaza la de otro sitio y la de otro origen del mismo sitio (un subdominio hermano).
- **RNF-04/RNF-05:** ningún token en la URL ni en el historial; el regreso del login se acepta solo con su `state` y el `iss` del área, y el canje usa PKCE `S256`.
- **RN-26:** una sola sesión por navegador.
- **RN-22 y RN-25:** la sesión vence por inactividad *real*. Cómo se mide se decide en ADR-0021, que depende de esta.

Conceptos para leer esta ADR:

- **BFF (Backend For Frontend):** un servidor pequeño que vive junto a la SPA, en el **mismo origen**. Él hace el inicio de sesión con Keycloak, guarda los tokens y, cuando la SPA pide datos, reenvía la petición a la API añadiendo el token. El navegador solo tiene una cookie de sesión `HttpOnly`, que JavaScript no puede leer. Analogía: el guardarropa de un teatro. Dejas el abrigo (el token) y te dan una ficha (la cookie). La ficha no sirve fuera del teatro y no se puede convertir en el abrigo.
- **Cliente público frente a confidencial (OAuth):** un cliente público (una SPA) no puede guardar un secreto, así que cualquiera que copie su `client_id` puede pedir tokens con él. Uno confidencial (un servidor) se autentica ante Keycloak con un secreto que nunca llega al navegador.
- **CSRF:** otra página hace que el navegador de la víctima envíe una petición a nuestro sitio. El navegador **adjunta la cookie solo**, aunque la petición la haya iniciado otro sitio. Con un encabezado `Authorization` eso no pasaba, porque solo lo pone el código de la app. Al cambiar de token en el navegador a cookie, aparece este riesgo y hay que cerrarlo.
- **Sitio frente a origen:** el origen es esquema + host + puerto (`https://app.banco.com`); el sitio es el dominio registrable (`banco.com`). `evil.banco.com` es **otro origen del mismo sitio**: `SameSite` no lo frena.
- **YARP:** la librería de proxy inverso de Microsoft para ASP.NET Core. Reenvía peticiones HTTP a otro servidor y permite transformarlas (por ejemplo, poner el token).

## Opciones consideradas

**Dónde vive el BFF:**

1. **Dentro de la API** (`CoreBancario.Api` aceptaría cookie además de JWT). A favor: un proceso menos. En contra: la API dejaría de ser un servidor de recursos puro. Mezclaría dos modelos de autenticación (cookie con CSRF y JWT) en los mismos endpoints, las pruebas de ADR-0015 y ADR-0018 se complicarían, y cualquier error de configuración expondría las rutas de la API a CSRF.
2. **Proyecto nuevo en este repositorio, `src/CoreBancario.Bff`.** Un host ASP.NET Core aparte que no referencia Domain, Application ni Infrastructure. A favor: la API no cambia su forma de autenticar, y el BFF se prueba y se despliega por separado. Vive junto al contrato que consume y comparte el CI .NET. En contra: un proceso más que levantar.
3. **Servicio en un repositorio propio.** En contra: un tercer repositorio y un tercer CI para unas 600 líneas. Sin ganancia frente a la opción 2.

**Con qué piezas:**

1. **`AddOpenIdConnect` + `AddCookie` + YARP (`MapForwarder`)**, todo de Microsoft, con licencia MIT. Hay que escribir la renovación de tokens, el almacén de sesiones y la protección CSRF. Son unas pocas clases, y además son justo lo que el autor quiere aprender.
2. **Duende.BFF (v4).** Trae sesiones en el servidor, renovación de tokens, el encabezado anti-CSRF `X-CSRF: 1` y varios frontends. **Licencia (verificada el 2026-10-26):** la documentación de Duende dice que *"production use requires a license"*. Es gratis para desarrollo, pruebas y proyectos personales. Un revendedor lista la edición Starter en **3.920 USD al año** (hasta 3 frontends). Además, su modelo de sesión "deslizante" no distingue la actividad deliberada (RN-25): habría que desactivarlo y escribir el reloj propio igualmente.
3. **Librerías comunitarias pequeñas** (por ejemplo, `Bff.AspNetCore`). En contra: poco uso y mantenimiento incierto, en el componente más sensible del sistema.

**Dónde se guardan los tokens:**

1. **En la cookie cifrada** (Data Protection). Sin estado en el servidor. En contra: los tres tokens de Keycloak ocupan varios KB, así que la cookie se trocea en varias. No se puede revocar desde el servidor: una cookie robada sirve hasta que vence. Y con la rotación de refresh tokens (`maxReuse 0`), dos peticiones simultáneas llegan con el **mismo** refresh token en la cookie. Si las dos renuevan, Keycloak rechaza la segunda.
2. **En un almacén de sesiones del servidor** (`ITicketStore`), y la cookie solo lleva una clave opaca. A favor: cookie pequeña, cerrar sesión borra de verdad, y las renovaciones de una sesión se serializan con un candado. En contra: el servidor guarda estado. Si es en memoria, un reinicio cierra todas las sesiones y no escala a varias instancias.

**CSRF:**

1. **Solo `SameSite`.** No frena a un subdominio hermano (mismo sitio), y la spec lo exige (RNF-02).
2. **Token sincronizador** (un valor secreto por sesión que la SPA lee y reenvía). Funciona, pero obliga a exponer un valor a JavaScript y a gestionarlo.
3. **`SameSite=Strict` + encabezado propio obligatorio (`X-CSRF: 1`) + `Sec-Fetch-Site`.** Un formulario de otro sitio no puede poner encabezados. Un `fetch` de otro origen que lo intente dispara un *preflight* de CORS, y el BFF no responde CORS a nadie, así que el navegador bloquea la petición. `Sec-Fetch-Site`, que ponen los navegadores actuales, da una segunda comprobación barata.

## Decisión

- **Proyecto `src/CoreBancario.Bff`** (opción 2), construido con **`AddOpenIdConnect` + `AddCookie` + YARP 2.3.0** (piezas, opción 1). Duende.BFF se descarta por licencia y porque igualmente habría que escribir el reloj de RN-25. El precedente es ADR-0009, que tampoco tomó una dependencia con licencia comercial para algo que se resuelve con poco código propio.
- **Mismo origen.** El navegador solo habla con un origen: en local, `http://localhost:5173`, donde Vite (desarrollo) o nginx (compose y CI) reenvían `/bff/*` y `/api/*` al BFF. **La API deja de exponerse al navegador:** en compose no publica puerto y solo la alcanza el BFF. Como el navegador nunca llama ni a la API ni a Keycloak por `fetch`, **no hace falta CORS** en ninguna parte. Se cierra así el riesgo 12 de la 003.
- **Tokens en un almacén del servidor, en memoria** (`AlmacenDeSesiones : ITicketStore`). La cookie lleva solo la clave de la sesión, cifrada por el manejador de cookies. Las renovaciones de una misma sesión se serializan. Que la sesión no sobreviva a un reinicio se acepta en local y en la demo. D-11 lo revisa (pregunta abierta 1 del plan).
- **Cookie de sesión:** `__Host-cb-sesion` fuera de Development (`Secure`, `Path=/`, sin `Domain`: un subdominio hermano no puede ni leerla ni sobrescribirla), y `cb-sesion` en Development por HTTP. Siempre `HttpOnly` y `SameSite=Strict`. Es de sesión del navegador (sin `Expires`), sin deslizamiento: la vigencia la decide ADR-0021.
- **Dos realms, dos esquemas OIDC** (`OidcClientes`, `OidcEmpleados`) y **un solo esquema de cookie**. La sesión guarda su área. Cada esquema tiene su `CallbackPath` (`/bff/signin-oidc/clientes` y `/bff/signin-oidc/empleados`), su `Authority` y su `ValidIssuer`. Un regreso solo puede crear sesión del área de su ruta.
- **RN-26:** como hay una sola cookie, el navegador tiene como máximo una sesión. Si ya hay una de otra área, `GET /bff/iniciar-sesion/{area}` no inicia sesión: lleva a la pantalla "Tienes otra sesión abierta" (CL-10). Si, aun así, llega un regreso válido de otra sesión (dos pestañas a la vez), la anterior se cierra también en Keycloak antes de guardar la nueva.
- **El cliente `core-bancario-web` de Keycloak pasa a ser confidencial**, en los dos realms:
  - `publicClient: false`, `clientAuthenticatorType: client-secret`;
  - `secret: "${KC_WEB_CLIENTES_SECRET}"` / `"${KC_WEB_EMPLEADOS_SECRET}"`;
  - **se conserva PKCE `S256`** (defensa en profundidad, RNF-05);
  - `redirectUris` **exactas**: `["http://localhost:5173/bff/signin-oidc/clientes"]` y `[".../empleados"]`, sin comodín;
  - `webOrigins: []`;
  - `post.logout.redirect.uris` vacío.

  **Motivo de fondo:** si siguiera existiendo un cliente público con redirección a nuestro origen, un XSS podría hacer él mismo el flujo de código + PKCE y obtener tokens en JavaScript, y el BFF no serviría de nada. Cambiar el realm exige recrear el volumen de Keycloak (riesgo 10 de la 003).
- **Regreso del login (RNF-05, CL-01, CL-02):**
  - `ResponseType = code`, `ResponseMode = query`, `UsePkce = true`;
  - el `state` y la cookie de correlación los valida el manejador de ASP.NET Core;
  - el parámetro `iss` del regreso (RFC 9207) se compara con el emisor del área en `OnMessageReceived`;
  - cualquier fallo (`state`, `iss` o `error`) termina en `OnRemoteFailure`, que nunca muestra un error técnico. Si el navegador ya tiene sesión de esa área, va a su inicio (CL-02); si no, al inicio del área con el aviso `inicio-fallido`.
- **CSRF:** opción 3. Todo `/api/**` y todo `/bff/**`, salvo las tres navegaciones de nivel superior (iniciar sesión, cambiar clave y los regresos de Keycloak), exige `X-CSRF: 1`. Si llega `Sec-Fetch-Site` y no es `same-origin`, se rechaza. El rechazo es un 403 `peticion-no-confiable` **antes** de autenticar y de reenviar, así que la API nunca ve la petición.
- **Regla: ninguna operación que cambia datos responde a `GET`**, ni en el BFF ni en la API. Las tres navegaciones `GET` del BFF no cambian datos: iniciar sesión solo redirige a Keycloak, cambiar clave lleva a una pantalla de Keycloak que pide la clave actual, y el regreso solo crea sesión con un `state` y una cookie de correlación válidos. Cerrar sesión y registrar actividad son `POST`. Una prueba sobre el contrato de la API comprueba que sus `GET` son solo consultas.
- **El proxy** (`/api/{**resto}` → API):
  - quita `/api`, la cookie y `X-CSRF`;
  - pone `Authorization: Bearer <access token>`;
  - quita `Set-Cookie` de la respuesta;
  - añade `Cache-Control: no-store`.

  La API **no cambia** su autenticación (ADR-0015).
- **Cierre de sesión por el canal trasero.** El BFF llama al endpoint de *logout* de Keycloak con el refresh token y el secreto del cliente, servidor a servidor. No se usa el cierre por el navegador con `id_token_hint`, porque pondría el token de identidad en la URL y en el historial (RNF-04).
- **El contrato del BFF** (`/bff/*`) se genera al compilar y se versiona en `src/CoreBancario.Bff/openapi/core-bancario-bff.json`, con el mismo mecanismo de ADR-0019. La SPA genera sus tipos de los dos contratos.

El motivo principal es cumplir RNF-01 con piezas estándar y sin licencias comerciales, y dejar la API como un servidor de recursos que solo entiende JWT.

## Consecuencias

**Lo que se gana:**

- Un XSS puede actuar mientras la página está abierta, pero no puede llevarse una credencial reutilizable (la garantía que eligió el autor).
- Cerrar sesión borra la sesión de verdad, en el BFF y en Keycloak.
- La SPA no necesita ninguna librería OIDC.

**Lo que cuesta:**

- Un proceso más (`CoreBancario.Bff`) en local, en compose y en el CI.
- Sesiones en memoria: un reinicio del BFF cierra todas las sesiones, y no admite varias instancias.
- La protección CSRF es código propio: un middleware y su batería de pruebas.
- El secreto de `core-bancario-web` vive en `.env`/user-secrets del BFF y en el realm como marcador. Las pruebas de Keycloak.Tests que canjean el código directamente deben enviarlo.

**Hay que vigilar:**

- Que nadie añada una ruta `/bff` o `/api` exenta del encabezado anti-CSRF, ni un `GET` que cambie datos (las pruebas de la sección 7 del plan lo detectan).
- Que el proxy nunca reenvíe la cookie a la API ni copie un token a una respuesta (CA-05).
- Con dominios distintos en producción (D-11), revisar `SameSite`, el prefijo `__Host-` y las cookies de correlación de Keycloak. `localhost:5173` y `localhost:8080` son el mismo sitio, y eso puede ocultar problemas en local.

**Mejoras futuras:** almacén de sesiones persistente o compartido (PostgreSQL o Redis, con los tokens cifrados) si hay varias instancias; PAR (*Pushed Authorization Requests*); el *back-channel logout* de Keycloak hacia el BFF (revocación inmediata en vez de "en la siguiente renovación").

## Decisiones del autor

**Por qué el token no llega al navegador** (entrevista de la spec, 2026-10-26), en sus palabras: *"Lo que gana el banco es limitar el impacto del XSS. El atacante puede hacer daño mientras está dentro de la página, pero no puede convertir ese acceso temporal en una sesión reutilizable fuera del navegador de la víctima"*. Y: *"reduce el valor que un atacante obtiene de un XSS: puede actuar mientras la sesión está abierta, pero no puede extraer credenciales reutilizables. A cambio acepto el coste de introducir un BFF y protegerlo frente a CSRF mediante cookies seguras y mecanismos anti-CSRF"*.

**Comprobación del mecanismo de CSRF** (2026-10-26), en sus palabras:

1. Sobre un `GET` que cambia datos con `SameSite=Lax`: *"El atacante solo tendría que conseguir que la víctima visitara un enlace o cargara una página que apuntara a GET /cuentas/123/bloquear. Como SameSite=Lax sí permite enviar la cookie cuando el usuario sigue una navegación normal, el navegador adjuntaría la sesión y la acción podría ejecutarse. Por eso las operaciones que cambian datos nunca deben exponerse como GET."* Matiz que añadió Claude: con `Lax`, la cookie viaja solo en la navegación principal (un enlace, `window.location` o un formulario `GET`), no en un `<img>` incrustado.
2. Sobre el XSS frente al encabezado anti-CSRF: *"No, el encabezado X-CSRF no le estorba. Un XSS ejecuta JavaScript dentro de nuestra página, así que puede hacer exactamente las mismas peticiones que haría el código legítimo de la aplicación, incluyendo añadir el encabezado X-CSRF. El encabezado anti-CSRF protege contra páginas externas (CSRF), no contra código malicioso que ya está ejecutándose dentro de nuestro origen (XSS)."*

Consecuencia que el autor dejó fijada: **ninguna operación que cambie datos, en el BFF o en la API, responde a `GET`.**

**Almacén de sesiones en memoria** (pregunta abierta 1 del plan, 2026-10-26). Lo eligió entre memoria y PostgreSQL cifrado. Acepta como límite conocido que un reinicio del BFF cierre todas las sesiones y que solo haya una instancia; D-11 lo revisa al desplegar.

**Número de documento en la búsqueda** (pregunta abierta 2 del plan, 2026-10-26): se mantiene el `GET /clientes/por-documento` de la 002 y se controlan los logs (nginx sin la consulta; el BFF y la API no la registran, con una prueba que lo vigila). Se descartó un `POST /clientes/busqueda`. Si RNF-04 deja de ser propuesto, esa es la opción más robusta.
