# ADR-0021: La inactividad la mide el BFF con la actividad deliberada del usuario

- Estado: Aceptada
- Fecha: 2026-10-26
- Feature: 004-front-base (RN-22, RN-25, RNF-06, RNF-07, CL-06, CL-07, CL-09; depende de ADR-0020)

## Contexto

RN-22 fija los límites: el cliente tiene 5 min de inactividad y 30 de vida máxima, y el operador, 15 min y 9 h. RN-25 dice **qué es actividad**: una interacción deliberada (clic, tecla o cambio de pantalla) en cualquier pestaña. Mover el ratón y hacer scroll no cuentan, y **las peticiones automáticas nunca cuentan**. RNF-06 exige cerrar como mucho 10 s después del límite, aunque la pestaña siga abierta haciendo peticiones solas. CL-06 y CL-07 piden un aviso 60 s antes. CL-09 pide que, pasada la inactividad, entrar de nuevo exija usuario, clave y código.

Hallazgos del experimento de PKCE del autor (2026-10-14, Keycloak real):

- para Keycloak, "inactividad" es **que nadie le pida nada**. El canje del código y cada renovación cuentan como actividad;
- si algo renueva el token en segundo plano, **la sesión de Keycloak no vence nunca**;
- tras 6 min 42 s sin actividad, Keycloak pidió login otra vez y la cookie SSO no reabrió la sesión. Fue una sola medición, en `start-dev` con H2.

Conceptos para leer esta ADR:

- **Dos relojes:** el de Keycloak (su sesión SSO, que se alarga con cada renovación de token) y el del BFF (la sesión del navegador, ADR-0020). Si el reloj de RN-25 fuera el de Keycloak, una pestaña abierta que refresca datos sola mantendría viva la sesión para siempre. Analogía: el temporizador de luz de un pasillo que se reactiva con cualquier movimiento, incluso el de una cortina. RN-25 pide uno que solo cuente a las personas.
- **Latido de actividad (*heartbeat*):** un aviso explícito "el usuario hizo algo", separado de las peticiones de datos. Así el servidor no tiene que adivinar qué petición fue deliberada.
- **`prompt=login` (OIDC):** un parámetro que obliga al proveedor a pedir credenciales aunque tenga una sesión SSO viva en su cookie.

## Opciones consideradas

**Cómo distingue el BFF lo deliberado de lo automático:**

1. **Un encabezado en las peticiones deliberadas** (`X-Actividad: 1` en las que nacen de un clic). En contra: la capa de datos (TanStack Query) no sabe si una consulta nace de un cambio de pantalla o de un refresco automático. Habría que marcar cada llamada a mano, y un olvido haría contar como actividad una petición automática.
2. **Latido separado:** la SPA detecta la interacción deliberada y la informa con `POST /bff/actividad`. **Ninguna** petición `/api/**` cuenta, nunca. A favor: la regla de RN-25 queda en un solo sitio de cada lado y es fácil de probar ("las peticiones del proxy no tocan `UltimaActividad`"). En contra: una petición más, y el aviso puede llegar con algo de retraso (se acota abajo).
3. **Que la SPA cierre sola por su temporizador y el BFF no mida nada.** En contra: el navegador no es de fiar. Un script, una pestaña dormida o un reloj manipulado dejarían la sesión abierta en el servidor, y RNF-06 no se cumpliría.

**Cómo se evita que Keycloak reabra la sesión (CL-09):**

1. Confiar en que la sesión SSO de Keycloak venza a la vez. En contra: el proxy renueva tokens por peticiones automáticas y alarga la sesión de Keycloak más allá de la del BFF.
2. **`prompt=login` en todo inicio de sesión** y, además, cerrar la sesión de Keycloak por el canal trasero cuando el BFF detecta el vencimiento.

## Decisión

**El BFF es el reloj de la sesión. Keycloak es la red de seguridad.**

1. **`SesionDelNavegador`** guarda `Inicio` y `UltimaActividad`:
   - `Inicio` es el `auth_time` del token de identidad, para alinearlo con el inicio de la sesión de Keycloak. Si el regreso trae el mismo `sid` (cambio de clave), se conserva;
   - `UltimaActividad` solo la cambian el regreso del login y `POST /bff/actividad`.
2. **`PoliticaDeSesion` (función pura):** la sesión vence por vida máxima si `ahora ≥ Inicio + VidaMaxima` (y esta manda si vencen las dos). Vence por inactividad si `ahora ≥ UltimaActividad + Inactividad`. Los límites salen de la configuración del BFF por área, y una prueba de configuración comprueba que coinciden con los realm JSON (RN-22).
3. **Toda petición al BFF evalúa la política.** Si la sesión venció, el BFF la cierra en Keycloak por el canal trasero, la borra y responde 401 con el motivo (`sesion-vencida-por-inactividad` o `sesion-vencida-por-vida-maxima`). Esto cumple RNF-06 en el servidor **justo** al límite, aunque la pestaña siga pidiendo datos.
4. **`POST /bff/actividad`** (latido):
   - **renueva los tokens en Keycloak**, y así alinea el reloj de inactividad de Keycloak con el del BFF: los dos parten del mismo instante;
   - después registra `UltimaActividad = ahora`;
   - si Keycloak rechaza la renovación (`invalid_grant`: sesión cerrada o acceso deshabilitado, CL-12), cierra con el motivo `sesion-revocada`;
   - si Keycloak no responde, registra la actividad igualmente.
5. **El proxy `/api/**` nunca registra actividad.** Solo renueva el access token si le quedan menos de 30 s de vida (una necesidad técnica, no actividad).
6. **La SPA** (`crearRegistroDeActividad`):
   - escucha `click` y `keydown` en `document` y los cambios de ruta. **No** escucha `mousemove`, `pointermove`, `wheel` ni `scroll`;
   - envía el latido de inmediato si pasaron ≥ 15 s desde el último. Si no, lo deja pendiente y lo envía al cumplirse los 15 s;
   - **El retraso máximo es de 15 s, menor que los 60 s del aviso:** cuando llega la hora del aviso, cualquier interacción ya se informó, y el aviso no aparece por error.
7. **Alineación de relojes:**
   - el BFF devuelve **duraciones** (`segundosHastaInactividad`, `segundosHastaVidaMaxima`), no instantes, así que un reloj del navegador desajustado no importa;
   - la SPA calcula sus plazos con la hora local de recepción;
   - el aviso aparece 60 s antes del primer cierre, y "Seguir conectado" es un latido (CL-06);
   - el aviso de vida máxima no tiene opción de alargar (CL-07);
   - al llegar a cero, la SPA hace `POST /bff/salir` y lleva al inicio con el mensaje. Aunque el temporizador de una pestaña dormida se retrase, el servidor ya rechaza en el instante exacto (punto 3).
8. **Pestañas (RNF-07):**
   - un `BroadcastChannel` reparte a las demás pestañas los plazos nuevos (tras cada latido o consulta) y el cierre;
   - al recibir un cierre, cada pestaña vacía la caché de datos y va al inicio. La entrega es inmediata, muy por debajo de los 5 s;
   - si un mensaje se pierde, la siguiente petición de esa pestaña recibe 401;
   - no se usa `localStorage`.
9. **CL-09:**
   - **todo inicio de sesión envía `prompt=login`**. Keycloak pide credenciales aunque su cookie SSO siga viva, por ejemplo porque una renovación automática la alargó;
   - "Cambiar clave" (`kc_action=UPDATE_PASSWORD`) no lo envía: necesita la sesión viva, y `max_auth_age = 0` ya obliga a escribir la clave actual (ADR-0014).

El motivo principal es que RN-25 es una regla del banco sobre **personas**, y solo la app sabe qué hizo una persona. Keycloak sabe cuándo le pidieron tokens, no por qué.

## Consecuencias

- Un usuario activo envía como mucho un latido cada 15 s, y cada uno renueva tokens en Keycloak (rotación con `maxReuse 0`). Las renovaciones de una sesión se serializan con un candado en el almacén (ADR-0020).
- **El cierre del BFF manda.** La sesión de Keycloak puede durar algo más que la del BFF si el proxy la renovó por una petición automática. Por eso el BFF la cierra al detectar el vencimiento, y `prompt=login` impide que la reabra.
- **Límite aceptado:** si el usuario cierra la pestaña y no vuelve, la sesión del BFF queda en memoria hasta que vence su vida máxima (se libera sola) y la de Keycloak, hasta su inactividad. Ninguna de las dos puede reabrirse sin credenciales. Un barrido periódico no se construye (YAGNI): ningún requisito lo pide.
- Los valores de RN-22 quedan en **dos sitios** (realm JSON y `appsettings.json` del BFF). Una prueba de configuración los compara.
- Se prueba sin esperar 5 ni 30 min:
  - con funciones puras y `FakeTimeProvider` en el BFF;
  - con temporizadores falsos de Vitest y el reloj de Playwright en la SPA;
  - con tiempos cortos, por configuración y por la Admin API, en las pruebas contra Keycloak real (ADR-0018).

## Decisiones del autor

Comprobación de la pregunta abierta 3 del plan (2026-10-26), en sus palabras:

1. **Por qué el reloj de Keycloak no sirve para RN-25:** *"Porque Keycloak no sabe si la actividad viene de una persona o de la aplicación. Si el BFF renueva tokens automáticamente o la SPA hace peticiones periódicas, Keycloak interpreta que la sesión sigue activa aunque el usuario lleve varios minutos sin tocar nada. Para cumplir RN-25 hace falta que el BFF lleve su propio reloj y reinicie el contador solo cuando detecte una interacción deliberada del usuario."*
2. **Qué protege `prompt=login`:** *"Si alguien se sienta delante del ordenador y pulsa 'Iniciar sesión', Keycloak verá su propia sesión SSO todavía activa y devolverá un nuevo código sin mostrar la pantalla de login. El usuario volvería a entrar sin escribir clave ni segundo factor, aunque el BFF ya hubiera considerado expirada la sesión por inactividad. Por eso se usa prompt=login: obliga a pedir credenciales otra vez y hace cumplir la regla de inactividad definida por el negocio."*

Matiz que añadió Claude: `prompt=login` es la segunda barrera. La primera es el cierre de la sesión de Keycloak por el canal trasero cuando el BFF detecta el vencimiento. Son dos capas independientes (defensa en profundidad), y por eso el riesgo 1 del plan, que ese cierre no sea completo, no rompe CL-09.
