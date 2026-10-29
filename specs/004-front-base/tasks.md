# Tareas — 004 Front base: inicio de sesión, sesión segura y pantallas del operador

Plan: [plan.md](plan.md)

El Build va en **dos bloques** (plan, sección 1.1), y cada bloque lleva **dos PR**: primero el de `core-bancario` y, cuando está mergeado, el de `core-bancario-web` (ADR-0005: el contrato cambia primero en la API). Cada PR tiene su commit de pruebas en rojo, su `/sdd-review` y su CI en verde. Cada uno compila, pasa sus pruebas y se puede mergear por sí solo.

Dentro de cada repositorio, el orden es de adentro hacia afuera:

- **`core-bancario`:** Application → Infrastructure → configuración de Keycloak → Api → Bff → operación;
- **`core-bancario-web`:** contratos y tipos → módulos puros → componentes → pantallas → operación.

Cada tarea compila por sí sola. El coder marca `[x]` al terminar cada una.

**Requisito previo (fuera de la 004):** `chore/kit-web` creó `core-bancario-web` con lo que pide la sección 1.2 del plan. Sin ello no empiezan las tareas del repositorio web.

**Ramas:**

- `core-bancario`: `feature/004a-sesion-bff` (renombrando la actual `feature/004-front-base`, que ya lleva spec, plan y ADRs) y después `feature/004b-estado-de-acceso` desde `main`;
- `core-bancario-web`: `feature/004a-sesion-y-cliente` y `feature/004b-backoffice`.

---

# Bloque 004a — Sesión y área del cliente

**Cubre:** CA-01…CA-21 y CA-34; CA-35 y CA-36 sobre las pantallas de 004a. RF-05 (cliente), RF-14, RF-15; RN-19, RN-21, RN-22, RN-24, RN-25, RN-26; CL-01…CL-14 y CL-24; RNF-01…RNF-16; D-09, D-12.

## PR 1 de 004a — repo `core-bancario`

**Antes de T01 (test-writer, commit `test(004): pruebas en rojo del bloque 004a`):**

- Crea `tests/CoreBancario.Bff.Tests` (xUnit v3 con MTP, **sin** `--ignore-exit-code 8`) y lo añade a `CoreBancario.slnx`, con:
  - `BffFixture` y sus reemplazos: `FakeTimeProvider`, `ClienteDeIdentidadFalso`, la `ApiFalsa` detrás de `IForwarderHttpClientFactory` y el `IStartupFilter` de `/__pruebas/sesion`, solo en `ConfigureTestServices`;
  - el proveedor de logs en memoria.

  Ver la sección 7 del plan, "Infraestructura de pruebas". Escribe las pruebas de Bff.Tests contra esqueletos de las firmas de la sección 3.3 (`// SDD: esqueleto creado por test-writer`). Para eso, el esqueleto del proyecto `src/CoreBancario.Bff` ya necesita sus paquetes.
- Keycloak.Tests:
  - secretos `KC_WEB_*`, `ssoSessionMaxLifespan` corto y varias URI de redirección;
  - `client_secret` en el canje y la renovación de `NavegadorSimulado`;
  - el BFF con `WebApplicationFactory<MarcadorDelBff>` y `NavegadorDelBff`;
  - las pruebas `F004_*` del bloque.
- Api.Tests:
  - **sustituye** `F003_ADR0014_ClienteWeb_EsPublicoYUsaCodigoConPkceS256` por `F004_ADR0020_ClienteWeb_EsConfidencialConPkceS256YSecretoComoMarcador`;
  - añade `F004_RNF12_ContratoOpenApi_EnumeraTodosLosCodigosDeError`, `F004_RNF02_ContratoOpenApi_LosGetSonSoloConsultas`, `F004_RN22_TiemposDelBff_CoincidenConLosRealms` y `F004_ADR0020_Metadatos_SeUsaComoDocumentoDeDescubrimiento`.
- En rojo:
  - las pruebas de configuración fallan porque el realm aún tiene el cliente público;
  - Bff.Tests y las de Keycloak.Tests con BFF fallan por los esqueletos;
  - **las pruebas existentes de Keycloak.Tests que canjean el código también fallan hasta T01** (cliente confidencial). Es lo esperado.

### Configuración de Keycloak

- [x] T01 — Cliente `core-bancario-web` confidencial y Keycloak alcanzable desde los contenedores:
  - en los dos realm JSON, los ajustes de la tabla de la sección 4 del plan: `publicClient: false`, `clientAuthenticatorType: client-secret`, `secret` con el marcador `${KC_WEB_CLIENTES_SECRET}` / `${KC_WEB_EMPLEADOS_SECRET}`, `redirectUris` exactas a `/bff/signin-oidc/{area}`, `webOrigins: []`, `post.logout.redirect.uris` vacío y PKCE `S256` conservado. Hay que actualizar la descripción;
  - en `docker-compose.yml`, servicio `keycloak`: las dos variables nuevas (obligatorias con `:?`), `KC_HOSTNAME: http://localhost:${KEYCLOAK_PORT:-8080}` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC: "true"`;
  - en `.env.example`, las dos variables sin valor, con el comentario de que el mismo valor va en los user-secrets del BFF (`Bff:Clientes:ClientSecret` y `Bff:Empleados:ClientSecret`);
  - en `ci.yml`, job `entorno-local`: generar y ocultar los dos secretos nuevos.

  Verificar en local, **recreando el volumen**, que el descubrimiento de `clientes` sigue publicando `issuer` `http://localhost:8080/realms/clientes` (riesgo 2) · Repo: core-bancario · Capa: Configuración de Keycloak · Archivos: `infra/keycloak/realms/clientes-realm.json`, `infra/keycloak/realms/empleados-realm.json`, `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml` · Cubre: RF-14, RN-21, RN-24, RNF-01, RNF-05, RNF-16, CL-03, CA-01, CA-03, ADR-0020 · Pruebas en verde: `F004_ADR0020_ClienteWeb_…`, todas las de Keycloak.Tests de la 003 (con el secreto)

### Api

- [x] T02 — Contrato y compose:
  - `OpcionesDeEmisor.Metadatos` (opcional) y, en `ConfiguracionDeAutenticacion.ConfigurarEsquema`, `jwt.MetadataAddress = Metadatos` si no es null (no cambia `ValidIssuer`);
  - `CodigosDeErrorEnOpenApi : IOpenApiDocumentTransformer` (*enum* `CodigoDeError` por reflexión sobre `CodigosDeError`), registrado en `AddOpenApi`;
  - regenerar y versionar `src/CoreBancario.Api/openapi/core-bancario-api.json`.

  Repo: core-bancario · Capa: Api · Archivos: `src/CoreBancario.Api/Autenticacion/{OpcionesDeAutenticacion,ConfiguracionDeAutenticacion}.cs`, `src/CoreBancario.Api/OpenApi/CodigosDeErrorEnOpenApi.cs`, `src/CoreBancario.Api/Program.cs`, `src/CoreBancario.Api/openapi/core-bancario-api.json` · Cubre: RNF-12, RNF-16, CA-34, regla "ningún GET cambia datos" · Pruebas en verde: `F004_RNF12_ContratoOpenApi_…`, `F004_RNF02_ContratoOpenApi_…`, `F004_ADR0020_Metadatos_…`

### Bff (proyecto nuevo `src/CoreBancario.Bff`)

- [x] T03 — Esqueleto del host:
  - `CoreBancario.Bff.csproj` (Web SDK, `net10.0`, nullable, `UserSecretsId`, `OpenApiDocumentsDirectory=openapi`, `--file-name core-bancario-bff`, y los paquetes de la sección 3.3 del plan);
  - `MarcadorDelBff`, `AreaDeSesion`, `AreasDeSesion` y `OpcionesDelBff`/`OpcionesDeLaApi`/`OpcionesDeArea`;
  - `appsettings.json`: `Bff:Clientes` 00:05:00/00:30:00, `Bff:Empleados` 00:15:00/09:00:00, `ClientId`, logs de `Microsoft.AspNetCore` y `Yarp` en `Warning`;
  - `appsettings.Development.json`: emisores `http://localhost:8080/realms/…` y `Bff:Api:Url` `http://localhost:5096`;
  - `Properties/launchSettings.json` en `http://localhost:5180`;
  - un `Program.cs` mínimo que arranca;
  - añadir el proyecto a `CoreBancario.slnx`.

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/{CoreBancario.Bff.csproj,Program.cs,MarcadorDelBff.cs,appsettings.json,appsettings.Development.json,Properties/launchSettings.json}`, `src/CoreBancario.Bff/Configuracion/{AreaDeSesion,AreasDeSesion,OpcionesDelBff}.cs`, `CoreBancario.slnx` · Cubre: RN-22, RNF-04 (logs), D-12 · Pruebas en verde: `F004_RN22_TiemposDelBff_CoincidenConLosRealms`

- [x] T04 — Reglas puras de la sesión:
  - `TokensDeLaSesion` (con `ToString` oculto) y `RolDeSesion`;
  - `SesionDelNavegador`, con guardas, `RegistrarActividad` que nunca retrocede y `ReemplazarTokens`;
  - `EstadoDeSesion`, `PlazosDeSesion` y `PoliticaDeSesion` ("≥" en el límite; vida máxima manda; plazos nunca negativos);
  - `LectorDeRol` (lee `realm_access.roles` con `JsonWebTokenHandler.ReadJsonWebToken`, sin validar; `null` si no se puede leer);
  - `RutaDeRegreso.Normalizar`, con las reglas exactas de su resumen.

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/Sesiones/{TokensDeLaSesion,RolDeSesion,SesionDelNavegador,EstadoDeSesion,PlazosDeSesion,PoliticaDeSesion,LectorDeRol}.cs`, `src/CoreBancario.Bff/InicioDeSesion/RutaDeRegreso.cs` · Cubre: RN-19, RN-22, RNF-06, CL-04, CL-07, CL-08, CA-07, CA-09, CA-13, CA-15

- [x] T05 — Frontera con Keycloak:
  - `IClienteDeIdentidad`, `TokensRenovados`, `RenovacionRechazadaException` e `IdentidadSinRespuestaException`;
  - `ClienteDeIdentidadKeycloak`:
    - endpoints sacados del `ConfigurationManager` del esquema OIDC del área;
    - `RenovarAsync` (400 `invalid_grant` → rechazada; sin respuesta o 5xx → sin respuesta);
    - `CerrarSesionAsync` (POST con `refresh_token` y credenciales; nunca lanza);
    - `EstaDisponibleAsync` (descubrimiento, 3 s).

    Nunca registra tokens ni cuerpos.

  Se registra con `AddHttpClient` en T07 · Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/Identidad/{IClienteDeIdentidad,TokensRenovados,RenovacionRechazadaException,IdentidadSinRespuestaException,ClienteDeIdentidadKeycloak}.cs` · Cubre: RF-14, CL-05, CL-12, CA-16

- [x] T06 — Almacén y servicio de sesión:
  - `AlmacenDeSesiones : ITicketStore`:
    - `NuevaClave` de 256 bits;
    - `StoreAsync` construye la `SesionDelNavegador` desde el ticket y no deja los tokens en el ticket guardado;
    - `RetrieveAsync` pone `PropiedadesDeSesion.Clave`;
    - caducidad en `Inicio + VidaMaxima`;
    - `ConCandadoAsync` por clave;
  - `MotivoDeCierre`, `SesionTerminadaException` y `SesionDto`;
  - `ServicioDeSesion`: `ConsultarAsync` (sin actividad); `RegistrarActividadAsync` (renueva y después registra, con candado); `ObtenerAccessTokenAsync` (renueva solo con < 30 s, con candado y volviendo a mirar dentro, **nunca** registra actividad); `CerrarAsync` (canal trasero + borrado, idempotente). Toda operación evalúa la política primero, y si la sesión venció, la cierra.

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/Sesiones/{AlmacenDeSesiones,MotivoDeCierre,SesionTerminadaException,SesionDto,ServicioDeSesion}.cs`, `src/CoreBancario.Bff/InicioDeSesion/PropiedadesDeSesion.cs` · Cubre: RN-22, RN-25, RNF-01, RNF-06, CL-09, CL-12, CA-05, CA-07, CA-10, CA-16 · Pruebas en verde: `F004_CA07_…`, `F004_CA09_…`, `F004_CL12_RenovacionRechazada_…`, `F004_RNF06_…`, `F004_RNF01_TokensDeLaSesion_…`

- [x] T07 — Inicio de sesión OIDC:
  - `ConfiguracionDeSesion.AddSesionDelNavegador`, con exactamente las opciones de su resumen (sección 3.3 del plan): cookie `cb-sesion`/`__Host-cb-sesion`, dos esquemas OIDC, `ResponseMode` `query`, PAR desactivado, PKCE, `SaveTokens`, solo `openid` y correlación `Lax`;
  - `EventosDeOidc`: `prompt=login` o `kc_action`; `iss` en `MessageReceived`; aviso de cambio de clave según `kc_action_status` en `TokenValidated`; RN-26 en `TicketReceived` (mismo `sid` conserva `Inicio`, otro `sid` cierra la anterior); `RemoteFailure` sin errores técnicos;
  - `Avisos`;
  - registros: `AlmacenDeSesiones` (singleton), `ServicioDeSesion` (scoped), `AddHttpClient<IClienteDeIdentidad, ClienteDeIdentidadKeycloak>` (10 s) y `TryAddSingleton(TimeProvider.System)`.

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/InicioDeSesion/{ConfiguracionDeSesion,EventosDeOidc,Avisos}.cs` · Cubre: RF-14, RF-15, RN-19, RN-21, RN-24, RN-26, RNF-01, RNF-05, CL-01, CL-02, CL-03, CL-08, CL-09, CL-10, CA-01, CA-02, CA-04, CA-10, CA-13, CA-14, CA-21

- [x] T08 — Seguridad y errores:
  - `ProteccionCsrf`, con su lista exacta de exenciones (iniciar sesión, cambiar clave y los dos `CallbackPath`), `X-CSRF: 1` y `Sec-Fetch-Site` igual a `same-origin`;
  - `EncabezadosDeSeguridad` (`no-store`, `nosniff`);
  - `CodigosDelBff` y `RespuestasDelBff` (`ProblemDetails` con `codigo` y `traceId`, sin `detail`).

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/Seguridad/{ProteccionCsrf,EncabezadosDeSeguridad}.cs`, `src/CoreBancario.Bff/Errores/{CodigosDelBff,RespuestasDelBff}.cs` · Cubre: RNF-02, RNF-12, CL-11, CA-06, CA-12

- [x] T09 — Endpoints, *proxy* y cableado:
  - `EndpointsDelBff`: las cinco rutas de la sección 3.1, con `WithName`. `iniciar-sesion` hace estas comprobaciones en orden: área válida → sesión de otra área → sesión de la misma área → `EstaDisponibleAsync` → `Challenge`. `cambiar-clave` exige sesión de clientes. `actividad` y `salir` solo aceptan `POST`;
  - `ProxyDeLaApi`: paso previo que resuelve el token o responde 401/503 sin reenviar, y después `MapForwarder` con las transformaciones de su resumen;
  - `CodigosDelBffEnOpenApi`;
  - `Program.cs` en el orden del plan (sección 3.3);
  - regenerar y versionar `src/CoreBancario.Bff/openapi/core-bancario-bff.json`. En `ci.yml`, el paso "Contrato OpenAPI al día" compara también `src/CoreBancario.Bff/openapi`.

  Repo: core-bancario · Capa: Bff · Archivos: `src/CoreBancario.Bff/Endpoints/EndpointsDelBff.cs`, `src/CoreBancario.Bff/Proxy/ProxyDeLaApi.cs`, `src/CoreBancario.Bff/OpenApi/CodigosDelBffEnOpenApi.cs`, `src/CoreBancario.Bff/Program.cs`, `src/CoreBancario.Bff/openapi/core-bancario-bff.json`, `.github/workflows/ci.yml` · Cubre: RF-14, RF-15, RN-25, RN-26, RNF-01, RNF-02, RNF-04, CL-05, CL-08, CL-10, CA-01, CA-05, CA-06, CA-07, CA-13, CA-14, CA-21 · Pruebas en verde: todo Bff.Tests y todas las `F004_*` de Keycloak.Tests

### Operación y cierre

- [x] T10 — Compose con perfiles y Dockerfiles:
  - `src/CoreBancario.Api/Dockerfile` y `src/CoreBancario.Bff/Dockerfile` (multietapa `sdk:10.0` → `aspnet:10.0`, puerto 8080, sin generar OpenAPI en la imagen: `-p:OpenApiGenerateDocuments=false`);
  - en `docker-compose.yml`, servicios `api` y `bff` (perfil `app`) y `web` (perfil `web`, `build: ${CORE_BANCARIO_WEB_DIR:-../core-bancario-web}`, `5173:8080`), con la configuración de la tabla "Compose" del plan. `api` y `bff` **sin puertos publicados**;
  - en `.env.example`: `CORE_BANCARIO_WEB_DIR` comentado;
  - en `ci.yml`, job `entorno-local`: `docker compose --profile app up -d --build --wait` y comprobación de `/health` de la API y respuesta 401 de `/bff/sesion` desde un contenedor de la red.

  Repo: core-bancario · Capa: Operación · Archivos: `src/CoreBancario.Api/Dockerfile`, `src/CoreBancario.Bff/Dockerfile`, `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml` · Cubre: RNF-01 (API no expuesta), RNF-02 (sin CORS), RNF-16

- [x] T11 — Documentación y verificación del PR:
  - README, sección nueva "Aplicación web y BFF":
    - arquitectura (navegador → nginx/Vite → BFF → API, y BFF ↔ Keycloak);
    - por qué no hay tokens en el navegador;
    - comando único y requisito de los dos repos uno al lado del otro;
    - **recrear el volumen de Keycloak** y los dos secretos nuevos;
    - user-secrets del BFF;
    - límites conocidos (sesiones en memoria, revocación de hasta 270 s sin actividad, CL-15);
  - en `CLAUDE.md` (solo estas líneas): en "Estructura", `src/CoreBancario.Bff/` (BFF: sesión del navegador y proxy a la API, ADR-0020) y `tests/CoreBancario.Bff.Tests/`; en "Stack", una línea "Sesión del navegador: BFF propio, sin tokens en JavaScript (ADR-0020, ADR-0021)". La lista "Pendiente de ADR" no cambia: D-09 y D-12 no estaban en ella;
  - en `docs/producto/spec-fase-1.md`, "Resueltas": "D-09 → ADR-0022, D-12 → ADR-0020";
  - verificación: `dotnet format --verify-no-changes`, `dotnet build -c Release -warnaserror` (los dos contratos quedan regenerados y versionados), `dotnet test` con Docker, CI en verde y `/sdd-review`. Comprobación manual: ningún log del BFF contiene un token ni un número de documento.

  Repo: core-bancario · Capa: transversal · Archivos: `README.md`, `CLAUDE.md`, `docs/producto/spec-fase-1.md` · Cubre: RNF-14, RNF-16, D-09, D-12

## PR 2 de 004a — repo `core-bancario-web` (después de mergear el PR 1)

**Antes de T12 (test-writer, commit `test(004): pruebas en rojo del bloque 004a`):**

- Configura Playwright con los proyectos `simulado` y `real` y las ayudas `e2e/ayudas/{totp,keycloak,bff}.ts` (plan, sección 7).
- Prepara MSW con manejadores por defecto para `/bff/sesion` y `/api/mis-cuentas`.
- Escribe las pruebas Vitest, simuladas y reales del bloque contra los esqueletos de la sección 3.4 del plan.

### Contratos y API

- [x] T12 — Contratos y tipos:
  - `contratos/core-bancario-api.json` y `contratos/core-bancario-bff.json`, copiados de `../core-bancario` (`main`);
  - el script `contratos:sincronizar` (copia desde `${CORE_BANCARIO_DIR:-../core-bancario}`);
  - el script `tipos` (`openapi-typescript` 7.13.0 → `src/api/generado/api.d.ts` y `bff.d.ts`);
  - los dos `.d.ts` versionados;
  - paquetes de ADR-0022 con versiones exactas en `package.json`.

  Repo: core-bancario-web · Capa: contratos · Archivos: `contratos/*.json`, `src/api/generado/*.d.ts`, `package.json`, `package-lock.json` · Cubre: RNF-13, D-09

- [x] T13 — Clientes HTTP y errores:
  - `crearClienteDeLaApi`, `crearClienteDelBff` y `crearMiddlewareDeSesion` (`X-CSRF: 1`, 401 → motivo, sin reintentos);
  - `MENSAJES_DE_ERROR: Record<CodigoDeError, string>` (incluido "La cuenta cambió mientras tanto" y el de reintentar sin riesgo de CL-19), `MENSAJE_GENERICO` e `interpretarError` (nunca `detail`);
  - `clienteDeConsultas.ts` con la configuración de la sección 3.4 del plan.

  Repo: core-bancario-web · Capa: api · Archivos: `src/api/{clientes,errores,mensajes}.ts`, `src/app/clienteDeConsultas.ts` · Cubre: RF-05, RNF-12, CL-13, CL-24, CA-18, CA-34

### Módulos puros y componentes comunes

- [x] T14 — Comunes:
  - `formatearDinero` (texto a `Intl.NumberFormat`, nunca `number`);
  - componentes `Boton` (`enCurso` → `disabled` + `aria-busy`), `Dialogo` y `DialogoDeConfirmacion` sobre React Aria, `MensajeDeError` (con "Reintentar" opcional y el `traceId`), `Cargando` y `EstadoVacio`;
  - estilos base con `:focus-visible` y *mobile-first*.

  Repo: core-bancario-web · Capa: comun · Archivos: `src/comun/dinero.ts`, `src/comun/componentes/*.tsx`, `src/comun/estilos/*.css` · Cubre: RNF-08, CL-14, CL-15, CL-24, CA-18, CA-19, CA-35

- [x] T15 — Reloj, actividad y pestañas (puros):
  - `aPlazosLocales` y `evaluarReloj`;
  - `crearRegistroDeActividad` (15 s, pendiente y `vaciar`) y `escucharInteraccionesDeliberadas` (solo `click` y `keydown`);
  - `crearCanalDeSesion` (`BroadcastChannel`).

  Repo: core-bancario-web · Capa: sesion · Archivos: `src/sesion/{relojDeSesion,registroDeActividad,canalDeSesion}.ts` · Cubre: RN-22, RN-25, RNF-07, CL-06, CL-07, CA-07, CA-08, CA-09, CA-11

### Pantallas

- [x] T16 — Sesión en la interfaz:
  - `ProveedorDeSesion`/`useSesion`, con el comportamiento de su resumen: consulta inicial, latidos por interacción y por cambio de ruta, un solo `setTimeout`, canal entre pestañas, `pageshow`, y en `cerrarSesion`, caché vacía antes de `location.replace`;
  - `AvisoDeSesion` (`alertdialog`, cuenta atrás, "Seguir conectado" solo por inactividad);
  - `AreaProtegida`, `PantallaDeInicio` (botón "Iniciar sesión" con `regreso` y avisos), `SinPermiso` y `OtraSesionAbierta`;
  - `avisos.ts`;
  - `rutas.tsx` con las rutas de 004a;
  - un *error boundary* por área.

  Repo: core-bancario-web · Capa: sesion · Archivos: `src/sesion/{ProveedorDeSesion,AvisoDeSesion,AreaProtegida,PantallaDeInicio,SinPermiso,OtraSesionAbierta}.tsx`, `src/sesion/avisos.ts`, `src/app/{rutas,proveedores}.tsx`, `src/main.tsx` · Cubre: RF-14, RN-19, RN-22, RN-25, RN-26, RNF-06, RNF-07, RNF-08, RNF-09, CL-01, CL-04, CL-05, CL-06, CL-07, CL-10, CL-11, CL-12, CL-13, CA-01, CA-02, CA-04, CA-07, CA-08, CA-09, CA-11, CA-12, CA-14, CA-15, CA-16, CA-35, CA-36

- [x] T17 — Área del cliente e inicio del backoffice:
  - `MisCuentas` (número, moneda, estado y saldo; carga, vacío y error con "Reintentar"; las Cerradas incluidas);
  - `DetalleDeCuenta` (con fechas; un 404 → "Cuenta no encontrada", igual para ajena e inexistente);
  - `BotonCambiarClave` (navegación a `/bff/cambiar-clave`; el aviso `clave-actualizada` se muestra y se limpia);
  - `InicioDelBackoffice` (operador y "Cerrar sesión"; la búsqueda llega en 004b);
  - `cliente/consultas.ts`.

  Área del cliente usable desde 360 px · Repo: core-bancario-web · Capa: pantallas · Archivos: `src/cliente/{MisCuentas,DetalleDeCuenta,BotonCambiarClave}.tsx`, `src/cliente/consultas.ts`, `src/backoffice/InicioDelBackoffice.tsx`, `src/app/rutas.tsx` · Cubre: RF-05, RF-15, RNF-08, RNF-09, RNF-11, CL-14, CL-24, CA-01, CA-02, CA-18, CA-19, CA-20, CA-21, CA-35, CA-36

### Operación y cierre

- [x] T18 — Imagen web con CSP:
  - `Dockerfile` multietapa (`node:24` → `nginxinc/nginx-unprivileged`, puerto 8080);
  - `infra/nginx/default.conf`: `try_files` a `index.html`; `index.html` con `Cache-Control: no-store`; *proxy* de `/bff/` y `/api/` a `http://bff:8080` con `proxy_set_header Host $http_host` y `X-Forwarded-Proto`/`For`; las cabeceras de RNF-03 (CSP, `nosniff`, `Referrer-Policy`); `log_format` con `$uri` en vez de `$request` (RNF-04).

  Repo: core-bancario-web · Capa: operación · Archivos: `Dockerfile`, `infra/nginx/default.conf`, `.dockerignore` · Cubre: RNF-03, RNF-04, RNF-16, CL-11

- [x] T19 — CI de los recorridos:
  - en `.github/workflows/ci.yml`, job `e2e-simulado` (Chromium) y job `e2e-real`:
    - checkout de `core-bancario` en `../core-bancario` (`ref: ${{ vars.CORE_BANCARIO_REF || 'main' }}`);
    - `.env` con `KC_BOOTSTRAP_ADMIN_*`, `KC_CLIENTES_API_SECRET` y `KC_WEB_*` aleatorios y enmascarados;
    - `docker compose -f ../core-bancario/docker-compose.yml --profile app --profile web up -d --build --wait` con `CORE_BANCARIO_WEB_DIR=${{ github.workspace }}`;
    - espera de los dos realms;
    - `npm run e2e:real`;
    - si falla, artefactos de *trace* y `docker compose logs`;
  - un paso que compruebe `contratos/` frente a `../core-bancario` y `src/api/generado/` frente a `npm run tipos` (`git diff --exit-code`).

  Repo: core-bancario-web · Capa: operación · Archivos: `.github/workflows/ci.yml`, `playwright.config.ts` (solo la parte de CI) · Cubre: RNF-10, RNF-13, RNF-15, RNF-16, CA-01, CA-03, CA-05, CA-06, CA-17 · Pruebas en verde: los recorridos reales de 004a

- [x] T20 — Documentación y verificación del PR:
  - README del web: cómo levantar todo (comando único), `npm run dev` con el BFF en 5180, cómo sincronizar contratos, navegadores soportados (RNF-10), por qué no hay CSP en `dev` y enlace a la spec de este repositorio;
  - verificación: `lint`, `typecheck`, `test`, `e2e:simulado` y `e2e:real` en verde, CI en verde y `/sdd-review`;
  - comprobación manual con las herramientas del navegador: ninguna cookie legible, ni almacenamiento ni respuesta con un token.

  Repo: core-bancario-web · Capa: transversal · Archivos: `README.md` · Cubre: RNF-10, RNF-14, D-09

---

# Bloque 004b — Backoffice y RF-16

**Cubre:** CA-22…CA-33; CA-34…CA-36 sobre las pantallas nuevas. RF-01, RF-02, RF-03, RF-12, RF-13, RF-16; RN-14, RN-20; CL-15…CL-24; RNF-04 (búsqueda y clave temporal), RNF-15 (recorrido del operador).

## PR 3 de 004b — repo `core-bancario` (desde `main` con 004a mergeado)

**Antes de T21 (test-writer, commit `test(004): pruebas en rojo del bloque 004b`):**

- Pruebas de Application, Infrastructure, Api y Keycloak.Tests de RF-16 (plan, sección 7), contra esqueletos de la sección 3.3 del plan.
- La fila `ObtenerEstadoDeAccesoDigital` en `F002_RNF05_ContratoOpenApi_…`.

### Application

- [x] T21 — Estado del acceso:
  - `UsuarioDeIdentidad` gana `bool PrimerIngresoPendiente = false`;
  - `ObtenerEstadoDeAccesoConsulta`, `EstadoDeAccesoDto` y `ObtenerEstadoDeAccesoHandler` (404 sin llamar al proveedor; `LocalizadorDeAcceso`; `PrimerIngresoPendiente` solo si Habilitado; sin escribir ni reconciliar);
  - `AddAplicacion` lo registra como Scoped.

  Repo: core-bancario · Capa: Application · Archivos: `src/CoreBancario.Application/AccesosDigitales/{UsuarioDeIdentidad,ObtenerEstadoDeAccesoConsulta,EstadoDeAccesoDto,ObtenerEstadoDeAccesoHandler}.cs`, `src/CoreBancario.Application/DependencyInjection.cs` · Cubre: RF-16, CL-20, CA-29

### Infrastructure

- [x] T22 — `ProveedorDeIdentidadKeycloak.ComoUsuario` lee `requiredActions`: `UPDATE_PASSWORD` o `CONFIGURE_TOTP` → `PrimerIngresoPendiente = true`; ausente o vacío → `false` · Repo: core-bancario · Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Identidad/ProveedorDeIdentidadKeycloak.cs` · Cubre: RF-16, CA-29

### Api

- [x] T23 — Endpoint de RF-16 y cierre del PR:
  - `EndpointsDeAccesoDigital`: el filtro `RegistroDeOperacionDelOperador` pasa a cada `POST`. Nuevo `GET` del grupo (`ObtenerEstadoDeAccesoDigital`, `ProducesProblem` 404, 502 y 503);
  - regenerar y versionar el contrato;
  - en el README, el endpoint nuevo en la sección de Keycloak;
  - verificación: `dotnet format --verify-no-changes`, build con `-warnaserror`, `dotnet test` con Docker, CI y `/sdd-review`.

  Repo: core-bancario · Capa: Api · Archivos: `src/CoreBancario.Api/AccesosDigitales/EndpointsDeAccesoDigital.cs`, `src/CoreBancario.Api/openapi/core-bancario-api.json`, `README.md` · Cubre: RF-16, RN-19, RNF-14, CL-20, CA-29, CA-32 · Pruebas en verde: todas las de RF-16 y CL-20

## PR 4 de 004b — repo `core-bancario-web` (después del PR 3)

**Antes de T24 (test-writer, commit `test(004): pruebas en rojo del bloque 004b`):**

- Pruebas Vitest de CA-22…CA-32, `acciones` y `esquemaDeRegistro`, con manejadores MSW de `/api/clientes/**` y `/api/cuentas/**`.
- Simuladas de CA-30 y CA-35/CA-36 de las pantallas nuevas.
- Real `F004_CA33_…`.

- [ ] T24 — `npm run contratos:sincronizar` y `npm run tipos` con el contrato de 004b (`EstadoDeAccesoDto`, `ObtenerEstadoDeAccesoDigital`) · Repo: core-bancario-web · Capa: contratos · Archivos: `contratos/core-bancario-api.json`, `src/api/generado/api.d.ts` · Cubre: RNF-13, RF-16

- [ ] T25 — Buscar y registrar:
  - `BuscarCliente` en `/backoffice`, que sustituye a `InicioDelBackoffice`: tipo y número, con o sin puntos, y el número solo en el estado del componente. Con éxito → `/backoffice/clientes/{id}`. Con 404 → CL-22 con "Registrarlo";
  - `FormularioDeRegistro` con `esquemaDeRegistro` (RHF + Zod): un 400 `errors` → `setError` en cada campo; 409 `documento-duplicado` → "Ir a la ficha"; con éxito → ficha nueva;
  - `backoffice/consultas.ts`.

  Uso desde 1280 px · Repo: core-bancario-web · Capa: pantallas · Archivos: `src/backoffice/{BuscarCliente,FormularioDeRegistro}.tsx`, `src/backoffice/{esquemaDeRegistro,consultas}.ts`, `src/app/rutas.tsx` · Cubre: RF-01, RF-12, RNF-04, RNF-08, RNF-09, CL-22, CL-23, CL-24, CA-22, CA-23, CA-33, CA-35, CA-36

- [ ] T26 — Ficha y cuentas:
  - `accionesDeCuenta` (RN-14);
  - `FichaDeCliente`: datos, cuentas con `formatearDinero` y estado del acceso en su propia consulta (T27);
  - `AccionesDeCuenta`: solo las permitidas. "Cerrar" pide confirmación; 409 → mensaje + `invalidateQueries`; 422 → mensaje de negocio;
  - `AbrirCuenta`: COP o USD, con `Boton enCurso` y `retry: 0`.

  Repo: core-bancario-web · Capa: pantallas · Archivos: `src/backoffice/{FichaDeCliente,AccionesDeCuenta,AbrirCuenta}.tsx`, `src/backoffice/acciones.ts` · Cubre: RF-02, RF-03, RN-14, RNF-08, RNF-09, CL-15, CL-16, CL-18, CL-24, CA-24, CA-25, CA-26, CA-27, CA-33, CA-35, CA-36

- [ ] T27 — Acceso digital:
  - `accionesDeAcceso`;
  - `AccesoDigital`: estado, "primer ingreso pendiente", y 502/503 → "Estado del acceso no disponible" sin acciones;
  - confirmaciones: "Habilitar" avisa de qué pasa si ya tuviera acceso; "Restablecer" explica clave nueva, segundo factor borrado y sesiones cerradas; "Deshabilitar";
  - `DialogoDeClaveTemporal`: no se cierra por accidente, título con `accion` de la respuesta, "Copiar" y "Ya la entregué". Al cerrarlo, `mutation.reset()`, con `gcTime: 0`;
  - 502/503 al habilitar → mensaje de reintentar sin riesgo;
  - después de cada acción, invalidar `['acceso', id]`.

  Repo: core-bancario-web · Capa: pantallas · Archivos: `src/backoffice/{AccesoDigital,DialogoDeClaveTemporal}.tsx`, `src/backoffice/acciones.ts` · Cubre: RF-13, RF-16, RN-20, RNF-04, RNF-08, CL-17, CL-19, CL-20, CL-21, CL-24, CA-28, CA-30, CA-31, CA-32, CA-35, CA-36

- [ ] T28 — Cierre del bloque:
  - el recorrido real del operador (CA-33) en el job `e2e-real`;
  - README del web: pantallas del backoffice y el límite conocido de CL-15;
  - verificación: `lint`, `typecheck`, `test`, `e2e:simulado` y `e2e:real` en verde, CI y `/sdd-review`;
  - comprobación manual: la clave temporal no queda en el DOM, en la caché ni en el almacenamiento tras "Ya la entregué", y el número de documento no aparece en la URL ni en los logs de nginx, del BFF ni de la API.

  Repo: core-bancario-web · Capa: transversal · Archivos: `README.md`, `.github/workflows/ci.yml` (si hace falta) · Cubre: RNF-04, RNF-14, RNF-15, CA-33
