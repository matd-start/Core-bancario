# Aprendizaje — 004 Front base, bloque 004a (PR 1: repo `core-bancario`, tareas T01 a T11)

Este documento explica lo que se construyó en el repositorio `core-bancario` para el bloque 004a: el **BFF** (*backend for frontend*) que da sesión segura al navegador. Está pensado para alguien que ve estos patrones por primera vez. Lo que se construye en `core-bancario-web` (PR 2) no está aquí.

## 1. Mapa del código

### Configuración y operación

| Archivo | Qué hace |
|---|---|
| `infra/keycloak/realms/{clientes,empleados}-realm.json` | El cliente `core-bancario-web` deja de ser **público** y pasa a **confidencial**: tiene un secreto (que Keycloak resuelve desde una variable de entorno) y solo acepta volver a la URL exacta del BFF de su área. |
| `docker-compose.yml`, `.env.example` | Los dos secretos nuevos (`KC_WEB_*`), `KC_HOSTNAME` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`, y los servicios `api`, `bff` (perfil `app`) y `web` (perfil `web`). |
| `src/CoreBancario.{Api,Bff}/Dockerfile`, `.dockerignore` | Las imágenes de la API y el BFF (multietapa: el SDK compila, el runtime ejecuta). |
| `.github/workflows/ci.yml` | Genera los secretos nuevos, levanta la API y el BFF y comprueba que la API responde `200` en `/health` y el BFF `401` sin sesión, desde dentro de la red de compose. Compara también el contrato del BFF. |

### Api (`src/CoreBancario.Api`) — cambios pequeños

| Archivo | Qué hace |
|---|---|
| `Autenticacion/OpcionesDeAutenticacion.cs`, `ConfiguracionDeAutenticacion.cs` | `Metadatos` opcional por emisor: de dónde se baja el documento de descubrimiento (dentro de Docker es `http://keycloak:8080/...`), sin cambiar el `iss` que se valida (`http://localhost:8080/...`). |
| `OpenApi/CodigosDeErrorEnOpenApi.cs` | Añade al contrato OpenAPI un *enum* `CodigoDeError` con todas las constantes de `CodigosDeError`, leídas por reflexión. |

### Bff (`src/CoreBancario.Bff`, proyecto nuevo)

| Archivo | Capa conceptual | Qué hace |
|---|---|---|
| `Program.cs` | Arranque | Registra todo y ordena el *pipeline*: cabeceras reenviadas → cabeceras de seguridad → anti-CSRF → autenticación → endpoints y proxy. |
| `Configuracion/AreaDeSesion.cs`, `AreasDeSesion.cs` | Reglas puras | Las dos áreas (`Clientes`, `Empleados`) y sus nombres, rutas y esquema OIDC. |
| `Configuracion/OpcionesDelBff.cs` | Configuración | Sección `Bff`: emisor, secreto, inactividad y vida máxima de cada área. |
| `Sesiones/SesionDelNavegador.cs`, `TokensDeLaSesion.cs`, `PlazosDeSesion.cs`, `EstadoDeSesion.cs`, `RolDeSesion.cs`, `MotivoDeCierre.cs` | Dominio de la sesión | Lo que el servidor sabe de un navegador: quién es, desde cuándo, cuándo hizo algo por última vez y sus tokens (que no se imprimen). |
| `Sesiones/PoliticaDeSesion.cs` | Reglas puras | "¿Venció esta sesión?" y "¿cuánto le queda?". Una función pura: recibe la sesión, las opciones y la hora. |
| `Sesiones/LectorDeRol.cs` | Reglas puras | Lee los roles del access token (sin validarlo) para saber qué pantalla mostrar. |
| `Sesiones/AlmacenDeSesiones.cs` | Almacenamiento | Guarda las sesiones **en memoria** y le da a la cookie solo una clave opaca. |
| `Sesiones/ServicioDeSesion.cs` | Caso de uso | Consultar, registrar actividad, entregar el access token al proxy y cerrar. Aplica la política en cada operación. |
| `Identidad/IClienteDeIdentidad.cs`, `ClienteDeIdentidadKeycloak.cs` | Frontera | Renovar tokens, cerrar sesión y preguntar si Keycloak responde. Lo único que habla HTTP con Keycloak fuera del navegador. |
| `InicioDeSesion/ConfiguracionDeSesion.cs`, `EventosDeOidc.cs` | Autenticación | Configura la cookie y los dos esquemas OIDC (uno por realm) y reacciona a sus eventos. |
| `InicioDeSesion/RutaDeRegreso.cs`, `Avisos.cs`, `PropiedadesDeSesion.cs` | Reglas puras | A qué ruta volver tras iniciar sesión (sin permitir saltos a otros sitios), los avisos de la URL y las claves internas del ticket. |
| `Seguridad/ProteccionCsrf.cs`, `EncabezadosDeSeguridad.cs` | Seguridad | Rechaza peticiones no confiables y fuerza `no-store` y `nosniff`. |
| `Errores/CodigosDelBff.cs`, `RespuestasDelBff.cs` | Errores | Los códigos estables (`sin-sesion`, `sesion-revocada`…) y las respuestas `ProblemDetails`. |
| `Endpoints/EndpointsDelBff.cs` | Entrada | `/bff/iniciar-sesion/{area}`, `/bff/cambiar-clave`, `/bff/sesion`, `/bff/actividad`, `/bff/salir`. |
| `Proxy/ProxyDeLaApi.cs` | Entrada | `/api/{**resto}`: obtiene el token y reenvía a la API con YARP. |
| `OpenApi/CodigosDelBffEnOpenApi.cs`, `openapi/core-bancario-bff.json` | Contrato | El *enum* de códigos del BFF y el contrato versionado. |

## 2. Recorrido de una petición

### A. Iniciar sesión (de punta a punta)

1. La SPA navega a `GET /bff/iniciar-sesion/clientes?regreso=/mis-cuentas/abc`. Pasa por `EncabezadosDeSeguridad` y `ProteccionCsrf` (esta ruta está exenta: es una navegación).
2. `EndpointsDelBff.IniciarSesionAsync` hace las comprobaciones **en este orden**: ¿el área existe? → ¿hay sesión de la *otra* área? → ¿ya hay sesión de esta área (entonces solo redirige)? → ¿Keycloak responde (`ClienteDeIdentidadKeycloak.EstaDisponibleAsync`, 3 s)? Si todo va bien, `Challenge` al esquema `OidcClientes`.
3. El manejador OIDC de ASP.NET Core arma la petición de autorización (código + PKCE `S256` + `state`) y `EventosDeOidc.RedirectToIdentityProvider` le añade `prompt=login`. El navegador va a Keycloak y la persona escribe usuario, clave y código OTP.
4. Keycloak redirige a `GET /bff/signin-oidc/clientes?code=…&state=…&iss=…`. `ProteccionCsrf` también lo exime. `EventosDeOidc.MessageReceived` comprueba que `iss` sea el del realm; el manejador comprueba `state` y correlación y **canjea el código por tokens** por el canal trasero, usando el secreto del cliente.
5. `EventosDeOidc.TokenValidated` anota el área. `TicketReceived` aplica RN-26 (una sola sesión por navegador). El manejador de cookies llama a `AlmacenDeSesiones.StoreAsync`: construye la `SesionDelNavegador`, la guarda **con los tokens**, y le devuelve a la cookie solo la clave.
6. El navegador recibe una redirección a la ruta de regreso (`RutaDeRegreso.Normalizar` la validó) y la cookie `HttpOnly` + `SameSite=Strict`.

### B. Pedir datos (`GET /api/mis-cuentas`)

1. `ProteccionCsrf` exige `X-CSRF: 1` y `Sec-Fetch-Site: same-origin`; si falta, `403 peticion-no-confiable` y la API ni se entera.
2. `ProxyDeLaApi.PasoPrevioAsync` lee la clave de la cookie y llama a `ServicioDeSesion.ObtenerAccessTokenAsync`: evalúa `PoliticaDeSesion`; si el token vence en menos de 30 s lo renueva (con candado por sesión); **nunca** registra actividad.
3. Si la sesión terminó, responde `401` con su código; si hacía falta renovar y Keycloak no responde, `503`. En los dos casos **no** llama a la API.
4. Si todo va bien, YARP reenvía sin el prefijo `/api`, sin `Cookie` ni `X-CSRF`, con `Authorization: Bearer <access token>`, y quita cualquier `Set-Cookie` de la respuesta. `EncabezadosDeSeguridad` añade `no-store` al final.

### C. El latido (`POST /bff/actividad`)

La SPA lo envía cuando la persona hace clic, pulsa una tecla o cambia de pantalla (como mucho uno cada 15 s). `ServicioDeSesion.RegistrarActividadAsync` renueva los tokens (para que el reloj de Keycloak se alinee) y **después** mueve `UltimaActividad`. Es lo único que mantiene viva la sesión.

## 3. Conceptos clave

### BFF con cookie opaca: el navegador nunca ve un token

- **Qué problema resuelve:** si un token vive en JavaScript, cualquier script inyectado (XSS) lo puede robar. Aquí los tokens viven en el servidor y el navegador solo tiene una clave que no puede leer.
- **Dónde está:** `AlmacenDeSesiones` (guarda), `ConfiguracionDeSesion` (cookie `HttpOnly`, `SameSite=Strict`, `SessionStore`), `ProxyDeLaApi` (pone el `Bearer`), `TokensDeLaSesion.ToString` (ni en un log por descuido).
- **Analogía:** el guardarropa de un teatro. Entregas tu abrigo (el token) y recibes una ficha numerada (la cookie). La ficha no sirve para nada fuera del guardarropa, y quien la pierde no pierde el abrigo.

### CSRF: tres defensas juntas

- **Qué problema resuelve:** una página maliciosa podría hacer que tu navegador envíe una petición al BFF *con tu cookie*. Si la petición cambia datos, sería un ataque.
- **Dónde está:** `ProteccionCsrf` (encabezado `X-CSRF: 1` y `Sec-Fetch-Site`), la cookie `SameSite=Strict` y la regla de que ningún `GET` cambia datos (`/bff/actividad` y `/bff/salir` solo aceptan `POST`).
- **Analogía:** para retirar dinero no basta con tener la tarjeta (la cookie): hay que decir también una palabra secreta que solo la propia app sabe poner (el encabezado), y el cajero comprueba de dónde viene la persona (`Sec-Fetch-Site`).

### Reglas puras con reloj inyectado

- **Qué problema resuelve:** las reglas de tiempo (5 min de inactividad, 30 min de vida máxima, "≥" en el límite exacto) son fáciles de equivocar y lentas de probar con tiempo real. Una función pura que recibe la hora se prueba en milisegundos con un reloj falso.
- **Dónde está:** `PoliticaDeSesion`, `SesionDelNavegador.RegistrarActividad` (nunca retrocede), `RutaDeRegreso`, `LectorDeRol`; `TimeProvider` en `AlmacenDeSesiones`, `ServicioDeSesion` y `ClienteDeIdentidadKeycloak`.
- **Analogía:** un árbitro que mira el cronómetro que le dan, en vez de llevar uno propio: en el entrenamiento le pasas un cronómetro que corre a la velocidad que quieras.

### Candado por sesión (renovación con rotación)

- **Qué problema resuelve:** Keycloak rota el *refresh token*: cada renovación invalida el anterior. Si dos peticiones renuevan a la vez, una usa un token ya gastado y la sesión se cierra por error.
- **Dónde está:** `AlmacenDeSesiones.ConCandadoAsync` (un `SemaphoreSlim` por clave) y `ServicioDeSesion`, que **vuelve a mirar dentro del candado** si otra petición ya renovó.
- **Analogía:** una sola ventanilla por cliente: quien llega segundo espera, y cuando le toca comprueba si el primero ya hizo el trámite.

### Frontera con un sistema externo (puerto y adaptador)

- **Qué problema resuelve:** el BFF depende de Keycloak, que puede rechazar, no responder o estar caído. Una interfaz permite provocar esos casos en las pruebas sin Keycloak real.
- **Dónde está:** `IClienteDeIdentidad` (el puerto) y `ClienteDeIdentidadKeycloak` (el adaptador); en las pruebas, `ClienteDeIdentidadFalso`.
- **Analogía:** el enchufe de la pared: tu lámpara no sabe si detrás hay una central eléctrica o un generador de pruebas.

## 4. Decisiones no obvias

- **El almacén conserva la sesión un minuto más allá de la vida máxima** (`ObtenerParaEvaluar`). `Obtener` ya dice "no existe" en el instante exacto, pero el servicio necesita saber que *venció* para responder `sesion-vencida-por-vida-maxima` en vez de un genérico `sin-sesion`.
- **Cada inicio de sesión recibe una clave nueva (fijación de sesión).** Si alguien planta en el navegador de otra persona una cookie suya (por ejemplo con un XSS) y la víctima termina un inicio de sesión, la sesión de la víctima no puede quedar bajo la clave de esa cookie: quien tuviera la copia entraría como ella. Cómo funciona el arreglo:
  - El manejador de cookies de ASP.NET Core, al iniciar sesión, primero lee la cookie de la petición. Si la clave existe en el almacén, la recuerda y llama a `RenewAsync(esaClave, ticket)`; solo si no la encuentra llama a `StoreAsync`, que crea una clave nueva. `SignOutAsync` no olvida la clave recordada, así que no sirve para forzar `StoreAsync`.
  - Por eso `EventosDeOidc.TicketReceived` lee la clave de la cookie **sin pasar por el manejador** (con `TicketDataFormat.Unprotect`; el ticket solo lleva esa clave) y **borra esa sesión del almacén antes** de que el manejador guarde la nueva. El manejador no encuentra nada, llama a `StoreAsync` y la cookie nueva lleva una clave nueva. La cookie anterior ya no abre nada.
  - Con otro `sid` u otra área, además, se cierra la sesión anterior en Keycloak (`CerrarAsync`). Con el mismo `sid` (cambiar la clave), la sesión nueva conserva el `Inicio` de la anterior (RN-22) y Keycloak no se cierra (cerraría la sesión que acaba de entrar).
  - `RenewAsync` solo reemplaza, bajo una clave existente, una sesión de la **misma** identidad (mismo `sid` y misma área): nunca guarda ahí a otra persona.
- **`AlmacenDeSesiones.ObtenerParaEvaluar` y la retención de 1 minuto.** `Obtener` ya dice "no existe" al cumplirse la vida máxima, pero el servicio necesita saber que la sesión *venció* para responder `sesion-vencida-por-vida-maxima` y no un `sin-sesion` genérico.
- **`ForwardedHeaders` configurable.** `Bff:RedesDeConfianza` (lista de redes CIDR) limita de quién se aceptan `X-Forwarded-*`. Sin valor se confía en cualquier origen: el supuesto es que el BFF no publica puertos y solo lo alcanza el contenedor web. Si algún día se publica el 5180, hay que rellenar la lista.
- **`Eliminar` libera el `SemaphoreSlim`** solo si nadie lo tiene tomado: quien cierra la sesión desde dentro del candado hará `Release` sobre él y un `Dispose` previo lo rompería.
- **Un mapper `preferred-username-en-token-de-identidad` en el cliente `core-bancario-web`.** Con solo los *scopes* `basic` y `roles`, Keycloak no pone el nombre de usuario (`cc1234567`) en el token de identidad, y el BFF lo necesita para `SesionDto.usuario`. El mapper añade solo ese claim, y solo al token de identidad (no al access token que ve la API); el *scope* `profile` habría traído además nombre, apellidos y otros datos personales.
- **`auth_time` tiene resolución de segundos.** Si un inicio de sesión ocurrió "ahora" (dentro del último segundo), `Inicio` es ahora y no hasta un segundo antes por el redondeo.
- **El destino de la redirección viaja en el `state` protegido** del flujo OIDC, nunca en una cookie legible ni en la URL de Keycloak.

## 5. Para practicar (Copiar → Modificar → Recrear)

1. **Una regla pura nueva.** *Copiar* `RutaDeRegreso` y su teoría `F004_CL08_RutaDeRegreso_Normalizar`. *Modificar*: añade la regla "no se acepta una ruta que contenga `..`" escribiendo primero un caso en la teoría (verás cómo falla) y luego el cambio mínimo. *Recrear*: borra tu cambio y escríbelo de nuevo de memoria, empezando por la prueba.
2. **Un código de error nuevo en el BFF.** *Copiar* cómo `CodigosDelBff.De(MotivoDeCierre)` traduce un motivo a un código. *Modificar*: imagina el motivo `Bloqueada` con el código `sesion-bloqueada`; sigue los errores del compilador hasta el contrato OpenAPI y observa cómo cambia `core-bancario-bff.json` y qué prueba lo exige. *Recrear*: explica con tus palabras por qué el *enum* del contrato es la fuente de los tipos de la SPA.
3. **Seguir una petición con un log.** *Copiar* el recorrido B de este documento. *Modificar*: en `ServicioDeSesion.ObtenerAccessTokenAsync`, añade un log que diga si renovó o no (sin imprimir ningún token), y ejecuta `F004_CL12_ProxyConTokenPorVencer_RenuevaYReenviaElNuevo`. *Recrear*: dibuja de memoria el recorrido B y marca en qué punto una petición podría llegar a la API sin pasar por `ProteccionCsrf` (pista: no puede).
