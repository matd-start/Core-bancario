# ADR-0015: Validación local de JWT con una ventana de revocación de 5 minutos

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api (concreta cómo confía la API en los tokens de ADR-0014)

## Contexto

Con Keycloak como emisor (ADR-0014), la API recibe en cada petición un JWT firmado por uno de dos realms. Hay que decidir:

1. **Cómo se valida:** preguntando a Keycloak en cada petición, o comprobando la firma localmente.
2. **Cuánto tarda en dejar de valer** un acceso deshabilitado. RNF-01 fija un máximo de **5 min, contando la tolerancia de reloj**. El `ClockSkew` por defecto de ASP.NET Core es de 5 min, así que solo él ya agotaría la ventana: con una vida del token de 5 min, la ventana real sería de hasta 10.
3. **Cómo se acepta a dos emisores** sin que un token de `clientes` pueda actuar como Operador.
4. **Con qué algoritmos** (`ValidAlgorithms`).

Tres conceptos para leer esta ADR:

- **Validación local con JWKS:** el realm publica sus claves públicas (JWKS) y la API las descarga y guarda. Para cada token comprueba la firma, el emisor, la audiencia y el vencimiento sin llamar a nadie. Analogía: un guardia que verifica el holograma de un carné en vez de llamar a la oficina que lo expidió. Es rápido y funciona aunque la oficina esté cerrada, pero no se entera de que alguien acaba de anular ese carné.
- **Introspección:** preguntar al emisor en cada petición si el token sigue vivo. Revoca al instante, pero hace a la API dependiente de Keycloak en cada petición y suma latencia.
- **`ClockSkew` (tolerancia de reloj):** margen que se acepta por si los relojes de Keycloak y de la API no coinciden exactamente. Se suma a la vida del token.

## Opciones consideradas

**Revocación:**

1. **Validación local con access tokens cortos** (270 s de vida + 30 s de tolerancia = 300 s). A favor: sin dependencia de Keycloak en cada petición; sin latencia añadida; la API sigue funcionando si Keycloak cae. En contra: un acceso deshabilitado puede seguir usándose hasta 5 min.
2. **Introspección en cada petición.** A favor: revocación inmediata. En contra: cada petición depende de Keycloak (si cae, cae la API), suma latencia y carga, y contradice la decisión de RNF-01.
3. **Validación local + lista de revocación propia** (la API consulta en la base los accesos deshabilitados). A favor: revocación casi inmediata. En contra: una consulta por petición, una caché que invalidar y otra fuente de verdad del estado del acceso, que ADR-0017 dejó solo en Keycloak.

**Dos emisores:**

1. **Dos esquemas `JwtBearer` (uno por realm) + un esquema de política que elige por el `iss` del token.** Es el patrón documentado de ASP.NET Core para varios emisores. Cada esquema tiene su `Authority`, su `ValidIssuer` y sus eventos, así que el rol de la API depende de **qué esquema** validó el token.
2. **Un solo esquema con dos emisores válidos y un resolvedor de claves propio.** Menos registro, pero hay que escribir a mano la resolución de las dos JWKS, y el rol ya no se ata al realm de forma natural.

**Mapeo de roles** (Keycloak los envía en `realm_access.roles` y ASP.NET Core no los lee solo):

1. **En el evento `TokenValidated` de cada esquema.** El esquema `Clientes` solo puede añadir el rol `Cliente`, y `Empleados` solo `Operador`.
2. **`IClaimsTransformation`.** Se ejecuta en cada autenticación y no sabe qué esquema validó el token.
3. **Un mapper en Keycloak que emita un claim `roles` plano.** No hace falta código, pero el rol dependería solo de la configuración del realm.

## Decisión

- **Validación local** (revocación, opción 1). Vida del access token **270 s** en los dos realms y **`ClockSkew` = 30 s**, fijado como constante en el código (`OpcionesDeAutenticacion.ToleranciaDeReloj`) para que una configuración no pueda romper RNF-01. Suman 300 s.
- **`ValidAlgorithms = ["RS256"]`** en los dos emisores, igual al `defaultSignatureAlgorithm` de los realms. Así se cierran ataques conocidos de JWT: `alg: none` o un HMAC firmado con la clave pública.
- **Dos esquemas + selector por `iss`** (opción 1). El selector lee el `iss` **sin validarlo**, solo para elegir quién valida. Si no coincide con `empleados`, el token va a `clientes`, que lo rechaza y registra el motivo.
- **Rol en `TokenValidated`** (opción 1). Además, el token debe traer `sub`: sin él se rechaza, porque la API no tendría a quién atribuir la operación (RN-19).
- Cada esquema: `MapInboundClaims = false` (el `sub` sigue llamándose `sub`), `IncludeErrorDetails = false` y `RequireHttpsMetadata = true` salvo en Development. `ValidIssuer` y `ValidAudience = core-bancario-api` son explícitos. Keycloak pone la audiencia con un mapper.
- Las respuestas son **401** `no-autenticado` sin `detail`, con `WWW-Authenticate: Bearer` sin explicación, y **403** `prohibido`, ambas en `ProblemDetails` (ADR-0011). El **motivo** del 401 (vencido, firma, emisor, audiencia, algoritmo, sin `sub`) **solo va al log** y nunca con el token (RNF-04).
- Política por defecto: **usuario autenticado**. Una ruta nueva sin política queda cerrada, no abierta.

El motivo principal es el equilibrio que eligió el autor: una ventana de riesgo pequeña y predecible, a cambio de no depender de Keycloak en cada petición.

## Consecuencias

- Deshabilitar o restablecer un acceso (RF-13) impide **renovar** la sesión al instante, pero el access token ya entregado se acepta **hasta 5 min**. Es el límite aceptado de RNF-01 y debe estar en el README. Si un requisito futuro pidiera revocación inmediata (por ejemplo, transferencias de alto valor), la opción 3 se puede añadir solo a esas rutas.
- El cliente de la SPA renueva el token cada menos de 270 s. Para el cliente, 270 s < 300 s de inactividad: la renovación llega a tiempo mientras haya actividad.
- Una prueba de configuración lee los realm JSON y la `ClockSkew` efectiva de cada esquema y falla si la suma supera 300 s.
- Las pruebas de la API firman tokens con claves RSA propias para los dos emisores. Inyectan una configuración OIDC fija, sin descargar nada (ADR-0018).
- Hay que vigilar que nadie cambie `ClockSkew` ni `accessTokenLifespan` por separado: los dos números forman una sola regla.

## Decisiones del autor

Sobre la ventana de revocación (sección 11 de la spec), en sus palabras: *"existe un equilibrio entre seguridad y operatividad. Exigir revocación inmediata de todos los access tokens obligaría a que la API consultara constantemente al servidor de identidad o mantuviera mecanismos adicionales de invalidación. En cambio, usar access tokens de vida corta limita el riesgo a una ventana pequeña y predecible"*.

Matices que añadió en la entrevista: validar sin consultar al emisor también permite que la API siga funcionando si el emisor se cae, y la ventana de 5 min debe incluir la tolerancia de reloj (con el `ClockSkew` por defecto serían 10).

El reparto 270 s + 30 s y `ValidAlgorithms = RS256` los fijó el autor al revisar la sección 10 de la spec.
