# Plan — 004 Front base: inicio de sesión, sesión segura y pantallas del operador

Estado: Aprobado (2026-10-26)
Spec: [spec.md](spec.md)

## 1. Resumen del diseño

**El navegador nunca tiene un token** (RNF-01). Un BFF nuevo en este repositorio, `src/CoreBancario.Bff`, vive en el **mismo origen** que la SPA (`http://localhost:5173`, detrás de Vite o de nginx). Hace el inicio de sesión con Keycloak como cliente **confidencial**, guarda los tokens en un almacén del servidor y entrega al navegador una cookie `HttpOnly` y `SameSite=Strict` que solo lleva una clave opaca. La SPA pide datos a `/api/*`, y el BFF los reenvía a la API con YARP añadiendo `Authorization: Bearer`. La API no cambia su forma de autenticar (ADR-0015), y deja de exponerse al navegador (ADR-0020).

**CSRF:** todo `/api/*` y `/bff/*` exige el encabezado `X-CSRF: 1` y un `Sec-Fetch-Site` igual a `same-origin`. Ninguna operación que cambia datos responde a `GET`.

**La inactividad la mide el BFF**, no Keycloak. Solo `POST /bff/actividad` mueve la última actividad. Ese latido lo envía la SPA ante un clic, una tecla o un cambio de pantalla, como mucho uno cada 15 s, y además renueva los tokens para alinear el reloj de Keycloak. Las peticiones a `/api/*` **nunca** cuentan. Todo inicio de sesión lleva `prompt=login` (ADR-0021).

**El frontend** es Vite + React 19 + TypeScript estricto, con TanStack Query, React Router, React Hook Form + Zod, openapi-typescript + openapi-fetch y React Aria Components (ADR-0022). Los montos se formatean con `Intl.NumberFormat` pasando **el texto** del monto.

**RF-16** es un `GET /clientes/{id}/acceso-digital` nuevo en la API. Reutiliza `LocalizadorDeAcceso` (ADR-0017): Keycloak es la fuente del estado.

### 1.1 Dos repositorios, dos bloques y cuatro PR

La spec y este plan viven aquí, y cada tarea dice en qué repositorio va. **Un cambio de contrato va primero en `core-bancario`** (ADR-0005), así que en cada bloque el PR del backend se mergea antes que el del web.

| Bloque | PR en `core-bancario` | PR en `core-bancario-web` | CA |
|---|---|---|---|
| **004a — Sesión y área del cliente** | Realms con cliente confidencial, BFF completo, códigos de error en el OpenAPI, `Metadatos` del emisor, compose con perfiles `app` y `web`, Dockerfiles | Contratos y tipos, sesión (reloj, latido, pestañas, aviso), inicio y pantallas de sesión de las dos áreas, "Mis cuentas", detalle, "Cambiar clave", inicio del backoffice (sin búsqueda), nginx con CSP, recorrido real del cliente | CA-01…CA-21, CA-34…CA-36 (CA-35 y CA-36 sobre las pantallas de 004a) |
| **004b — Backoffice y RF-16** | `GET …/acceso-digital` con "primer ingreso pendiente" | Búsqueda, registro, ficha, acciones de cuenta, acceso digital y diálogo de la clave, recorrido real del operador | CA-22…CA-33, más CA-34…CA-36 ampliadas a las pantallas nuevas |

**004a se puede mergear sola:**

- no usa nada de 004b;
- el inicio del backoffice (CA-02) es una pantalla con el operador y "Cerrar sesión", y la búsqueda llega en 004b;
- los datos de los recorridos reales de 004a (cliente, cuenta y acceso habilitado) los crea el *setup* llamando a la API **a través del BFF** con una sesión real de operador, sin pantallas del backoffice.

**Ramas** (recomendación, igual que en la 003):

- `core-bancario`: `feature/004a-sesion-bff` nace de la actual `feature/004-front-base` (renombrarla), con la spec, el plan y las ADRs; `feature/004b-estado-de-acceso` nace de `main` tras mergear 004a.
- `core-bancario-web`: `feature/004a-sesion-y-cliente` y `feature/004b-backoffice`, cada una después del PR de backend de su bloque.

Commits `tipo(004): …` en los dos repositorios.

### 1.2 Qué necesita la 004 de `chore/kit-web` (no es parte de la 004)

La tarea `chore/kit-web` crea `core-bancario-web` antes de `/sdd-build`. La 004 da por hecho que deja:

- **Estructura:** `src/` (código), `e2e/` (Playwright), `contratos/` (copias de los OpenAPI), `src/api/generado/` (tipos generados, versionados), `infra/nginx/` y `Dockerfile`. `.nvmrc` con Node 24 y `package-lock.json`.
- **Comandos (`package.json`):**
  - `dev` (Vite en 5173, con *proxy* de `/bff` y `/api` a `http://localhost:5180`, sin `changeOrigin`, para que el BFF vea `Host: localhost:5173`);
  - `build`, `preview`, `typecheck` (`tsc --noEmit`), `lint`, `format:check`;
  - `test` (Vitest);
  - `e2e:simulado` y `e2e:real` (Playwright, dos proyectos);
  - `contratos:sincronizar` y `tipos` (el contenido lo da la tarea T12).
- **Kit SDD para TypeScript:** los agentes, `/sdd-*` apuntando a la spec de este repositorio, y la skill `ts-clean-code`. Convención de nombres de prueba `F004_ID_Escenario_Resultado` en el título de cada `test(...)`, igual que en .NET.
- **CI (`.github/workflows/ci.yml`) en cada PR:**
  - `install` (`npm ci`), `lint`, `format:check`, `typecheck`, `test` y `build`;
  - `e2e:simulado` (Chromium, `npx playwright install --with-deps chromium`);
  - **un job `e2e-real`**: clona `core-bancario` (`main`) en `../core-bancario`, genera el `.env` con valores aleatorios ocultos en el log, levanta `docker compose --profile app --profile web up -d --build --wait` y ejecuta `e2e:real`. Si falla, sube el *trace* de Playwright y los logs de los contenedores;
  - escaneo de secretos (gitleaks).

  Los pasos concretos de la 004 sobre este CI están en T19.

## 2. Impacto por capa

| Capa / repo | Qué cambia | Archivos |
|---|---|---|
| Domain | Nada | — |
| Application (004b) | `UsuarioDeIdentidad` gana `PrimerIngresoPendiente` (opcional, no rompe las llamadas existentes). Consulta, DTO y handler del estado del acceso | `src/CoreBancario.Application/AccesosDigitales/{UsuarioDeIdentidad,ObtenerEstadoDeAccesoConsulta,EstadoDeAccesoDto,ObtenerEstadoDeAccesoHandler}.cs`, `DependencyInjection.cs` |
| Infrastructure (004b) | El adaptador de Keycloak lee `requiredActions` | `src/CoreBancario.Infrastructure/Identidad/ProveedorDeIdentidadKeycloak.cs` |
| Configuración de Keycloak (004a) | `core-bancario-web` confidencial en los dos realms, con `redirectUris` exactas al BFF. En compose: `KC_HOSTNAME` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` | `infra/keycloak/realms/*.json`, `docker-compose.yml`, `.env.example` |
| Api | 004a: `Metadatos` opcional por emisor (compose) y el *enum* `CodigoDeError` en el OpenAPI. 004b: endpoint de RF-16 | `src/CoreBancario.Api/Autenticacion/{OpcionesDeAutenticacion,ConfiguracionDeAutenticacion}.cs`, `src/CoreBancario.Api/OpenApi/CodigosDeErrorEnOpenApi.cs`, `Program.cs`, `src/CoreBancario.Api/AccesosDigitales/EndpointsDeAccesoDigital.cs`, `openapi/core-bancario-api.json`, `Dockerfile` |
| **Bff (nuevo, 004a)** | Host ASP.NET Core: OIDC con dos realms, cookie con almacén en memoria, reloj de sesión, CSRF, *proxy* YARP, endpoints `/bff/*` y su OpenAPI versionado | `src/CoreBancario.Bff/**` |
| Tests (.NET) | Proyecto **nuevo** `CoreBancario.Bff.Tests`. Keycloak.Tests levanta además el BFF, y su navegador simulado envía el secreto del cliente. Se cambian las pruebas de configuración del cliente web | `tests/CoreBancario.Bff.Tests/**`, `tests/CoreBancario.Keycloak.Tests/**`, `tests/CoreBancario.Api.Tests/ConfiguracionDeRealmsTests.cs` |
| Operación | Perfiles `app` (API y BFF) y `web` (nginx con la SPA) en el compose de este repo | `docker-compose.yml`, `src/CoreBancario.{Api,Bff}/Dockerfile`, `.github/workflows/ci.yml` |
| **Web (repo `core-bancario-web`)** | Toda la SPA de la sección 3.4 | `src/**`, `e2e/**`, `contratos/**`, `infra/nginx/**`, `Dockerfile` |

**Por qué un proyecto de tests más.** `CoreBancario.Bff` es un host nuevo, no una capa de la API. CLAUDE.md pide un proyecto de tests por proyecto de `src`, así que `CoreBancario.Bff.Tests` sigue la regla. Lo que necesita Keycloak real va a `CoreBancario.Keycloak.Tests`, como ya establece ADR-0018.

**Por qué el BFF no referencia Application ni Domain.** No tiene reglas de negocio: autentica, mide la sesión y reenvía. Si referenciara Application, tendría la tentación de saltarse la API (KISS y una frontera clara).

## 3. Contratos

### 3.1 Endpoints

#### API (`core-bancario`, contrato `src/CoreBancario.Api/openapi/core-bancario-api.json`)

| Método | Ruta | Rol | Request | Respuestas | Cubre |
|---|---|---|---|---|---|
| GET | `/clientes/{clienteId:guid}/acceso-digital` (**004b**) | Operador | — | 200 `EstadoDeAccesoDto` · 401 · 403 · 404 · 500 · 502 `identidad-error` · 503 `identidad-sin-respuesta` | RF-16, CA-28, CA-29, CL-20 |
| (el resto) | sin cambios | | | | |

- `operationId`: `ObtenerEstadoDeAccesoDigital`.
- **Sin** `RegistroDeOperacionDelOperador`: es una consulta y no cambia nada. El grupo de `EndpointsDeAccesoDigital` se reorganiza para que el filtro quede solo en los dos `POST`.
- **No reconcilia solicitudes pendientes:** una consulta no escribe en la base. El estado sale de Keycloak, que es la única fuente (ADR-0017). Así, el botón dice lo mismo que haría `HabilitarAccesoHandler`: con un usuario huérfano sin vínculo dice "Restablecer", porque el localizador lo adopta.
- **Por qué un endpoint aparte y no un campo de la ficha.** `ObtenerCliente` no depende de Keycloak. Si el estado viniera dentro de la ficha, una caída de Keycloak dejaría sin ficha al operador, y CL-20 pide justo lo contrario.

```json
// EstadoDeAccesoDto
{ "clienteId": "0199…", "estado": "Habilitado", "primerIngresoPendiente": true }
// estado ∈ "SinAcceso" | "Habilitado" | "Deshabilitado"; primerIngresoPendiente es null salvo si estado = "Habilitado"
```

**"Primer ingreso pendiente" sí se puede obtener:** el usuario de Keycloak conserva `UPDATE_PASSWORD` o `CONFIGURE_TOTP` en `requiredActions` hasta completarlas (las pone `CrearAsync` y `ReiniciarAccesoAsync`).

**Contrato, en 004a:** el documento gana `components.schemas.CodigoDeError`, un *enum* de texto con **todas** las constantes de `CodigosDeError`. Es la fuente del tipo `CodigoDeError` de la SPA (RNF-12, CA-34).

**Regla nueva (RNF-02, decisión del autor):** ninguna operación de la API que cambia datos es `GET`. Hoy ya se cumple, y una prueba sobre el contrato lo mantiene.

#### BFF (`core-bancario`, contrato `src/CoreBancario.Bff/openapi/core-bancario-bff.json`)

| Método | Ruta | Anti-CSRF | Respuestas | Cubre |
|---|---|---|---|---|
| GET | `/bff/iniciar-sesion/{area}?regreso=` | No (navegación) | 302 a Keycloak (`prompt=login`); 302 a `RutaDeRegreso` si ya hay sesión de esa área; 302 a `{prefijo}?aviso=otra-sesion-abierta` si hay sesión de la otra área; 302 a `{prefijo}?aviso=identidad-no-disponible` si Keycloak no responde; 404 si `area` no es `clientes` ni `empleados` | RF-14, CL-03, CL-05, CL-08, CL-10 |
| GET | `/bff/signin-oidc/clientes`, `/bff/signin-oidc/empleados` | No (regreso) | Los atiende el manejador OIDC: 302 a la ruta de regreso o a `{prefijo}?aviso=inicio-fallido` | RNF-05, CL-01, CL-02 |
| GET | `/bff/cambiar-clave` | No (navegación) | Con sesión de clientes: 302 a Keycloak con `kc_action=UPDATE_PASSWORD`; al volver, 302 a `/mis-cuentas?aviso=clave-actualizada` o `?aviso=clave-sin-cambios`. Sin esa sesión: 302 a `/` | RF-15, CA-21 |
| GET | `/bff/sesion` | Sí | 200 `SesionDto` (**no** cuenta como actividad) · 401 `ProblemDetails` del BFF | RF-14, RN-25, CL-04 |
| POST | `/bff/actividad` | Sí | 200 `SesionDto` · 401 | RN-25, CL-06, CL-12 |
| POST | `/bff/salir` | Sí | 204 (idempotente) | RF-14, RNF-07 |
| * | `/api/{**resto}` | Sí | Lo que responda la API, con `Cache-Control: no-store` · 401 del BFF sin llamar a la API si no hay sesión o venció · 503 `identidad-sin-respuesta` si hace falta renovar y Keycloak no responde | RNF-01, RNF-02, CL-04, CL-12 |

- `operationId`: `IniciarSesion`, `CambiarClave`, `ObtenerSesion`, `RegistrarActividad`, `CerrarSesion`. El *proxy* y los regresos no aparecen en el documento.
- `GET` a `/bff/actividad` o a `/bff/salir` → **405**, y la sesión sigue igual (regla del autor).
- `components.schemas.CodigoDeErrorDelBff`, *enum* de: `sin-sesion`, `sesion-vencida-por-inactividad`, `sesion-vencida-por-vida-maxima`, `sesion-revocada`, `peticion-no-confiable`, `identidad-sin-respuesta`.
- **Avisos** (parámetro `aviso` de las redirecciones del BFF hacia la SPA; no son secretos): `inicio-fallido`, `identidad-no-disponible`, `otra-sesion-abierta`, `clave-actualizada`, `clave-sin-cambios`.

```json
// SesionDto
{ "area": "clientes", "usuario": "cc1234567", "rol": "Cliente",
  "segundosHastaInactividad": 300, "segundosHastaVidaMaxima": 1794 }
// area ∈ "clientes" | "empleados"; rol ∈ "Cliente" | "Operador" | null (credencial sin el rol del área, CL-04)

// 401 del BFF (mismo formato que ADR-0011)
{ "title": "Sesión terminada", "status": 401, "codigo": "sesion-vencida-por-inactividad", "traceId": "00-…" }
// 403 anti-CSRF
{ "title": "Petición no confiable", "status": 403, "codigo": "peticion-no-confiable", "traceId": "00-…" }
```

**De dónde sale `rol`.** El BFF lee `realm_access.roles` del access token que acaba de recibir de Keycloak, sin volver a validarlo, porque lo obtuvo él mismo por el canal trasero. Aplica la misma regla estructural de ADR-0015: área `clientes` + `cliente` → `Cliente`; área `empleados` + `operador` → `Operador`; si no, `null`. **Es solo para la interfaz** (CL-04 sin llamar a la API). La autoridad sigue siendo la API, que responde 403, y la SPA trata un 403 igual.

### 3.2 Comandos, consultas y eventos

Sin eventos (YAGNI).

| Tipo | Nombre | Datos | Productor → consumidor | Cubre |
|---|---|---|---|---|
| Consulta (API, 004b) | `ObtenerEstadoDeAccesoConsulta` | `Guid ClienteId` | Endpoint → `ObtenerEstadoDeAccesoHandler` | RF-16 |
| Mensaje entre pestañas (SPA) | `MensajeDeSesion` `plazos` | `SesionDto`, `recibidoEn` | Pestaña que consultó o latió → las demás (`BroadcastChannel`) | RN-25, RNF-07, CA-11 |
| Mensaje entre pestañas (SPA) | `MensajeDeSesion` `cerrada` | `motivo` | Pestaña que cerró → las demás | RNF-07, CA-11 |

### 3.3 Firmas públicas (.NET)

Las pruebas comprueban tipos, estados HTTP, `codigo` y avisos, no textos. Los mensajes van en español.

```
src/CoreBancario.Bff/                      namespace CoreBancario.Bff (y subespacios por carpeta)
  CoreBancario.Bff.csproj, Program.cs, MarcadorDelBff.cs, appsettings.json, appsettings.Development.json, Dockerfile
  openapi/core-bancario-bff.json
  Configuracion/  AreaDeSesion.cs, AreasDeSesion.cs, OpcionesDelBff.cs
  Sesiones/       TokensDeLaSesion.cs, RolDeSesion.cs, SesionDelNavegador.cs, EstadoDeSesion.cs, PlazosDeSesion.cs,
                  PoliticaDeSesion.cs, LectorDeRol.cs, AlmacenDeSesiones.cs, MotivoDeCierre.cs,
                  SesionTerminadaException.cs, SesionDto.cs, ServicioDeSesion.cs
  Identidad/      IClienteDeIdentidad.cs, TokensRenovados.cs, RenovacionRechazadaException.cs,
                  IdentidadSinRespuestaException.cs, ClienteDeIdentidadKeycloak.cs
  InicioDeSesion/ RutaDeRegreso.cs, Avisos.cs, ConfiguracionDeSesion.cs, EventosDeOidc.cs, PropiedadesDeSesion.cs
  Seguridad/      ProteccionCsrf.cs, EncabezadosDeSeguridad.cs
  Errores/        CodigosDelBff.cs, RespuestasDelBff.cs
  Endpoints/      EndpointsDelBff.cs
  Proxy/          ProxyDeLaApi.cs
  OpenApi/        CodigosDelBffEnOpenApi.cs
```

#### Bff: configuración

```csharp
// ───────────── Configuracion/AreaDeSesion.cs ─────────────
namespace CoreBancario.Bff.Configuracion;

/// <summary>El área de la app y su realm (ADR-0014). Una sesión pertenece a una sola área (RN-26).</summary>
public enum AreaDeSesion { Clientes, Empleados }

// ───────────── AreasDeSesion.cs ─────────────
public static class AreasDeSesion
{
    /// <summary>"clientes" / "empleados": segmento de ruta del BFF y valor de SesionDto.Area.</summary>
    public static string Segmento(AreaDeSesion area);
    /// <summary>Sin distinguir mayúsculas. false con null, vacío u otro texto.</summary>
    public static bool TryParse(string? segmento, out AreaDeSesion area);
    /// <summary>"/" (Clientes) o "/backoffice" (Empleados): donde la SPA muestra el inicio y los avisos.</summary>
    public static string PrefijoDeLaSpa(AreaDeSesion area);
    /// <summary>"/mis-cuentas" o "/backoffice": pantalla inicial con sesión.</summary>
    public static string InicioDeLaSpa(AreaDeSesion area);
    /// <summary>"OidcClientes" / "OidcEmpleados".</summary>
    public static string EsquemaOidc(AreaDeSesion area);
    /// <summary>"/bff/signin-oidc/clientes" / "/bff/signin-oidc/empleados".</summary>
    public static string RutaDeRegresoOidc(AreaDeSesion area);
}

// ───────────── OpcionesDelBff.cs ─────────────
/// <summary>Sección "Bff". Los secretos van en user-secrets o en variables de entorno, nunca en appsettings.</summary>
public sealed class OpcionesDelBff
{
    public const string Seccion = "Bff";
    public OpcionesDeLaApi Api { get; set; } = new();
    public OpcionesDeArea Clientes { get; set; } = new();
    public OpcionesDeArea Empleados { get; set; } = new();
    public OpcionesDeArea De(AreaDeSesion area);
}

public sealed class OpcionesDeLaApi
{
    /// <summary>Destino del proxy. Local: http://localhost:5096; compose: http://api:8080.</summary>
    public Uri Url { get; set; } = new("http://localhost:5096");
}

public sealed class OpcionesDeArea
{
    /// <summary>"iss" del realm, igual que en la API: http://localhost:8080/realms/clientes.</summary>
    public string Emisor { get; set; } = "";
    /// <summary>Documento de descubrimiento por el canal trasero (compose: http://keycloak:8080/realms/…/.well-known/openid-configuration). null → Emisor.</summary>
    public string? Metadatos { get; set; }
    public string ClientId { get; set; } = "core-bancario-web";
    public string ClientSecret { get; set; } = "";
    /// <summary>RN-22. appsettings.json: Clientes 00:05:00, Empleados 00:15:00.</summary>
    public TimeSpan Inactividad { get; set; }
    /// <summary>RN-22. appsettings.json: Clientes 00:30:00, Empleados 09:00:00.</summary>
    public TimeSpan VidaMaxima { get; set; }
}

// ───────────── MarcadorDelBff.cs ─────────────
namespace CoreBancario.Bff;
/// <summary>
/// Tipo del ensamblado para WebApplicationFactory&lt;MarcadorDelBff&gt;. El Program de primer nivel del BFF queda
/// internal, así que no choca con el Program público de la API cuando una prueba referencia los dos proyectos.
/// </summary>
public sealed class MarcadorDelBff { private MarcadorDelBff() { } }
```

#### Bff: sesión

```csharp
namespace CoreBancario.Bff.Sesiones;

/// <summary>Tokens que el BFF guarda y NUNCA salen del servidor (RNF-01). ToString no los muestra.</summary>
public sealed record TokensDeLaSesion(string AccessToken, string RefreshToken, string IdToken, DateTimeOffset AccessTokenVence)
{
    public override string ToString();   // "TokensDeLaSesion { [ocultos] }"
}

public enum RolDeSesion { Cliente, Operador }

/// <summary>
/// Sesión de un navegador (ADR-0020, ADR-0021). Inicio = auth_time del token de identidad. UltimaActividad solo la
/// cambian el regreso del login y el latido (RN-25). Mutable solo con sus dos métodos.
/// </summary>
public sealed class SesionDelNavegador
{
    /// <exception cref="ArgumentException">clave, sub, usuario o sid vacíos, o ultimaActividad &lt; inicio.</exception>
    public SesionDelNavegador(string clave, AreaDeSesion area, string sub, string usuario, string sid, RolDeSesion? rol,
        DateTimeOffset inicio, DateTimeOffset ultimaActividad, TokensDeLaSesion tokens, ClaimsPrincipal principal);

    public string Clave { get; }
    public AreaDeSesion Area { get; }
    public string Sub { get; }
    /// <summary>preferred_username, como "cc1234567".</summary>
    public string Usuario { get; }
    /// <summary>Id de la sesión de Keycloak (claim "sid").</summary>
    public string Sid { get; }
    public RolDeSesion? Rol { get; }
    public DateTimeOffset Inicio { get; }
    public DateTimeOffset UltimaActividad { get; private set; }
    public TokensDeLaSesion Tokens { get; private set; }
    public ClaimsPrincipal Principal { get; }

    /// <summary>Nunca retrocede: si ahora &lt; UltimaActividad, no cambia.</summary>
    public void RegistrarActividad(DateTimeOffset ahora);
    public void ReemplazarTokens(TokensDeLaSesion tokens);
}

public enum EstadoDeSesion { Vigente, VencidaPorInactividad, VencidaPorVidaMaxima }

/// <summary>Duraciones (nunca negativas) hasta cada cierre; la SPA las convierte con su hora local (ADR-0021).</summary>
public sealed record PlazosDeSesion(TimeSpan HastaInactividad, TimeSpan HastaVidaMaxima);

/// <summary>Regla pura de RN-22 con RN-25. "≥": en el instante exacto del límite, ya venció. Si vencen las dos, gana VidaMaxima.</summary>
public static class PoliticaDeSesion
{
    public static EstadoDeSesion Evaluar(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora);
    public static PlazosDeSesion Plazos(SesionDelNavegador sesion, OpcionesDeArea opciones, DateTimeOffset ahora);
}

/// <summary>Rol de la interfaz a partir del access token (sección 3.1, "De dónde sale rol"). Lee el JWT sin validarlo.</summary>
public static class LectorDeRol
{
    /// <returns>Cliente si área Clientes y realm_access.roles contiene "cliente"; Operador si Empleados y "operador"; null si no o si el token no se puede leer.</returns>
    public static RolDeSesion? Desde(AreaDeSesion area, string accessToken);
}

/// <summary>
/// Almacén EN MEMORIA de las sesiones (ADR-0020). Implementa ITicketStore para el manejador de cookies: la cookie solo
/// lleva la clave. Cada entrada se libera sola al cumplirse Inicio + VidaMaxima. Singleton.
/// </summary>
public sealed class AlmacenDeSesiones(TimeProvider reloj, IOptions<OpcionesDelBff> opciones) : ITicketStore
{
    /// <summary>Clave opaca: 32 bytes aleatorios en Base64Url.</summary>
    public static string NuevaClave();

    // ── ITicketStore (lo usa el manejador de cookies) ──
    /// <summary>
    /// Construye la SesionDelNavegador desde el ticket del regreso OIDC (tokens de SaveTokens; área, inicio y última
    /// actividad de PropiedadesDeSesion; sub, preferred_username, sid y auth_time de los claims; rol con LectorDeRol).
    /// Los tokens NO quedan en el ticket que guarda.
    /// </summary>
    public Task<string> StoreAsync(AuthenticationTicket ticket);
    public Task RenewAsync(string key, AuthenticationTicket ticket);
    /// <summary>Ticket sin tokens, con PropiedadesDeSesion.Clave = key en sus Items. null si no existe.</summary>
    public Task<AuthenticationTicket?> RetrieveAsync(string key);
    public Task RemoveAsync(string key);

    // ── Para el BFF ──
    public void Agregar(SesionDelNavegador sesion);
    public SesionDelNavegador? Obtener(string clave);
    public void Eliminar(string clave);
    /// <summary>Serializa las operaciones sobre UNA sesión (renovaciones con rotación, maxReuse 0).</summary>
    public Task<T> ConCandadoAsync<T>(string clave, Func<Task<T>> operacion, CancellationToken cancellationToken);
}

public enum MotivoDeCierre { SinSesion, Inactividad, VidaMaxima, Revocada }

/// <summary>La sesión no existe o terminó. Se traduce a 401 con el código de CodigosDelBff.De(motivo).</summary>
public sealed class SesionTerminadaException(MotivoDeCierre motivo) : Exception
{
    public MotivoDeCierre Motivo { get; }
}

/// <summary>Respuesta de /bff/sesion y /bff/actividad. Sin tokens (RNF-01). Segundos truncados hacia abajo.</summary>
public sealed record SesionDto(string Area, string Usuario, string? Rol, int SegundosHastaInactividad, int SegundosHastaVidaMaxima);

/// <summary>ADR-0021. Todas las operaciones evalúan PoliticaDeSesion primero: si venció, CerrarAsync y lanza SesionTerminadaException.</summary>
public sealed class ServicioDeSesion(AlmacenDeSesiones almacen, IClienteDeIdentidad identidad,
    IOptions<OpcionesDelBff> opciones, TimeProvider reloj, ILogger<ServicioDeSesion> log)
{
    /// <summary>Si al access token le queda menos, el proxy lo renueva antes de reenviar.</summary>
    public static readonly TimeSpan MargenDeRenovacion = TimeSpan.FromSeconds(30);

    /// <summary>NO cuenta como actividad (RN-25).</summary>
    /// <exception cref="SesionTerminadaException">Sin sesión (SinSesion) o vencida.</exception>
    public Task<SesionDto> ConsultarAsync(string? clave, CancellationToken cancellationToken);

    /// <summary>
    /// Latido. Con candado: renueva los tokens (alinea la inactividad de Keycloak) y después registra UltimaActividad = ahora.
    /// RenovacionRechazadaException → cierra y lanza SesionTerminadaException(Revocada) (CL-12).
    /// IdentidadSinRespuestaException → registra la actividad igualmente y responde.
    /// </summary>
    public Task<SesionDto> RegistrarActividadAsync(string? clave, CancellationToken cancellationToken);

    /// <summary>
    /// Para el proxy. NUNCA registra actividad. Renueva (con candado y volviendo a mirar dentro de él) solo si
    /// AccessTokenVence - ahora &lt; MargenDeRenovacion.
    /// </summary>
    /// <exception cref="SesionTerminadaException">Sin sesión, vencida o renovación rechazada (Revocada).</exception>
    /// <exception cref="IdentidadSinRespuestaException">Hacía falta renovar y Keycloak no respondió (503; la sesión sigue).</exception>
    public Task<string> ObtenerAccessTokenAsync(string? clave, CancellationToken cancellationToken);

    /// <summary>
    /// Cierre en Keycloak por el canal trasero (mejor esfuerzo) y borrado del almacén. Idempotente: sin sesión no hace
    /// nada. Usado por /bff/salir, por el vencimiento, por la revocación y por RN-26.
    /// </summary>
    public Task CerrarAsync(string? clave, CancellationToken cancellationToken);
}
```

#### Bff: identidad (frontera con Keycloak)

```csharp
namespace CoreBancario.Bff.Identidad;

/// <summary>Tokens que devuelve una renovación. IdToken null si Keycloak no lo reenvía (se conserva el anterior).</summary>
public sealed record TokensRenovados(string AccessToken, string RefreshToken, string? IdToken, DateTimeOffset AccessTokenVence)
{
    public override string ToString();   // sin tokens
}

/// <summary>400 invalid_grant al renovar: la sesión de Keycloak ya no existe (vencida, cerrada o acceso deshabilitado).</summary>
public sealed class RenovacionRechazadaException : Exception { public RenovacionRechazadaException(); }

/// <summary>Keycloak no respondió (red, tiempo de espera) o respondió 5xx.</summary>
public sealed class IdentidadSinRespuestaException(Exception? causa = null) : Exception;

/// <summary>
/// Puerto hacia los endpoints OIDC de Keycloak que usa el BFF fuera del flujo del navegador. Es una interfaz porque es
/// una frontera con un sistema externo y las pruebas del BFF necesitan provocar rechazos y silencios.
/// </summary>
public interface IClienteDeIdentidad
{
    /// <summary>POST token_endpoint: grant_type=refresh_token, client_id y client_secret del área.</summary>
    /// <exception cref="RenovacionRechazadaException"/>
    /// <exception cref="IdentidadSinRespuestaException"/>
    Task<TokensRenovados> RenovarAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken);

    /// <summary>POST al logout del realm por el canal trasero (derivado de token_endpoint) con client_id, client_secret y refresh_token. Nunca lanza: registra un warning.</summary>
    Task CerrarSesionAsync(AreaDeSesion area, string refreshToken, CancellationToken cancellationToken);

    /// <summary>GET al documento de descubrimiento del área con 3 s de espera máxima (CL-05). Nunca lanza.</summary>
    Task<bool> EstaDisponibleAsync(AreaDeSesion area, CancellationToken cancellationToken);
}

/// <summary>
/// Implementación con HttpClient tipado (10 s de espera). Los endpoints salen del ConfigurationManager del esquema OIDC
/// del área (respeta Metadatos). Nunca registra tokens ni cuerpos de respuesta.
/// </summary>
public sealed class ClienteDeIdentidadKeycloak(HttpClient http, IOptionsMonitor<OpenIdConnectOptions> oidc,
    IOptions<OpcionesDelBff> opciones, TimeProvider reloj, ILogger<ClienteDeIdentidadKeycloak> log) : IClienteDeIdentidad;
```

#### Bff: inicio de sesión, seguridad, errores y endpoints

```csharp
namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>CL-08: solo rutas internas de la SPA del mismo área. Nunca devuelve una URL absoluta (sin redirección abierta).</summary>
public static class RutaDeRegreso
{
    public const int LongitudMaxima = 2048;
    /// <summary>
    /// Devuelve regreso si: no es null ni vacío; empieza por "/" y su segundo carácter no es "/" ni "\"; no contiene "\",
    /// caracteres de control ni "://"; mide ≤ 2048; y no empieza por "/bff" ni por "/api". Para Clientes, además, no es
    /// "/backoffice" ni empieza por "/backoffice/"; para Empleados, es "/backoffice" o empieza por "/backoffice/" (sin
    /// distinguir mayúsculas). Si no cumple: AreasDeSesion.InicioDeLaSpa(area).
    /// </summary>
    public static string Normalizar(AreaDeSesion area, string? regreso);
}

public static class Avisos
{
    public const string InicioFallido = "inicio-fallido";
    public const string IdentidadNoDisponible = "identidad-no-disponible";
    public const string OtraSesionAbierta = "otra-sesion-abierta";
    public const string ClaveActualizada = "clave-actualizada";
    public const string ClaveSinCambios = "clave-sin-cambios";
    /// <summary>"{PrefijoDeLaSpa(area)}?aviso={aviso}" (Clientes: "/?aviso=…").</summary>
    public static string UrlDe(AreaDeSesion area, string aviso);
}

/// <summary>Claves de AuthenticationProperties.Items que usa el BFF.</summary>
public static class PropiedadesDeSesion
{
    public const string Area = "cb:area";
    public const string Clave = "cb:clave";
    public const string Accion = "cb:accion";        // "UPDATE_PASSWORD" en cambiar clave
    public const string Inicio = "cb:inicio";        // se conserva en un regreso con el mismo sid
}

public static class ConfiguracionDeSesion
{
    public const string EsquemaDeCookie = "Sesion";
    /// <summary>"__Host-cb-sesion" fuera de Development; "cb-sesion" en Development (HTTP).</summary>
    public static string NombreDeLaCookie(IHostEnvironment entorno);

    /// <summary>
    /// AddAuthentication(Sesion) + AddCookie(Sesion: HttpOnly, SameSite Strict, SecurePolicy Always fuera de Development,
    /// sin Expires, SlidingExpiration = false, SessionStore = AlmacenDeSesiones, OnRedirectToLogin/OnRedirectToAccessDenied
    /// → 401/403 sin redirigir) + dos AddOpenIdConnect (uno por área): Authority = Emisor, MetadataAddress = Metadatos,
    /// ClientId/ClientSecret, ResponseType code, ResponseMode query, UsePkce, SaveTokens, Scope solo "openid",
    /// MapInboundClaims = false, sin UserInfo, CallbackPath = AreasDeSesion.RutaDeRegresoOidc(area),
    /// PushedAuthorizationBehavior.Disable, ValidIssuer = Emisor, cookies de correlación y nonce HttpOnly y SameSite Lax,
    /// RequireHttpsMetadata fuera de Development, Events = EventosDeOidc. Registra AlmacenDeSesiones (singleton),
    /// ServicioDeSesion (scoped), IClienteDeIdentidad (AddHttpClient) y TryAddSingleton(TimeProvider.System).
    /// </summary>
    public static IServiceCollection AddSesionDelNavegador(this IServiceCollection services, IConfiguration configuracion, IHostEnvironment entorno);
}

/// <summary>Eventos de los dos esquemas OIDC; uno por área.</summary>
public sealed class EventosDeOidc(AreaDeSesion area) : OpenIdConnectEvents
{
    /// <summary>prompt=login (CL-09), salvo Items[Accion] = "UPDATE_PASSWORD": entonces kc_action=UPDATE_PASSWORD y sin prompt.</summary>
    public override Task RedirectToIdentityProvider(RedirectContext context);
    /// <summary>RNF-05: ProtocolMessage.Iss distinto del Emisor del área, o ausente → context.Fail.</summary>
    public override Task MessageReceived(MessageReceivedContext context);
    /// <summary>
    /// Con Items[Accion]: RedirectUri = "/mis-cuentas?aviso=clave-actualizada" si kc_action_status = "success"; si no,
    /// "?aviso=clave-sin-cambios". Siempre: Items[Area].
    /// </summary>
    public override Task TokenValidated(TokenValidatedContext context);
    /// <summary>
    /// RN-26: si el navegador ya tiene sesión con el MISMO sid, conserva su Inicio (Items[Inicio]) y la reemplaza; si es
    /// de otro sid (u otra área), ServicioDeSesion.CerrarAsync de la anterior antes de guardar la nueva.
    /// </summary>
    public override Task TicketReceived(TicketReceivedContext context);
    /// <summary>
    /// CL-01/CL-02: HandleResponse(). Con sesión vigente de esta área → 302 a InicioDeLaSpa; si no → 302 a
    /// Avisos.UrlDe(area, InicioFallido). Registra el motivo (sin code, state ni tokens).
    /// </summary>
    public override Task RemoteFailure(RemoteFailureContext context);
}

namespace CoreBancario.Bff.Seguridad;

/// <summary>
/// RNF-02 (ADR-0020). Antes de autenticar. Protege /api/** y /bff/**, salvo GET /bff/iniciar-sesion/*, GET
/// /bff/cambiar-clave y los dos CallbackPath. Rechaza con 403 "peticion-no-confiable" si falta "X-CSRF: 1" o si hay
/// Sec-Fetch-Site y no es "same-origin". Una petición rechazada nunca llega a la API.
/// </summary>
public sealed class ProteccionCsrf(RequestDelegate siguiente)
{
    public const string Encabezado = "X-CSRF";
    public const string Valor = "1";
    public Task InvokeAsync(HttpContext contexto);
}

/// <summary>En /bff/** y /api/**: Cache-Control: no-store y X-Content-Type-Options: nosniff (CL-11).</summary>
public sealed class EncabezadosDeSeguridad(RequestDelegate siguiente) { public Task InvokeAsync(HttpContext contexto); }

namespace CoreBancario.Bff.Errores;

/// <summary>Contrato estable con la SPA (como ADR-0011). Nunca se renombran.</summary>
public static class CodigosDelBff
{
    public const string SinSesion = "sin-sesion";
    public const string SesionVencidaPorInactividad = "sesion-vencida-por-inactividad";
    public const string SesionVencidaPorVidaMaxima = "sesion-vencida-por-vida-maxima";
    public const string SesionRevocada = "sesion-revocada";
    public const string PeticionNoConfiable = "peticion-no-confiable";
    public const string IdentidadSinRespuesta = "identidad-sin-respuesta";
    public static string De(MotivoDeCierre motivo);
}

/// <summary>ProblemDetails {title, status, codigo, traceId}, sin detail. 401 lleva Cache-Control: no-store.</summary>
public static class RespuestasDelBff
{
    public static IResult SesionTerminada(MotivoDeCierre motivo);              // 401
    public static Task EscribirPeticionNoConfiableAsync(HttpContext contexto);  // 403, desde el middleware
    public static IResult IdentidadSinRespuesta();                            // 503
}

namespace CoreBancario.Bff.Endpoints;

/// <summary>Las cinco rutas /bff de la sección 3.1, con WithName y metadatos de respuesta. La clave sale de Items[Clave].</summary>
public static class EndpointsDelBff
{
    public static IEndpointRouteBuilder MapEndpointsDelBff(this IEndpointRouteBuilder app);
}

namespace CoreBancario.Bff.Proxy;

/// <summary>
/// /api/{**resto} → OpcionesDeLaApi.Url (YARP 2.3.0, AddHttpForwarder + MapForwarder). Antes de reenviar, un paso
/// previo obtiene el token con ServicioDeSesion.ObtenerAccessTokenAsync: SesionTerminadaException → 401 del BFF;
/// IdentidadSinRespuestaException → 503, en los dos casos SIN llamar a la API. Transformaciones: quita el prefijo /api;
/// quita Cookie, X-CSRF y Authorization entrantes; pone Authorization: Bearer; quita Set-Cookie de la respuesta.
/// </summary>
public static class ProxyDeLaApi
{
    public const string Prefijo = "/api";
    public static IEndpointRouteBuilder MapProxyDeLaApi(this IEndpointRouteBuilder app);
}
```

**Añadidos del Build (004a) fuera de las firmas de arriba** (aprobados por el autor tras `/sdd-review`, 2026-10-27):

- `AlmacenDeSesiones.ObtenerParaEvaluar(string clave)` devuelve la sesión aunque haya pasado su `Inicio + VidaMaxima`. `Obtener` devuelve `null` en ese instante exacto, pero la entrada se conserva 1 minuto más (la libera el temporizador). Así `ServicioDeSesion` y `RetrieveAsync` pueden responder `sesion-vencida-por-vida-maxima` a la petición que llega justo después, y no un genérico `sin-sesion`.
- `PropiedadesDeSesion.LeerClaveAsync(HttpContext)` lee `Items[Clave]` del ticket de la cookie, o devuelve `null`. La comparten los endpoints, el *proxy* y `EventosDeOidc`.
- **Rotación de la clave en cada inicio de sesión (contra la fijación de sesión, RNF-01; hallazgo del ciclo 1 de `review.md`):**
  - Cada regreso OIDC recibe una clave nueva.
  - `TicketReceived` lee la clave de la cookie anterior **sin pasar por el manejador de cookies**. Si el manejador autenticara la cookie, recordaría la clave y llamaría a `RenewAsync` con ella en vez de a `StoreAsync`.
  - Después borra esa sesión del almacén:
    - con el mismo `sid`, conserva el `Inicio` y no cierra Keycloak;
    - con otro `sid` u otra área, llama a `CerrarAsync`.
  - `StoreAsync` guarda entonces la sesión nueva con una clave nueva, y la cookie anterior deja de servir.
  - `RenewAsync` solo reemplaza una sesión bajo una clave existente si es de la misma identidad (mismo `sid` y misma área) y el ticket trae tokens.
  - Lo demuestra `F004_RN26_RegresoDeOtraSesion_LaCookieAnteriorNoAbreLaNueva`.
- `Bff:RedesDeConfianza` (opcional) es la lista de redes CIDR de las que se aceptan los encabezados `X-Forwarded-*`. Sin valor, se confía en cualquier origen, porque el BFF no publica puertos y solo lo alcanza el contenedor web.

**`Program.cs` del BFF, en orden:**

1. `AddSesionDelNavegador`, `AddHttpForwarder`, `AddOpenApi` (con `CodigosDelBffEnOpenApi`), `AddProblemDetails` y `AddForwardedHeaders` (detrás de nginx).
2. `UseForwardedHeaders`, `EncabezadosDeSeguridad`, `ProteccionCsrf`, `UseAuthentication` y `UseAuthorization`.
3. `MapEndpointsDelBff` y `MapProxyDeLaApi`.

Solo en Development: `MapOpenApi()`. **Logs** (`appsettings.json`): `Microsoft.AspNetCore` y `Yarp` en `Warning`, para que ni la URL con el número de documento ni los encabezados lleguen al log (RNF-04). El paquete de generación del contrato exige que `Program` arranque sin secretos, y por eso las opciones se leen de forma perezosa, como en la API (ADR-0019).

**Paquetes del BFF:**

| Paquete | Versión |
|---|---|
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | 10.0.12 |
| `Yarp.ReverseProxy` | 2.3.0 |
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 |
| `Microsoft.Extensions.ApiDescription.Server` | 10.0.12 |

En `CoreBancario.Bff.Tests`: `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 y `xunit.v3.mtp-v2` 4.0.1.

#### Api (004a)

```csharp
// OpcionesDeAutenticacion.cs: OpcionesDeEmisor gana una propiedad
public sealed class OpcionesDeEmisor
{
    public string Emisor { get; set; } = "";
    /// <summary>Documento de descubrimiento por el canal trasero (compose). null → se deriva de Emisor. No cambia ValidIssuer.</summary>
    public string? Metadatos { get; set; }
}
// ConfiguracionDeAutenticacion.ConfigurarEsquema: si Metadatos no es null, jwt.MetadataAddress = Metadatos.

// ───────────── src/CoreBancario.Api/OpenApi/CodigosDeErrorEnOpenApi.cs ─────────────
namespace CoreBancario.Api.OpenApi;
/// <summary>
/// components.schemas["CodigoDeError"] = string con enum de TODAS las constantes public const string de CodigosDeError
/// (por reflexión, en orden alfabético). Fuente del tipo CodigoDeError de la SPA (RNF-12).
/// </summary>
public sealed class CodigosDeErrorEnOpenApi : IOpenApiDocumentTransformer
{
    public const string NombreDelEsquema = "CodigoDeError";
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken);
}
// El BFF tiene su gemelo CodigosDelBffEnOpenApi (NombreDelEsquema = "CodigoDeErrorDelBff", sobre CodigosDelBff). Son dos
// clases pequeñas en dos hosts que no se referencian: duplicar es más simple que una librería compartida (DRY a la tercera).
```

#### Application e Infrastructure (004b)

```csharp
namespace CoreBancario.Application.AccesosDigitales;

/// <summary>
/// PrimerIngresoPendiente: el usuario aún tiene UPDATE_PASSWORD o CONFIGURE_TOTP en sus acciones obligatorias. Es
/// opcional (por defecto false) para que las construcciones existentes sigan compilando.
/// </summary>
public sealed record UsuarioDeIdentidad(string Sub, bool Habilitado, Guid? UltimaSolicitud, bool PrimerIngresoPendiente = false);

public sealed record ObtenerEstadoDeAccesoConsulta(Guid ClienteId);

/// <summary>Estado: "SinAcceso" | "Habilitado" | "Deshabilitado". PrimerIngresoPendiente: null salvo si Habilitado.</summary>
public sealed record EstadoDeAccesoDto(Guid ClienteId, string Estado, bool? PrimerIngresoPendiente);

/// <summary>RF-16. Solo lectura: no escribe ni reconcilia. El estado sale de LocalizadorDeAcceso (Keycloak es la fuente, ADR-0017).</summary>
public sealed class ObtenerEstadoDeAccesoHandler(IClienteRepositorio clientes, LocalizadorDeAcceso localizador)
{
    /// <exception cref="RecursoNoEncontradoException">"No existe un cliente con ese identificador." (sin llamar al proveedor).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException">503 (CL-20).</exception>
    /// <exception cref="ErrorDelProveedorDeIdentidadException">502 (CL-20).</exception>
    /// <exception cref="InvalidOperationException">Vínculo sin usuario en el proveedor (500, riesgo 7 de la 003).</exception>
    public Task<EstadoDeAccesoDto> EjecutarAsync(ObtenerEstadoDeAccesoConsulta consulta, CancellationToken cancellationToken);
}
// AddAplicacion: AddScoped<ObtenerEstadoDeAccesoHandler>().

// ProveedorDeIdentidadKeycloak.ComoUsuario: PrimerIngresoPendiente = requiredActions contiene "UPDATE_PASSWORD" o "CONFIGURE_TOTP".
// Un requiredActions ausente o vacío → false.

// EndpointsDeAccesoDigital: grupo "/clientes/{clienteId:guid}/acceso-digital" con RequireAuthorization(Operador) y
// DocumentarErroresComunes; RegistroDeOperacionDelOperador pasa a cada POST. Nuevo:
//   grupo.MapGet("", …) → TypedResults.Ok(EstadoDeAccesoDto), WithName("ObtenerEstadoDeAccesoDigital"),
//   ProducesProblem 404, 502, 503.
```

### 3.4 Firmas públicas (TypeScript, repo `core-bancario-web`)

```
src/
  main.tsx
  app/          rutas.tsx, proveedores.tsx, clienteDeConsultas.ts
  api/          generado/api.d.ts, generado/bff.d.ts (generados), clientes.ts, errores.ts, mensajes.ts
  comun/        dinero.ts, componentes/{Boton,Dialogo,DialogoDeConfirmacion,MensajeDeError,Cargando,EstadoVacio}.tsx
  sesion/       relojDeSesion.ts, registroDeActividad.ts, canalDeSesion.ts, ProveedorDeSesion.tsx, AvisoDeSesion.tsx,
                AreaProtegida.tsx, PantallaDeInicio.tsx, SinPermiso.tsx, OtraSesionAbierta.tsx, avisos.ts
  cliente/      MisCuentas.tsx, DetalleDeCuenta.tsx, BotonCambiarClave.tsx, consultas.ts
  backoffice/   InicioDelBackoffice.tsx (004a); BuscarCliente.tsx, FormularioDeRegistro.tsx, esquemaDeRegistro.ts,
                FichaDeCliente.tsx, AccionesDeCuenta.tsx, AbrirCuenta.tsx, AccesoDigital.tsx, DialogoDeClaveTemporal.tsx,
                acciones.ts, consultas.ts (004b)
e2e/            simulado/**, real/**, ayudas/{totp.ts,keycloak.ts,bff.ts}
```

**Rutas de la SPA:**

| Ruta | Área | Pantalla | Bloque |
|---|---|---|---|
| `/` | Clientes | Con sesión → `/mis-cuentas`; sin sesión → `PantallaDeInicio` | 004a |
| `/mis-cuentas` | Clientes | `MisCuentas` | 004a |
| `/mis-cuentas/:cuentaId` | Clientes | `DetalleDeCuenta` | 004a |
| `/backoffice` | Empleados | Sin sesión → `PantallaDeInicio`; con sesión → `InicioDelBackoffice` (004a) / `BuscarCliente` (004b) | 004a / 004b |
| `/backoffice/clientes/:clienteId` | Empleados | `FichaDeCliente` | 004b |

El registro del cliente se abre **dentro** de `BuscarCliente`, con el tipo y el número ya escritos en el estado del componente. Así el número de documento nunca pasa por una ruta ni por `history.state` (RNF-04).

**`AreaProtegida`** envuelve cada área y decide por `useSesion().estado`:

| `estado` | Qué muestra |
|---|---|
| `cargando` | `Cargando` |
| `sin-sesion` | `PantallaDeInicio`, con `regreso` = la ruta actual |
| `otra-area` | `OtraSesionAbierta` (CL-10) |
| `sin-permiso` | `SinPermiso` (CL-04) |
| `abierta` | Los hijos |

```ts
// ───────────── src/comun/dinero.ts ─────────────
export type Moneda = 'COP' | 'USD';
/**
 * CL-14. Formatea el texto de DineroDto.monto con Intl.NumberFormat('es-CO', { style: 'currency', currency,
 * minimumFractionDigits y maximumFractionDigits: COP 0, USD 2 }).format(monto). Pasa el TEXTO (Intl.NumberFormat v3):
 * nunca usa Number, parseFloat ni el + unario.
 * "1234567","COP" → "$ 1.234.567"; "12345678901234.56","USD" → "US$ 12.345.678.901.234,56"; "0.00","USD" → "US$ 0,00".
 * @throws Error si monto no cumple /^-?\d+(\.\d+)?$/.
 */
export function formatearDinero(monto: string, moneda: Moneda): string;

// ───────────── src/sesion/relojDeSesion.ts (puro) ─────────────
export const ANTICIPO_DEL_AVISO_MS = 60_000;
/** Instantes locales (Date.now()) de cada cierre. */
export interface PlazosLocales { venceInactividad: number; venceVidaMaxima: number }
export function aPlazosLocales(sesion: SesionDto, recibidoEn: number): PlazosLocales;
export type EstadoDelReloj =
  | { tipo: 'normal'; proximoCambioEn: number }
  | { tipo: 'aviso-inactividad'; cierraEn: number }
  | { tipo: 'aviso-vida-maxima'; cierraEn: number }
  | { tipo: 'vencida'; motivo: 'inactividad' | 'vida-maxima' };
/**
 * Vida máxima manda si vence antes o a la vez. Aviso cuando ahora ≥ cierre − 60 s. Vencida cuando ahora ≥ cierre.
 * proximoCambioEn = instante del siguiente cambio de estado (para un único setTimeout).
 */
export function evaluarReloj(plazos: PlazosLocales, ahora: number): EstadoDelReloj;

// ───────────── src/sesion/registroDeActividad.ts ─────────────
export const INTERVALO_MINIMO_ENTRE_LATIDOS_MS = 15_000;
export interface Temporizador { ahora(): number; programar(fn: () => void, ms: number): number; cancelar(id: number): void }
export interface RegistroDeActividad {
  /** RN-25. Envía ya si pasaron ≥ 15 s desde el último envío; si no, deja uno pendiente para cuando se cumplan. */
  notificarInteraccion(): void;
  /** Envía el pendiente, si lo hay. */
  vaciar(): Promise<void>;
  detener(): void;
}
export function crearRegistroDeActividad(enviarLatido: () => Promise<void>, temporizador?: Temporizador): RegistroDeActividad;
/** Escucha click y keydown (fase de captura) en destino. NO escucha mousemove, pointermove, wheel ni scroll. Devuelve la función para dejar de escuchar. */
export function escucharInteraccionesDeliberadas(destino: Document, alInteractuar: () => void): () => void;
// El cambio de pantalla lo notifica ProveedorDeSesion al cambiar location.pathname (no en la primera carga).

// ───────────── src/sesion/canalDeSesion.ts ─────────────
export type MotivoDeCierre = 'usuario' | 'inactividad' | 'vida-maxima' | 'revocada';
export type MensajeDeSesion =
  | { tipo: 'plazos'; sesion: SesionDto; recibidoEn: number }
  | { tipo: 'cerrada'; motivo: MotivoDeCierre };
export interface CanalDeSesion { publicar(mensaje: MensajeDeSesion): void; escuchar(fn: (m: MensajeDeSesion) => void): () => void; cerrar(): void }
/** BroadcastChannel("core-bancario-sesion"). No usa localStorage. */
export function crearCanalDeSesion(): CanalDeSesion;

// ───────────── src/sesion/avisos.ts ─────────────
export type Aviso = 'inicio-fallido' | 'identidad-no-disponible' | 'otra-sesion-abierta' | 'clave-actualizada'
  | 'clave-sin-cambios' | 'inactividad' | 'vida-maxima' | 'revocada' | 'sesion-cerrada';
export const MENSAJES_DE_AVISO: Record<Aviso, string>;
// inactividad → "Tu sesión se cerró por inactividad"; vida-maxima → "Tu sesión terminó; vuelve a iniciar sesión";
// identidad-no-disponible → "El inicio de sesión no está disponible; intenta más tarde"; clave-actualizada → "Clave actualizada";
// revocada → mensaje genérico (CL-12); inicio-fallido → mensaje + "Empezar de nuevo".
/** Lee ?aviso= (valor desconocido → null). */
export function leerAviso(busqueda: string): Aviso | null;
/** Quita ?aviso= de la URL con history.replaceState, sin recargar. */
export function limpiarAviso(): void;

// ───────────── src/sesion/ProveedorDeSesion.tsx ─────────────
export type AreaDeLaApp = 'clientes' | 'empleados';
export type EstadoDeSesionEnLaApp = 'cargando' | 'sin-sesion' | 'otra-area' | 'sin-permiso' | 'abierta';
export interface Sesion {
  estado: EstadoDeSesionEnLaApp;
  datos: SesionDto | null;
  reloj: EstadoDelReloj | null;
  /** POST /bff/salir → vacía la caché de consultas → publica 'cerrada' → va al inicio del área con el aviso del motivo. */
  cerrarSesion(motivo?: MotivoDeCierre): Promise<void>;
  /** POST /bff/actividad (CL-06). */
  seguirConectado(): Promise<void>;
  /** Navegación completa a /bff/iniciar-sesion/{area}?regreso=… (window.location.assign). */
  iniciarSesion(regreso?: string): void;
}
/**
 * Al montar: GET /bff/sesion. Engancha escucharInteraccionesDeliberadas + crearRegistroDeActividad, el canal de
 * sesión, un único setTimeout a reloj.proximoCambioEn y 'pageshow' con persisted (bfcache, CL-11) → vuelve a consultar.
 * Al vencer: cerrarSesion('inactividad' | 'vida-maxima'). Cualquier 401 de /api o /bff termina la sesión con su motivo.
 */
export function ProveedorDeSesion(props: { area: AreaDeLaApp; children: React.ReactNode }): JSX.Element;
export function useSesion(): Sesion;
/** CL-06 y CL-07. Diálogo (role="alertdialog") con cuenta atrás; "Seguir conectado" solo en aviso-inactividad. Foco inicial en la primera acción. */
export function AvisoDeSesion(): JSX.Element | null;

// ───────────── src/api/clientes.ts ─────────────
import type { paths } from './generado/api';
import type { paths as pathsDelBff } from './generado/bff';
/** openapi-fetch con baseUrl "/api". */
export function crearClienteDeLaApi(alTerminarSesion: (motivo: MotivoDeCierre) => void): Client<paths>;
/** openapi-fetch con baseUrl "". */
export function crearClienteDelBff(alTerminarSesion: (motivo: MotivoDeCierre) => void): Client<pathsDelBff>;
/**
 * Middleware compartido: pone "X-CSRF: 1" y credentials "same-origin". Un 401 llama a alTerminarSesion con el motivo de
 * su codigo (sin-sesion y no-autenticado → 'revocada', vencida-por-inactividad → 'inactividad', vencida-por-vida-maxima
 * → 'vida-maxima', revocada → 'revocada'). No reintenta nada.
 */
export function crearMiddlewareDeSesion(alTerminarSesion: (motivo: MotivoDeCierre) => void): Middleware;

// ───────────── src/api/errores.ts y mensajes.ts ─────────────
export type CodigoDeError = components['schemas']['CodigoDeError'];   // del contrato de la API
/** Uno por código: si falta, no compila (CA-34). conflicto-de-concurrencia → "La cuenta cambió mientras tanto"; identidad-error e identidad-sin-respuesta → "…puedes reintentar sin riesgo…" (CL-19). */
export const MENSAJES_DE_ERROR: Record<CodigoDeError, string>;
export const MENSAJE_GENERICO: string;   // "Algo salió mal. Si continúa, comunícate con soporte e indica este código:"
export interface ErrorDeLaApi {
  estado: number;
  codigo: CodigoDeError | null;   // null si es desconocido o no hay cuerpo
  mensaje: string;                // MENSAJES_DE_ERROR[codigo] o MENSAJE_GENERICO; NUNCA detail
  traceId: string | null;
  errores: Record<string, string[]>;   // de ValidationProblemDetails (400); {} si no hay
}
export function interpretarError(estado: number, cuerpo: unknown): ErrorDeLaApi;
// 500, o un código desconocido → MENSAJE_GENERICO con su traceId (CL-24, RNF-12).

// ───────────── src/backoffice/acciones.ts (004b, puro) ─────────────
export type EstadoDeCuenta = 'Activa' | 'Bloqueada' | 'Cerrada';
export type AccionDeCuenta = 'Bloquear' | 'Desbloquear' | 'Cerrar';
/** RN-14: Activa → [Bloquear, Cerrar]; Bloqueada → [Desbloquear]; Cerrada → []. */
export function accionesDeCuenta(estado: EstadoDeCuenta): AccionDeCuenta[];
export type EstadoDelAcceso = 'SinAcceso' | 'Habilitado' | 'Deshabilitado' | 'no-disponible';
export type AccionDeAcceso = 'Habilitar' | 'Restablecer' | 'Deshabilitar';
/** RF-16: SinAcceso → [Habilitar]; Habilitado → [Restablecer, Deshabilitar]; Deshabilitado → [Habilitar]; no-disponible → [] (CL-20). */
export function accionesDeAcceso(estado: EstadoDelAcceso): AccionDeAcceso[];

// ───────────── src/backoffice/esquemaDeRegistro.ts (004b) ─────────────
/**
 * Zod con las reglas de la spec 002, sección 4. Tipo CC, CE o PA. Número normalizado (sin espacios, puntos ni guiones,
 * en mayúsculas): CC/CE solo dígitos, de 3 a 10, sin empezar por 0; PA letras y dígitos, de 5 a 15. Nombres y
 * apellidos: de 1 a 100 tras recortar, con letras (tildes y ñ), espacios, apóstrofo y guion. Correo usuario@dominio, ≤ 254.
 * Teléfono E.164 tras quitar espacios, guiones y paréntesis: + y de 8 a 15 dígitos. Avisa; la API decide.
 */
export const esquemaDeRegistro: z.ZodType<RegistrarClienteRequest>;

// ───────────── src/backoffice/DialogoDeClaveTemporal.tsx (004b) ─────────────
/**
 * CL-17. Modal con isDismissable={false} e isKeyboardDismissDisabled, role="alertdialog". Título con la ACCIÓN que
 * devolvió la API ("Habilitado" o "Restablecido", CL-21), la clave, "Copiar" (navigator.clipboard) y "Ya la entregué",
 * que llama a onEntregada. El componente que lo usa hace mutation.reset() al cerrarlo: la clave no queda en la caché.
 */
export function DialogoDeClaveTemporal(props: { accion: 'Habilitar' | 'Restablecer'; claveTemporal: string; onEntregada: () => void }): JSX.Element;
```

**Configuración de TanStack Query (`clienteDeConsultas.ts`):**

- consultas con `retry`: 1 salvo en 401, 403 y 404;
- `refetchOnWindowFocus` en `true`. Es automático, así que **nunca** cuenta como actividad;
- mutaciones con `retry: 0` y `gcTime: 0` (CL-15).

**Botón con estado en curso (`Boton`):**

- con `enCurso`, queda `disabled` y `aria-busy`;
- todas las mutaciones lo usan, así que un doble clic envía una sola petición (CA-24).

**Clave de consulta de la ficha:** `['ficha', clienteId]` y `['acceso', clienteId]`. Tras cada acción de cuenta o de acceso se invalidan (CL-16).

**Añadidos y desviaciones del Build (004a, PR 2 del web)** (las 7 de `construccion/004-front-base/review.md` en `core-bancario-web#2`; el código manda sobre las firmas de arriba):

1. **`Sesion` tiene dos campos más:**
   - `area: AreaDeLaApp`, el área que muestra esta parte de la app, que puede no ser la de la sesión (CL-10);
   - `cerrarYContinuar(regreso?)`, que cierra la sesión de la otra área y va a iniciar sesión en esta (CL-10).

   `Sesion` y `useSesion` viven en `src/sesion/contextoDeSesion.ts` y no en `ProveedorDeSesion.tsx`. La regla `react-refresh` pide que un archivo de componentes solo exporte componentes.
2. **`crearClienteDeLaApi` y `crearMiddlewareDeSesion` aceptan `alSerProhibido?: () => void`.** Un 403 de la API (la autoridad, ADR-0015) lleva a `SinPermiso`: es CL-04 por la vía de la API. El cliente del BFF no lo necesita.
3. **RN-25 con el aviso de sesión abierto.** Mientras se ve `AvisoDeSesion` (reloj en `aviso-inactividad` o `aviso-vida-maxima`), la SPA **deja de escuchar** clics y teclas. Solo "Seguir conectado" cuenta, porque envía su propio latido (CL-06). Mover el foco con Tab dentro del diálogo no significa "sigo aquí". Es una precisión de RN-25 ("clic o tecla"): fuera del aviso, la regla se aplica tal cual.
4. **Piezas que el plan no nombraba:**
   - `comun/dinero.ts`: `formatearSaldo(dinero)` para un `DineroDto`, cuya moneda llega como texto libre. Con una moneda desconocida muestra el monto tal cual, en vez de lanzar.
   - `api/errores.ts`:
     - `FalloDeLaApi`, la excepción que lleva el `ErrorDeLaApi` ya interpretado;
     - `exigirDatos(respuesta)`, que devuelve los datos o lanza `FalloDeLaApi`;
     - `comoErrorDeLaApi(causa)`, que convierte cualquier error de una consulta (una caída de red es el estado 0).
   - `app/clienteDeConsultas.ts`: `crearClienteDeConsultas()`, con la configuración de TanStack Query de arriba.
   - `sesion/coordinadorDeSesion.ts`: guarda el cierre de una sola vez y el canal entre pestañas en un cierre (*closure*) y no en `useRef`, porque los clientes HTTP lo usan al crearse durante el render.
   - `sesion/useAvisoDeLaUrl.ts`: lee `?aviso=` una vez y lo quita de la URL (CA-21).
   - `comun/componentes/Pagina.tsx`, `sesion/BotonCerrarSesion.tsx` y `app/areas.tsx` (`AreaDelCliente`, `AreaDelBackoffice`, `RedireccionAMisCuentas`, `ErrorDelArea`).
5. **Paquetes de ADR-0022 (T12):** faltan `react-hook-form` y `zod`. 004a no tiene formularios, así que instalarlos ahora sería YAGNI. **Llegan en T25** con `esquemaDeRegistro`, con versiones exactas.
6. **`aPlazosLocales` acepta `number | string` en los segundos del `SesionDto`.** .NET 10 declara así los enteros del contrato. Un valor que no es un número finito cuenta como 0, es decir, la sesión se da por vencida: si no se entiende el plazo, cerrar es lo seguro.
7. **RNF-13 en el CI del web:** el paso "Contratos y tipos al día" (`contratos:sincronizar` + `tipos` + `git diff --exit-code`) va en el job de verificación existente, no en `e2e-real`. Cumple T19.

## 4. Modelo de datos y persistencia

- **Base de datos:** sin cambios, sin migraciones. RF-16 lee lo que ya existe (`accesos_digitales`) y Keycloak.
- **Almacén de sesiones del BFF:** en memoria (`AlmacenDeSesiones`), una entrada por navegador con clave aleatoria de 256 bits. Cada entrada contiene `SesionDelNavegador`: área, sub, usuario, `sid`, rol, inicio, última actividad, tokens y principal. Caduca sola en `Inicio + VidaMaxima`. No se persiste (ADR-0020; pregunta abierta 1).
- **Navegador:** solo la cookie `cb-sesion`/`__Host-cb-sesion` (clave cifrada) y las cookies de correlación y *nonce* del regreso OIDC, que duran unos minutos. **Nada** en `localStorage` ni en `sessionStorage`. Entre pestañas, solo mensajes de `BroadcastChannel`.

### Keycloak (cambios en `infra/keycloak/realms/*.json`, 004a)

| Ajuste de `core-bancario-web` | `clientes` | `empleados` | Para qué |
|---|---|---|---|
| `publicClient` | `false` | `false` | ADR-0020: sin cliente público que un XSS pueda usar |
| `clientAuthenticatorType` / `secret` | `client-secret` / `${KC_WEB_CLIENTES_SECRET}` | `client-secret` / `${KC_WEB_EMPLEADOS_SECRET}` | El secreto solo vive en `.env` y en el BFF |
| `redirectUris` | `["http://localhost:5173/bff/signin-oidc/clientes"]` | `["http://localhost:5173/bff/signin-oidc/empleados"]` | Exactas, sin comodín. Cada realm solo acepta el regreso de su área (CL-03) |
| `webOrigins` | `[]` | `[]` | El navegador no llama a Keycloak por `fetch`: sin CORS |
| `attributes."post.logout.redirect.uris"` | `""` | `""` | El cierre es por el canal trasero |
| `attributes."pkce.code.challenge.method"` | `S256` | `S256` | Se conserva (RNF-05) |
| `standardFlowEnabled`, `directAccessGrantsEnabled`, scopes y mapper de audiencia | sin cambios | sin cambios | — |
| `protocolMappers`: `preferred-username-en-token-de-identidad` (`oidc-usermodel-property-mapper`, `username` → `preferred_username`, solo `id.token.claim`) | nuevo | nuevo | **Añadido en el Build (2026-10-27, aprobado por el autor).** Con los scopes `basic` y `roles`, el token de identidad no trae `preferred_username`, y `SesionDelNavegador` lo exige. Va solo en el token de identidad, que se queda en el BFF; no viaja a la API ni a `userinfo`. Se descartó el scope `profile` porque añade nombre, correo y otros datos personales que no se usan |

**`docker-compose.yml`, servicio `keycloak`:**

- `KC_WEB_CLIENTES_SECRET` y `KC_WEB_EMPLEADOS_SECRET` (obligatorias, como `KC_CLIENTES_API_SECRET`);
- `KC_HOSTNAME: http://localhost:${KEYCLOAK_PORT:-8080}` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC: "true"`. El emisor (`iss`) queda fijo en la URL del navegador, y los contenedores pueden pedir el descubrimiento y los tokens por `http://keycloak:8080`. **Hay que recrear el volumen de Keycloak** (riesgo 10 de la 003), y el README lo dice.

### Compose (RNF-16)

Un solo `docker-compose.yml`, en **este** repositorio. Sin perfil, sigue levantando solo PostgreSQL y Keycloak (lo de hoy).

| Servicio | Perfil | Imagen / build | Puerto publicado | Configuración |
|---|---|---|---|---|
| `api` | `app` | `src/CoreBancario.Api/Dockerfile` | **ninguno** (solo red interna) | `ASPNETCORE_ENVIRONMENT=Development` (migra al arrancar). Cadena de conexión a `postgres`. `Autenticacion__{Clientes,Empleados}__Emisor=http://localhost:8080/realms/…` y `…__Metadatos=http://keycloak:8080/realms/…/.well-known/openid-configuration`. `Keycloak__Administracion__UrlBase=http://keycloak:8080` y su secreto |
| `bff` | `app` | `src/CoreBancario.Bff/Dockerfile` | ninguno | `Bff__Api__Url=http://api:8080`. `Bff__{Clientes,Empleados}__Emisor`, `__Metadatos` y `__ClientSecret`. `ASPNETCORE_ENVIRONMENT=Development` (cookie sin `Secure` por HTTP) |
| `web` | `web` | `${CORE_BANCARIO_WEB_DIR:-../core-bancario-web}` (su `Dockerfile`) | `5173:8080` | nginx sirve la SPA con CSP y reenvía `/bff/` y `/api/` a `bff:8080` con `Host $http_host` y `X-Forwarded-*` |

**Comando único:** `docker compose --profile app --profile web up -d --build --wait`, con los dos repositorios clonados uno al lado del otro.

En el CI de este repo, el job `entorno-local` añade `--profile app` y comprueba `/health` de la API y del BFF desde dentro de la red. El perfil `web` lo ejercita el CI del repo web.

## 5. Casos límite y su manejo

| ID | Caso | Manejo | Test esperado |
|---|---|---|---|
| CL-01 | Regreso con `state` distinto, `iss` de otro emisor o `error` | El manejador valida el `state` y la correlación; `EventosDeOidc.MessageReceived` valida el `iss`; `error` → fallo remoto. `RemoteFailure` → `?aviso=inicio-fallido`, sin cookie de sesión. La SPA muestra el mensaje con "Empezar de nuevo" | Keycloak.Tests `F004_CA04_*` (tres); Web `F004_CA04_AvisoInicioFallido_MuestraMensajeYEmpezarDeNuevo` |
| CL-02 | Regreso reutilizado (atrás o recargar) | La correlación ya se consumió → fallo remoto. Con sesión de esa área → 302 a su inicio; sin sesión → aviso `inicio-fallido`. Nunca una segunda sesión | Keycloak.Tests `F004_CL02_RegresoReutilizado_NoCreaOtraSesionYVaAlInicio` |
| CL-03 | Cliente por `/backoffice` o al revés | Cada área inicia en su realm. Las credenciales del otro realm fallan con el mensaje genérico del tema (003) | Keycloak.Tests `F004_CL03_IniciarEnEmpleados_RedirigeAlRealmDeEmpleados` |
| CL-04 | Sesión válida sin el rol del área | `SesionDto.rol = null` → `SinPermiso` sin datos y con "Cerrar sesión". Un 403 de la API muestra lo mismo | Bff.Tests `F004_CL04_LectorDeRol_*`; Web `F004_CA15_*`, `F004_CL04_Api403_PantallaSinPermiso` |
| CL-05 | Keycloak no responde al iniciar sesión | `iniciar-sesion` consulta antes `EstaDisponibleAsync` (3 s) y responde 302 `?aviso=identidad-no-disponible`. Un fallo del canje cae en `RemoteFailure` | Bff.Tests `F004_CL05_KeycloakNoResponde_RedirigeConAvisoIdentidadNoDisponible`; Web `F004_CL05_Aviso_MuestraMensaje` |
| CL-06 | Aviso de inactividad 60 s antes | `evaluarReloj` → `aviso-inactividad`; `AvisoDeSesion` con "Seguir conectado" (latido) y "Cerrar sesión". Al vencer, `cerrarSesion('inactividad')` | Vitest `F004_CL06_EvaluarReloj_*`; Playwright simulado `F004_CA08_*` |
| CL-07 | Aviso de vida máxima | `aviso-vida-maxima` sin alargar; al vencer, `cerrarSesion('vida-maxima')`. El BFF rechaza en el instante exacto | Vitest `F004_CA09_EvaluarReloj_*`; Bff.Tests `F004_CA09_*`; simulado `F004_CA09_*` |
| CL-08 | Ruta de regreso | `RutaDeRegreso.Normalizar` en el BFF. El destino viaja en el `state` protegido, nunca en una cookie legible ni en la URL de Keycloak | Bff.Tests `F004_CL08_RutaDeRegreso_*` (teoría); Keycloak.Tests `F004_CA13_*` |
| CL-09 | Pasada la inactividad, Keycloak no reabre | `prompt=login` siempre, y cierre por el canal trasero al detectar el vencimiento | Keycloak.Tests `F004_CA10_*`, `F004_CL09_IniciarConSesionDeKeycloakViva_PideCredenciales` |
| CL-10 | Sesión en un área y entrada a la otra | La SPA ve `area` distinta → `OtraSesionAbierta` ("Cerrar sesión y continuar": `POST /bff/salir` y luego navegación a `iniciar-sesion`). El BFF también lo impone (302 `otra-sesion-abierta`) | Keycloak.Tests `F004_CA14_*`; Web `F004_CA14_*` |
| CL-11 | Atrás tras cerrar sesión | `cerrarSesion` vacía la caché antes de navegar con `location.replace`. `pageshow` con `persisted` vuelve a consultar `/bff/sesion` (bfcache). `/api` y `/bff` responden `no-store`, y nginx sirve `index.html` con `no-store` | Simulado `F004_CA12_*`; real `F004_CA12_TrasCerrarSesion_AtrasNoMuestraSaldos` |
| CL-12 | Acceso deshabilitado o restablecido con sesión abierta | Keycloak cierra sus sesiones (003b). El siguiente latido o renovación del proxy recibe `invalid_grant` → `SesionTerminadaException(Revocada)` → 401 `sesion-revocada` → inicio con un mensaje genérico. Como mucho, al vencer el access token (≤ 270 s) | Keycloak.Tests `F004_CA16_*`; Bff.Tests `F004_CL12_RenovacionRechazada_CierraComoRevocada`; Web `F004_CL12_*` |
| CL-13 | La sesión vence durante un envío | El 401 del *proxy* termina la sesión con su motivo, y lo escrito se pierde (no se guarda nada). Ninguna mutación se reintenta | Web `F004_CL13_EnvioConSesionVencida_VaAlInicioConMensaje` |
| CL-14 | Saldo largo o con decimales | `formatearDinero` con el texto | Vitest y simulado `F004_CA19_*` |
| CL-15 | Doble clic o reintento | `Boton enCurso`, `retry: 0` en las mutaciones. **Límite conocido** documentado: dos pestañas o un reintento de red pueden abrir dos cuentas | Vitest `F004_CA24_*`, `F004_CL15_ErrorAlAbrir_NoReintentaSola` |
| CL-16 | 409 `conflicto-de-concurrencia` | Mensaje "La cuenta cambió mientras tanto" e `invalidateQueries(['ficha', id])` | Vitest `F004_CA27_*` |
| CL-17 | Clave temporal una sola vez | `DialogoDeClaveTemporal`: no se cierra con Esc ni con clic fuera, solo con "Ya la entregué". Después `mutation.reset()`, y la clave no queda en el DOM, la caché, la URL ni el almacenamiento | Vitest `F004_CA30_*`; simulado `F004_CA30_ClaveTemporal_NoQuedaEnUrlNiAlmacenamiento` |
| CL-18 | Cerrar con saldo | 422 `saldo-distinto-de-cero` → su mensaje; la ficha no cambia | Vitest `F004_CA26_*` |
| CL-19 | 502/503 al gestionar el acceso | Mensaje "puedes reintentar sin riesgo", y el botón vuelve a estar activo. Un reintento con éxito muestra la clave **nueva** | Vitest `F004_CL19_*` |
| CL-20 | Estado del acceso no disponible | 502/503 del endpoint de RF-16 → la ficha se ve igual, con "Estado del acceso no disponible", y `accionesDeAcceso('no-disponible') = []` | Api.Tests `F004_CL20_*`; Vitest `F004_CA32_*` |
| CL-21 | Estado desactualizado | La confirmación de "Habilitar" avisa siempre de lo que pasa si ya tuviera acceso. El diálogo usa `respuesta.accion` | Vitest `F004_CL21_BotonHabilitarPeroApiRestablece_DialogoDiceRestablecido` |
| CL-22 | Documento no registrado | 404 → "No hay un cliente con ese documento" y "Registrarlo", con el tipo y el número en el estado del componente | Vitest `F004_CL22_*` |
| CL-23 | 409 `documento-duplicado` al registrar | "Ir a la ficha": vuelve a buscar ese documento y abre su ficha | Vitest `F004_CL23_*` |
| CL-24 | API caída o 500 | En las consultas, `MensajeDeError` con "Reintentar" (`refetch`); en las acciones, `MENSAJE_GENERICO` con el `traceId`. Nunca una pantalla en blanco (un *error boundary* por área) | Vitest `F004_CA18_ErrorDeLaApi_Reintentar`, `F004_CA34_*` |
| Regla del autor | Un `GET` que cambiaría datos | No existe ninguno: `/bff/salir` y `/bff/actividad` son `POST` (un `GET` → 405), y los `GET` de la API son solo consultas | Bff.Tests `F004_RNF02_OperacionesDelBffQueCambianDatos_NoRespondenAGet`; Api.Tests `F004_RNF02_ContratoOpenApi_LosGetSonSoloConsultas` |

## 6. Estrategia para los requisitos no funcionales

| RNF | Cómo se cumple | Cómo se verifica |
|---|---|---|
| RNF-01 | BFF con almacén en el servidor, cookie `HttpOnly` con clave opaca, cliente confidencial, `SesionDto` sin tokens y *proxy* sin `Cookie` hacia la API (ADR-0020) | Real `F004_CA05_ConSesion_NingunTokenLegibleDesdeLaPagina` (`document.cookie`, los dos almacenamientos y todos los cuerpos recibidos, sin el patrón JWT `eyJ…\.eyJ…`). Bff.Tests `F004_RNF01_SesionDto_NoContieneTokens`, `F004_RNF01_ProxyNoReenviaLaCookieALaApi`, `F004_RNF01_TokensDeLaSesion_ToStringNoLosMuestra`. Api.Tests `F004_ADR0020_ClienteWeb_EsConfidencialConPkceS256YSecretoComoMarcador` |
| RNF-02 | `SameSite=Strict` + `X-CSRF: 1` + `Sec-Fetch-Site` + sin CORS + ningún `GET` que cambie datos | Bff.Tests `F004_CA06_*`, `F004_RNF02_*`; real `F004_CA06_OtroOrigenDelMismoSitio_NoPuedeCambiarDatos` (página servida en `http://localhost:5174`, mismo sitio y otro origen: un `fetch` con el encabezado lo bloquea el *preflight*, y un formulario lo rechaza el BFF; se comprueba por la API que no se abrió ninguna cuenta) |
| RNF-03 | nginx: `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'`, más `X-Content-Type-Options: nosniff` y `Referrer-Policy: no-referrer`. React aplica estilos por CSSOM, que la CSP permite. El inicio de sesión navega con `location.assign`, no con un formulario | Real `F004_RNF03_LaAppSeSirveConCsp_SinScriptsEnLinea` (encabezado presente; un `<script>` en línea inyectado no se ejecuta y dispara `securitypolicyviolation`; cero violaciones durante los recorridos). En desarrollo no hay CSP (ADR-0022) |
| RNF-04 | Número de documento solo en el estado del componente y en la URL de la *petición* (`/api/clientes/por-documento?…`), nunca en la del navegador. Logs `Microsoft.AspNetCore` y `Yarp` en `Warning` en el BFF (la API ya lo tiene). nginx registra `$uri` y no `$request`. Clave temporal solo en el diálogo. Tokens nunca en la URL (canje por el canal trasero, cierre sin `id_token_hint`) | Bff.Tests `F004_RNF04_BuscarPorDocumento_ElLogDelBffNoContieneElNumero`; Vitest `F004_CA22_*` (URL sin el número); simulado `F004_CA30_*`. Ver la pregunta abierta 2 |
| RNF-05 | `UsePkce`, `state` y correlación de ASP.NET Core, `iss` en `MessageReceived`, `ValidIssuer` en el token de identidad y PKCE `S256` exigido por el realm | Keycloak.Tests `F004_CA04_*`, `F004_RNF05_AutorizacionDelBff_UsaCodigoConPkceS256YPromptLogin` |
| RNF-06 | El BFF evalúa la política en **cada** petición (rechazo en el instante exacto, aunque haya peticiones automáticas) y la SPA cierra con un único `setTimeout` | Bff.Tests `F004_CA07_*`, `F004_RNF06_PeticionUnSegundoDespuesDelLimite_Responde401`; Keycloak.Tests `F004_RN25_PeticionesAutomaticasPorElBff_NoMantienenLaSesion` |
| RNF-07 | `BroadcastChannel` (`cerrada` y `plazos`), y 401 en la siguiente petición como respaldo | Simulado `F004_CA11_*` (dos páginas del mismo contexto) |
| RNF-08 | React Aria Components, foco visible (`:focus-visible`), `AvisoDeSesion` con ≥ 20 s (60 s) para responder | Simulado `F004_CA35_*` (axe en cada pantalla, WCAG 2.1 A y AA, y flujos solo con teclado); real: recorrido del cliente solo con teclado y axe en la pantalla de login de Keycloak |
| RNF-09 | CSS *mobile-first* en el área del cliente; backoffice a partir de 1280 px | Simulado `F004_CA36_*` (`scrollWidth ≤ clientWidth` en cada pantalla, a 360 y 1280 px) |
| RNF-10 | Sin APIs fuera de las dos últimas versiones (`BroadcastChannel`, `Intl.NumberFormat` v3, `:focus-visible`). Chromium en el CI | Revisión; README con la matriz soportada |
| RNF-11 | Mis cuentas es una sola consulta | Real `F004_RNF11_TrasElRegreso_MisCuentasVisibleEnDosSegundos` (desde la respuesta del regreso hasta el encabezado "Mis cuentas") |
| RNF-12 | `interpretarError` y `MENSAJES_DE_ERROR: Record<CodigoDeError, string>` sobre el *enum* del contrato | Vitest `F004_CA34_*`; Api.Tests `F004_RNF12_ContratoOpenApi_EnumeraTodosLosCodigosDeError`; Bff.Tests `F004_RNF12_ContratoDelBff_EnumeraSusCodigos` |
| RNF-13 | `contratos/` + `npm run tipos` + `typecheck` estricto. El contrato cambia primero en la API (orden de PR, sección 1.1) | CI web: `git diff --exit-code src/api/generado contratos` tras `contratos:sincronizar` + `tipos` |
| RNF-14 | Flujo `/sdd-build` en los dos repositorios: un commit `test(004): pruebas en rojo del bloque 004x` por repo y bloque | Historial de commits |
| RNF-15 | Dos recorridos Playwright contra el compose completo (sección 7) | Job `e2e-real` del CI web |
| RNF-16 | Compose con perfiles `app` y `web` (sección 4) | CI `entorno-local` (este repo) y `e2e-real` (web); README |

## 7. Estrategia de pruebas

| Nivel | Qué se prueba | Proyecto |
|---|---|---|
| Unitarias (Application, 004b) | `ObtenerEstadoDeAccesoHandler` con el `ProveedorDeIdentidadEnMemoria` existente | `tests/CoreBancario.Application.Tests` |
| Integración (Infrastructure, 004b) | `ComoUsuario` lee `requiredActions` (con el `HttpMessageHandler` falso de la 003) | `tests/CoreBancario.Infrastructure.Tests` |
| API | Contrato (*enum* de códigos, `GET` solo consultas, fila de RF-16), endpoint de RF-16, `Metadatos`, configuración del realm | `tests/CoreBancario.Api.Tests` |
| **BFF (nuevo)** | Funciones puras con `FakeTimeProvider`; `ServicioDeSesion` con un `IClienteDeIdentidad` falso; HTTP con `WebApplicationFactory<MarcadorDelBff>`: CSRF, 401/405, avisos, *proxy* contra una API falsa | `tests/CoreBancario.Bff.Tests` |
| Keycloak real (.NET) | El BFF real contra Keycloak real y la API real: login por el BFF, `iss`/`state`/`error`, `prompt=login`, RN-26, ruta de regreso, cambio de clave, revocación, inactividad con tiempos cortos | `tests/CoreBancario.Keycloak.Tests` |
| Web: unitarias y componentes | `dinero`, `relojDeSesion`, `registroDeActividad` (temporizador falso), `canalDeSesion`, `errores`, `acciones`, `esquemaDeRegistro`; pantallas con MSW | `core-bancario-web`, Vitest |
| Web: navegador simulado | Chromium real con `/bff` y `/api` simulados (`page.route`) y el reloj de Playwright: aviso de sesión, pestañas, atrás, axe, teclado y anchos | `core-bancario-web/e2e/simulado` |
| Web: navegador real | Compose completo con Keycloak real: los dos recorridos de RNF-15, CSP, CSRF desde otro origen, tokens no legibles | `core-bancario-web/e2e/real` |

**Probar la sesión sin esperar 5 ni 30 min** (pregunta de la sección 10 de la spec), en tres capas. Así no hay dos copias de los realms (ADR-0018):

1. **Lógica pura con un reloj falso:** `PoliticaDeSesion` y `ServicioDeSesion` con `FakeTimeProvider`, y en la SPA `evaluarReloj`, `crearRegistroDeActividad` con un `Temporizador` falso y `page.clock` de Playwright.
2. **Integración con tiempos cortos:** en Keycloak.Tests, el BFF se configura con `Inactividad = 40 s` y `VidaMaxima = 120 s`, y el fixture baja también `ssoSessionMaxLifespan` a 120 s (hoy solo baja la inactividad).
3. **Configuración de producción:** `F004_RN22_TiemposDelBff_CoincidenConLosRealms` lee `src/CoreBancario.Bff/appsettings.json` y los dos realm JSON (5/30 y 15 min/9 h).

**Segundo factor en navegador real (RNF-15):**

- Playwright lee el secreto en base32 de la pantalla de inscripción de Keycloak (modo "¿No puedes escanear?") y calcula el código con `otpauth` (TOTP, SHA-1, 6 dígitos, 30 s);
- como en Keycloak.Tests, antes de un segundo ingreso espera a la siguiente ventana de 30 s.

### Infraestructura de pruebas (la escribe el test-writer)

- **`CoreBancario.Bff.Tests`** (nuevo, xUnit v3 con MTP, **sin** `--ignore-exit-code 8`), añadido a `CoreBancario.slnx`:
  - `BffFixture` con `WebApplicationFactory<MarcadorDelBff>` y estos reemplazos: `TimeProvider` → `FakeTimeProvider`; `IClienteDeIdentidad` → `ClienteDeIdentidadFalso`, que puede aceptar, rechazar (`invalid_grant`), no responder o estar no disponible, y que cuenta renovaciones y cierres; y `IForwarderHttpClientFactory` de YARP → un `HttpMessageInvoker` hacia `ApiFalsa`, que registra método, ruta, encabezados y cuerpo de cada petición reenviada y responde lo que diga la prueba;
  - **sesiones sin Keycloak:** un `IStartupFilter` registrado **solo en `ConfigureTestServices`** expone `POST /__pruebas/sesion`, que hace `SignInAsync(ConfiguracionDeSesion.EsquemaDeCookie, …)` con un principal y tokens de prueba (un access token sin firma con `realm_access`). Así se emite la cookie real con el almacén real. Nunca existe fuera de las pruebas;
  - un proveedor de logs en memoria (el de Api.Tests duplicado: DRY a la tercera).
- **`CoreBancario.Keycloak.Tests`:**
  - `KeycloakFixture` pone `KC_WEB_CLIENTES_SECRET` y `KC_WEB_EMPLEADOS_SECRET` aleatorios y baja también `ssoSessionMaxLifespan`;
  - `RegistrarUriDeRedireccionAsync` acepta varias URI: la de la 003 (`http://localhost:3000/callback`) y la del BFF de pruebas (`http://localhost/bff/signin-oidc/{area}`);
  - `NavegadorSimulado.CanjearCodigoAsync` y `RenovarAsync` envían `client_secret`, porque el cliente ya es confidencial;
  - el fixture levanta además el BFF con `WebApplicationFactory<MarcadorDelBff>`, apuntando al Keycloak del contenedor y con el *proxy* hacia `Api.Server.CreateHandler()`;
  - un `NavegadorDelBff` (cookies a mano, como el simulado) dirige cada petición por **puerto**: el BFF en `TestServer` (`http://localhost`) y Keycloak (`http://localhost:{puerto}`), y recorre el login con AngleSharp y Otp.NET.
- **Api.Tests:**
  - se **sustituye** `F003_ADR0014_ClienteWeb_EsPublicoYUsaCodigoConPkceS256` por `F004_ADR0020_ClienteWeb_EsConfidencialConPkceS256YSecretoComoMarcador` (teoría de los dos realms: `publicClient` false, `secret` igual al marcador, `redirectUris` exactas y `webOrigins` vacío). El comportamiento cambió en la 004, así que la prueba nueva lleva `F004`;
  - en `F002_RNF05_ContratoOpenApi_…` se añade en 004b la fila `ObtenerEstadoDeAccesoDigital` → `"200,401,403,404,500,502,503"`.
- **Web (Playwright):**
  - `playwright.config.ts` con los proyectos `simulado` (`webServer: vite preview`) y `real` (`baseURL http://localhost:5173`, sin `webServer`);
  - `e2e/ayudas/keycloak.ts`: admin de `master` desde variables de entorno. Crea el operador con el rol `operador`;
  - `e2e/ayudas/bff.ts`: llamadas a `/api` con `page.request` (comparte cookies) y `X-CSRF: 1`, para preparar datos con la sesión del operador;
  - `e2e/ayudas/totp.ts`;
  - en `real`, el `globalSetup` **no** baja tiempos: los recorridos de RNF-15 no esperan inactividad.

### Pruebas por bloque

Cada bloque y repositorio tiene su commit en rojo: `test(004): pruebas en rojo del bloque 004a` en cada repo, y después las de 004b.

- **004a — `core-bancario`:** todo Bff.Tests; Api.Tests `F004_ADR0020_*`, `F004_RNF12_*`, `F004_RNF02_ContratoOpenApi_*`, `F004_RN22_*` y `F004_ADR0020_Metadatos_*`; Keycloak.Tests `F004_*` (CA-01, CA-02, CA-04, CA-05, CA-10, CA-13, CA-14, CA-16, CA-21, CL-02, CL-03, CL-09, RN-25 y RNF-05). CA-16 usa el endpoint de deshabilitar de 003b, que ya existe.
- **004a — `core-bancario-web`:** Vitest de `dinero`, sesión, errores, `MisCuentas`, `DetalleDeCuenta`, `BotonCambiarClave` y las pantallas de sesión; simulado de CA-04, CA-07, CA-08, CA-09, CA-11, CA-12, CA-14, CA-15, CA-19 y CA-35/CA-36 de esas pantallas; real del cliente (CA-01, CA-03, CA-05, CA-06, CA-12, CA-17, CA-18, RNF-03 y RNF-11) y del operador hasta el inicio del backoffice (CA-02).
- **004b — `core-bancario`:** Application, Infrastructure y Api de RF-16 (CA-29, CL-20, `F004_RF16_*`); Keycloak.Tests `F004_RF16_EstadoDeAcceso_ReflejaElPrimerIngresoReal`.
- **004b — `core-bancario-web`:** Vitest de CA-22…CA-32 y de `acciones`/`esquemaDeRegistro`; simulado de CA-30 y CA-35/CA-36 de las pantallas nuevas; real del operador completo (CA-33).

### Dónde va cada CA (RNF-14)

| CA | Prueba(s) | Dónde |
|---|---|---|
| CA-01 | `F004_CA01_ClienteIniciaSesionConOtp_VeMisCuentasYAlSalirPideCredenciales` · `F004_CA01_SesionPorElBff_MisCuentasYAlSalirKeycloakSinSesiones` | Web real · Keycloak.Tests |
| CA-02 | `F004_CA02_OperadorIniciaSesion_LlegaAlInicioDelBackoffice` · `F004_CA02_OperadorPorElBff_SesionConRolOperador` | Web real · Keycloak.Tests |
| CA-03 | `F004_CA03_PrimerIngreso_CambiaClaveInscribeOtpYLlegaAMisCuentas` | Web real (primera parte del recorrido del cliente) |
| CA-04 | `F004_CA04_RegresoConStateDistinto_NoCreaSesion`, `F004_CA04_RegresoConIssDeOtroEmisor_NoCreaSesion`, `F004_CA04_RegresoConError_NoCreaSesion` · `F004_CA04_AvisoInicioFallido_MuestraMensajeYEmpezarDeNuevo` | Keycloak.Tests · Web simulado |
| CA-05 | `F004_CA05_ConSesion_NingunTokenLegibleDesdeLaPagina` · `F004_CA05_CookieDeSesion_EsHttpOnlyYNoContieneTokens` · `F004_RNF01_*` | Web real · Keycloak.Tests · Bff.Tests |
| CA-06 | `F004_CA06_PeticionSinEncabezadoAntiCsrf_SeRechazaSinLlegarALaApi` (teoría: `POST /api/…`, `POST /bff/salir`, `POST /bff/actividad`, `GET /bff/sesion`), `F004_CA06_SecFetchSiteSameSiteOCrossSite_SeRechaza`, `F004_CA06_CookieDeSesion_EsSameSiteStrictYHttpOnly` · `F004_CA06_OtroOrigenDelMismoSitio_NoPuedeCambiarDatos` | Bff.Tests · Web real |
| CA-07 | `F004_CA07_SinActividadDeliberada_VenceAunqueHayaPeticionesAutomaticas`, `F004_CA07_ActividadALosCuatroMinutos_SigueAbiertaALosSeis` · `F004_RN25_PeticionesAutomaticasPorElBff_NoMantienenLaSesion`, `F004_RN25_ActividadDeliberada_MantieneTambienLaSesionDeKeycloak` · `F004_RN25_MoverElRatonYScroll_NoCuentan`, `F004_RN25_ClicTeclaYCambioDePantalla_Cuentan`, `F004_RN25_InteraccionesSeguidas_UnLatidoCada15Segundos` · `F004_CA07_RefrescosAutomaticos_NoEnvianLatidos` | Bff.Tests · Keycloak.Tests · Vitest · Web simulado |
| CA-08 | `F004_CA08_FaltanSesentaSegundos_AvisoAccesibleManejableConTeclado`, `F004_CA08_SeguirConectado_EnviaLatidoYCierraElAviso`, `F004_CA08_SinRespuesta_CierraConMensajeDeInactividad` · `F004_CL06_EvaluarReloj_*` | Web simulado (`page.clock`) · Vitest |
| CA-09 | `F004_CA09_VidaMaxima_VenceAunqueHayaActividad` · `F004_CA09_EvaluarReloj_AvisoDeVidaMaximaSinAlargar` · `F004_CA09_A29Minutos_AvisoSinSeguirConectadoYA30Cierra` | Bff.Tests · Vitest · Web simulado |
| CA-10 | `F004_CA10_TrasCerrarPorInactividad_EntrarPideUsuarioClaveYCodigo`, `F004_CL09_IniciarConSesionDeKeycloakViva_PideCredenciales` | Keycloak.Tests (tiempos cortos) |
| CA-11 | `F004_CA11_DosPestanas_ActividadEnUnaMantieneLaOtra`, `F004_CA11_CerrarEnUna_LaOtraVaAlInicioEnMenosDeCincoSegundos` | Web simulado |
| CA-12 | `F004_CA12_TrasCerrarSesion_AtrasNoMuestraDatos` · `F004_CA12_TrasCerrarSesion_AtrasNoMuestraSaldos` | Web simulado · Web real |
| CA-13 | `F004_CL08_RutaDeRegreso_Normalizar` (teoría con `https://x.com`, `//x.com`, `/\x.com`, `javascript:…`, `/backoffice/…` en clientes, `/mis-cuentas/…` en empleados, `/bff/…`, `/api/…`, más de 2048 caracteres, válidas) · `F004_CA13_EnlaceProfundo_TrasIniciarSesionLlegaALaRuta`, `F004_CA13_RegresoExternoODelBackoffice_LlegaAMisCuentas` | Bff.Tests · Keycloak.Tests |
| CA-14 | `F004_CA14_ConSesionDeCliente_IniciarEnEmpleados_RedirigeAOtraSesionAbierta`, `F004_RN26_RegresoDeOtraSesion_CierraLaAnteriorEnKeycloak` · `F004_CA14_SesionDeClienteEnBackoffice_OfreceCerrarPrimero` | Keycloak.Tests · Web simulado |
| CA-15 | `F004_CA15_SesionSinRol_PantallaSinPermisoSinDatos`, `F004_CL04_Api403_PantallaSinPermiso` · `F004_CL04_LectorDeRol_*` | Web simulado y Vitest · Bff.Tests |
| CA-16 | `F004_CA16_OperadorDeshabilita_LaSiguienteActividadCierraComoRevocadaYNoPuedeEntrar` · `F004_CL12_RenovacionRechazada_CierraComoRevocada` · `F004_CL12_SesionRevocada_VaAlInicioConMensajeGenerico` | Keycloak.Tests · Bff.Tests · Vitest |
| CA-17 | `F004_CA17_DocumentoConPuntos_EntraComoEseCliente` | Web real (en el recorrido del cliente) |
| CA-18 | `F004_CA18_MisCuentas_ActivaCopYCerradaUsdConFormatoEsCo`, `F004_CA18_MisCuentas_MuestraIndicadorMientrasCarga`, `F004_CA18_MisCuentas_SinCuentas_EstadoVacio`, `F004_CA18_ErrorDeLaApi_Reintentar` | Vitest + MSW |
| CA-19 | `F004_CA19_FormatearDinero_UsdLargoExacto`, `F004_CA19_FormatearDinero_CopSinDecimales`, `F004_CA19_FormatearDinero_MontoNoDecimal_Lanza` · `F004_CA19_SaldoLargo_ExactoEnElNavegador` | Vitest · Web simulado |
| CA-20 | `F004_CA20_DetalleDeCuentaPropia_MuestraNumeroMonedaEstadoSaldoYFechas`, `F004_CA20_CuentaAjenaEInexistente_MismaPantallaNoEncontrada` | Vitest + MSW |
| CA-21 | `F004_CA21_CambiarClavePorElBff_VuelveConAvisoYSoloSirveLaNueva` · `F004_CA21_AvisoClaveActualizada_MuestraMensajeYLimpiaLaUrl` | Keycloak.Tests · Vitest |
| CA-22 | `F004_CA22_BuscarConPuntos_MuestraLaFichaSinDocumentoEnLaUrl`, `F004_CL22_DocumentoNoRegistrado_OfreceRegistrarConLosDatos` · `F004_RNF04_*` | Vitest · Bff.Tests |
| CA-23 | `F004_CA23_TelefonoSinCodigoDePais_ErrorJuntoAlCampo`, `F004_CA23_Error400DeLaApi_JuntoASuCampo`, `F004_CL23_DocumentoDuplicado_OfreceIrALaFicha`, `F004_CA23_DatosValidos_AbreLaFichaNueva`, `F004_CA23_EsquemaDeRegistro_*` | Vitest |
| CA-24 | `F004_CA24_DobleClicEnAbrirCuenta_UnaSolaPeticionYCuentaUsdEnCero` | Vitest + MSW (cuenta las peticiones) |
| CA-25 | `F004_CA25_AccionesDeCuenta_SegunEstado` (teoría) · `F004_CA25_Ficha_ActivaBloqueadaYCerrada_OfrecenSusAcciones` | Vitest |
| CA-26 | `F004_CA26_CerrarConSaldo_MensajeDeNegocioYSigueActiva` | Vitest |
| CA-27 | `F004_CA27_Conflicto409_MensajeYLaFichaSeRecarga` | Vitest |
| CA-28 | `F004_CA28_AccionesDeAcceso_SegunEstado` (teoría) · `F004_CA28_Ficha_MuestraEstadoYAccionesDelAcceso` | Vitest |
| CA-29 | `F004_CA29_EstadoDeAcceso_SinAccesoHabilitadoYDeshabilitado`, `F004_CA29_ClienteInexistente_LanzaNoEncontradoSinLlamarAlProveedor`, `F004_RF16_PrimerIngresoPendiente_SoloSiHabilitado` · `F004_RF16_UsuarioConAccionesObligatorias_PrimerIngresoPendiente` · `F004_CA29_OperadorConsultaElEstado_Responde200`, `F004_CA29_ClienteInexistente_Responde404`, `F004_CA29_RolCliente_Responde403`, fila del contrato · `F004_RF16_EstadoDeAcceso_ReflejaElPrimerIngresoReal` | Application · Infrastructure · Api · Keycloak.Tests |
| CA-30 | `F004_CA30_DialogoDeClave_NoSeCierraConEscNiClicFuera`, `F004_CA30_YaLaEntregue_LaClaveDesapareceDelDomYDeLaCache` · `F004_CA30_ClaveTemporal_NoQuedaEnUrlNiAlmacenamiento` | Vitest · Web simulado |
| CA-31 | `F004_CA31_Restablecer_ConfirmacionExplicaConsecuenciasYDialogoDiceRestablecido`, `F004_CL21_BotonHabilitarPeroApiRestablece_DialogoDiceRestablecido` | Vitest |
| CA-32 | `F004_CA32_EstadoNoDisponible_FichaVisibleYAccionesDesactivadas`, `F004_CL19_FallaAlHabilitar_DiceQuePuedeReintentarSinRiesgo` · `F004_CL20_IdentidadSinRespuesta_EstadoResponde503`, `F004_CL20_ErrorDeIdentidad_EstadoResponde502` | Vitest · Api.Tests |
| CA-33 | `F004_CA33_OperadorBuscaYBloquea_LaFichaMuestraBloqueada` | Web real (recorrido del operador) |
| CA-34 | `F004_CA34_CadaCodigoDelCatalogo_TieneMensaje`, `F004_CA34_CodigoDesconocidoO500_MensajeGenericoConTraceId`, `F004_RNF12_InterpretarError_NuncaUsaDetail` · `F004_RNF12_ContratoOpenApi_EnumeraTodosLosCodigosDeError` | Vitest · Api.Tests |
| CA-35 | `F004_CA35_CadaPantalla_SinFallosAA` (una por pantalla y estado), `F004_CA35_InicioDeSesionAvisoYClave_SoloConTeclado` · recorrido real del cliente con teclado y axe en el login de Keycloak | Web simulado · Web real |
| CA-36 | `F004_CA36_AreaDelCliente360px_SinScrollHorizontal`, `F004_CA36_Backoffice1280px_SinScrollHorizontal` | Web simulado |

**Recorridos reales (RNF-15).** Los dos corren en el CI web contra el compose completo:

- **Cliente** (004a), solo con teclado:
  1. *Setup:* el operador inicia sesión (inscribe su OTP) y, por `page.request` a `/api` con su sesión, registra un cliente CC 1234567 + n, le abre una cuenta COP y habilita su acceso (clave temporal);
  2. el operador cierra sesión;
  3. el cliente, en otro contexto, entra por `/` escribiendo el documento con puntos (CA-17), cambia la clave e inscribe el OTP (CA-03) y ve "Mis cuentas" (CA-01, CA-18, RNF-11);
  4. comprobaciones de CA-05 y RNF-03;
  5. el cliente cierra sesión, pulsa atrás (CA-12) y vuelve a entrar con la clave nueva y el código: Keycloak los pide (CA-01).
- **Operador** (004b): el operador inicia sesión, busca `1.234.567`, abre la ficha, bloquea la cuenta Activa y la ficha la muestra Bloqueada (CA-33).

## 8. Decisiones

ADRs nuevas (todas en `Propuesta`):

- **ADR-0020: BFF propio en .NET para la sesión del navegador (D-12).** `CoreBancario.Bff` con `AddOpenIdConnect` + `AddCookie` + YARP. Tokens en un almacén del servidor y cliente de Keycloak confidencial. CSRF con `SameSite=Strict` + `X-CSRF` + `Sec-Fetch-Site`. Mismo origen, sin CORS, y la API ya no se expone al navegador. Duende.BFF se descarta por licencia.
- **ADR-0021: La inactividad la mide el BFF con la actividad deliberada.** Latido `POST /bff/actividad` (como mucho uno cada 15 s) que renueva tokens; el *proxy* nunca cuenta; `prompt=login`; cierre por el canal trasero; `BroadcastChannel` entre pestañas.
- **ADR-0022: Cómo se construye el frontend (D-09).** Vite 8, React 19, TS 5.9 estricto, React Router 8, TanStack Query 5, RHF + Zod, openapi-typescript + openapi-fetch, React Aria Components, Vitest + MSW, Playwright + axe y `Intl.NumberFormat` con texto.

Decisiones del plan sin ADR (locales y fáciles de cambiar):

1. **RF-16 es un endpoint aparte** y no un campo de la ficha (CL-20), y reutiliza `LocalizadorDeAcceso` sin reconciliar (es una consulta).
2. **"Primer ingreso pendiente" sí entra**, leído de `requiredActions`. `UsuarioDeIdentidad` lo gana como parámetro opcional, para no tocar las construcciones existentes (Open/Closed sin ceremonia).
3. **El *enum* de códigos de error en el OpenAPI**, generado por reflexión desde `CodigosDeError`. Así el compilador de la SPA garantiza CA-34, y la regla de ADR-0011 de no renombrar códigos se ve en el diff del contrato.
4. **`rol` en `SesionDto`**, calculado por el BFF solo para la interfaz (CL-04 sin un endpoint `/yo` nuevo, YAGNI). La API sigue siendo la autoridad.
5. **El registro del cliente se abre dentro de la búsqueda**, para no pasar el documento por una ruta ni por `history.state` (RNF-04).
6. **Un solo compose en este repositorio**, con perfiles. El backend ya tiene la infraestructura, y el CI web clona este repo. Resuelve la consecuencia pendiente de ADR-0005.
7. **`ResponseMode = query` y PAR desactivado**, para que el regreso sea un `GET` simple y las pruebas puedan leer la petición de autorización. El código viaja en la URL del regreso, pero es de un solo uso, está atado a PKCE y exige el secreto: no es un token (RNF-04). PAR queda como mejora (ADR-0020).
8. **Un 403 del anti-CSRF y no un 400**: la petición está bien formada, pero no es de confianza.
9. **El *proxy* responde `no-store`** a todo, aunque la API no lo pida, por CL-11.
10. **Recorridos reales en Chromium solamente** (RNF-10 lo permite). La matriz de navegadores se documenta.

## 9. Trazabilidad

| ID de la spec | Sección del plan | Bloque | Tareas |
|---|---|---|---|
| RF-01 | 3.4 (`esquemaDeRegistro`, `BuscarCliente`), 5 (CL-23) | 004b | T25 |
| RF-02 | 3.4 (`AbrirCuenta`, `Boton`), 5 (CL-15) | 004b | T26 |
| RF-03 | 3.4 (`accionesDeCuenta`), 5 (CL-16, CL-18) | 004b | T26 |
| RF-05 (cliente) | 3.4 (`MisCuentas`, `DetalleDeCuenta`, `formatearDinero`) | 004a | T13, T17 |
| RF-12 | 3.4 (`BuscarCliente`), 5 (CL-22) | 004b | T25 |
| RF-13 | 3.4 (`AccesoDigital`, `DialogoDeClaveTemporal`), 5 (CL-17, CL-19, CL-21) | 004b | T27 |
| RF-14 | 3.1 (BFF), 3.3, 3.4 (`ProveedorDeSesion`); ADR-0020 | 004a | T01, T06, T07, T09, T15, T16 |
| RF-15 | 3.1 (`/bff/cambiar-clave`), 3.3 (`EventosDeOidc`), 3.4 (`BotonCambiarClave`) | 004a | T07, T09, T17 |
| RF-16 | 3.1 (API), 3.3 (Application/Infra) | 004b | T21, T22, T23, T24, T27 |
| RN-14 | 3.4 (`accionesDeCuenta`) | 004b | T26 |
| RN-19 | 3.1 (rol y 403), 3.3 (`LectorDeRol`, dos esquemas); ADR-0020 | 004a | T04, T07, T16 |
| RN-20 | 3.4 (confirmación de "Restablecer") | 004b | T27 |
| RN-21, RN-24 | Las pantallas de Keycloak, sin cambios; recorridos reales | 004a | T01, T07 |
| RN-22 | 3.3 (`OpcionesDeArea`, `PoliticaDeSesion`), 7 (prueba de configuración); ADR-0021 | 004a | T03, T04, T06, T15 |
| RN-25 | 3.3 (`ServicioDeSesion`), 3.4 (`registroDeActividad`); ADR-0021 | 004a | T06, T09, T15, T16 |
| RN-26 | 3.3 (`EventosDeOidc.TicketReceived`), 3.1 (`otra-sesion-abierta`); ADR-0020 | 004a | T07, T09, T16 |
| RNF-01 | 6; ADR-0020 | 004a | T01, T06, T07, T09, T10 |
| RNF-02 | 6; ADR-0020 | 004a | T08, T09, T10 |
| RNF-03 | 6 (CSP) | 004a | T18 |
| RNF-04 | 6, 8 (decisión 5); pregunta abierta 2 | 004a, 004b | T03, T18, T25, T27 |
| RNF-05 | 3.3 (`EventosDeOidc`), 6 | 004a | T01, T07 |
| RNF-06 | 6; ADR-0021 | 004a | T04, T06, T16 |
| RNF-07 | 3.4 (`canalDeSesion`); ADR-0021 | 004a | T15, T16 |
| RNF-08 | 6, 7 | 004a, 004b | T14, T16, T17, T25…T27 |
| RNF-09 | 6 | 004a, 004b | T16, T17, T25…T27 |
| RNF-10 | 6, 8 (decisión 10) | 004a | T19 |
| RNF-11 | 6 | 004a | T17 |
| RNF-12 | 3.1 (*enums*), 3.4 (`interpretarError`) | 004a | T02, T08, T13 |
| RNF-13 | 1.2, 3.4, 6 | 004a, 004b | T12, T24 |
| RNF-14 | 7 | 004a, 004b | Todas (flujo `/sdd-build`) |
| RNF-15 | 7 (recorridos reales) | 004a, 004b | T19, T28 |
| RNF-16 | 4 (compose) | 004a | T01, T02, T10, T18, T19 |
| CL-01 | 5 | 004a | T07, T16 |
| CL-02 | 5 | 004a | T07 |
| CL-03 | 5 | 004a | T01, T07 |
| CL-04 | 5 | 004a | T04, T16 |
| CL-05 | 5 | 004a | T05, T09, T16 |
| CL-06 | 5 | 004a | T15, T16 |
| CL-07 | 5 | 004a | T04, T15, T16 |
| CL-08 | 5 | 004a | T04, T07, T09 |
| CL-09 | 5; ADR-0021 | 004a | T06, T07 |
| CL-10 | 5 | 004a | T07, T09, T16 |
| CL-11 | 5 | 004a | T08, T16, T18 |
| CL-12 | 5 | 004a | T05, T06, T16 |
| CL-13 | 5 | 004a | T13, T16 |
| CL-14 | 5 | 004a | T14, T17 |
| CL-15 | 5 | 004b | T14, T26 |
| CL-16 | 5 | 004b | T26 |
| CL-17 | 5 | 004b | T27 |
| CL-18 | 5 | 004b | T26 |
| CL-19 | 5 | 004b | T27 |
| CL-20 | 3.1, 5 | 004b | T21, T23, T27 |
| CL-21 | 5 | 004b | T27 |
| CL-22 | 5 | 004b | T25 |
| CL-23 | 5 | 004b | T25 |
| CL-24 | 5 | 004a, 004b | T13, T14, T17, T25…T27 |
| CA-01 | 7 | 004a | T01, T06, T07, T09, T16, T17, T19 |
| CA-02 | 7 | 004a | T07, T16, T17 |
| CA-03 | 7 | 004a | T01, T19 |
| CA-04 | 5 (CL-01), 7 | 004a | T07, T16 |
| CA-05 | 6 (RNF-01), 7 | 004a | T06, T09 |
| CA-06 | 6 (RNF-02), 7 | 004a | T08, T09, T19 |
| CA-07 | 6 (RNF-06), 7 | 004a | T04, T06, T09, T15, T16 |
| CA-08 | 5 (CL-06), 7 | 004a | T15, T16 |
| CA-09 | 5 (CL-07), 7 | 004a | T04, T15, T16 |
| CA-10 | 5 (CL-09), 7 | 004a | T06, T07 |
| CA-11 | 6 (RNF-07), 7 | 004a | T15, T16 |
| CA-12 | 5 (CL-11), 7 | 004a | T08, T16 |
| CA-13 | 5 (CL-08), 7 | 004a | T04, T07, T09 |
| CA-14 | 5 (CL-10), 7 | 004a | T07, T09, T16 |
| CA-15 | 5 (CL-04), 7 | 004a | T04, T16 |
| CA-16 | 5 (CL-12), 7 | 004a | T05, T06, T16 |
| CA-17 | 7 | 004a | T19 (el tema de la 003 ya normaliza) |
| CA-18 | 7 | 004a | T13, T14, T17 |
| CA-19 | 5 (CL-14), 7 | 004a | T14, T17 |
| CA-20 | 7 | 004a | T17 |
| CA-21 | 3.1, 7 | 004a | T07, T09, T17 |
| CA-22 | 5 (CL-22), 6 (RNF-04), 7 | 004b | T25 |
| CA-23 | 5 (CL-23), 7 | 004b | T25 |
| CA-24 | 5 (CL-15), 7 | 004b | T26 |
| CA-25 | 7 | 004b | T26 |
| CA-26 | 5 (CL-18), 7 | 004b | T26 |
| CA-27 | 5 (CL-16), 7 | 004b | T26 |
| CA-28 | 3.4 (`accionesDeAcceso`), 7 | 004b | T27 |
| CA-29 | 3.1, 3.3, 7 | 004b | T21, T22, T23 |
| CA-30 | 5 (CL-17), 7 | 004b | T27 |
| CA-31 | 5 (CL-21), 7 | 004b | T27 |
| CA-32 | 5 (CL-19, CL-20), 7 | 004b | T23, T27 |
| CA-33 | 7 | 004b | T25, T26, T28 |
| CA-34 | 6 (RNF-12), 7 | 004a | T02, T13 |
| CA-35 | 6 (RNF-08), 7 | 004a, 004b | T14, T16, T17, T25…T27 |
| CA-36 | 6 (RNF-09), 7 | 004a, 004b | T16, T17, T25…T27 |
| D-09 | ADR-0022 | 004a | T12, T20 |
| D-12 | ADR-0020, ADR-0021 | 004a | T03…T11 |

## 10. Riesgos y preguntas abiertas

### Preguntas abiertas para el autor

Se plantean con conceptos, opciones y recomendación en la respuesta del planner. El plan ya está escrito con la opción recomendada de cada una:

1. **Dónde guarda el BFF las sesiones:** en memoria (recomendada) o en PostgreSQL.
2. **El número de documento en la URL de la petición de búsqueda:** mantener `GET` y controlar los logs (recomendada), o un `POST /clientes/busqueda` nuevo en la API.
3. **Comprobación de ADR-0021:** que el autor explique por qué el reloj de Keycloak no sirve para RN-25 y qué protege `prompt=login`.

### Límites conocidos (propuestos para aceptar)

- **Doble envío entre pestañas o por reintento de red** (CL-15). Lo cierra la idempotencia de S4 y S5.
- **Sesiones en memoria:** un reinicio del BFF cierra todas las sesiones, y no admite varias instancias (ADR-0020).
- **Revocación de hasta 270 s** cuando el operador deshabilita a un cliente que **no** está haciendo nada (CL-12). Si el cliente actúa, se nota en el siguiente latido.
- **Sin CSP en el servidor de desarrollo de Vite** (HMR). Se verifica en compose y en el CI.

### Riesgos

1. **El cierre por el canal trasero con `refresh_token`.** La documentación de Keycloak 26 lo describe como el *logout* "heredado" con autenticación del cliente. Una fuente externa dice que cierra la sesión del **cliente** y no la de **todo el usuario**. Con un solo cliente por realm debería dar igual, pero el Build lo verifica en `F004_CA01_…KeycloakSinSesiones` (la Admin API devuelve 0 sesiones del usuario). Si quedara una sesión SSO viva, `prompt=login` mantiene CL-09 y se documenta.
2. **`KC_HOSTNAME` + `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`.** Hay un caso reportado de esquema incorrecto en el descubrimiento. Hay que verificar en el Build que, desde `keycloak:8080`, el emisor sigue siendo `http://localhost:8080/realms/…`, que el `token_endpoint` usa `keycloak:8080` y que la cuenta de servicio de la API (003b) sigue funcionando por `keycloak:8080`. Plan B: la API y el BFF con `network_mode: host` en compose (solo Linux), o un alias `host.docker.internal` documentado.
   - **Se materializó en el cierre (fix tras el PR #2 del web).** Keycloak publica `end_session_endpoint` con la dirección **frontal** (`localhost:8080`), porque lo trata como endpoint de navegador, aunque `token_endpoint` salga por `keycloak:8080`. Dentro del contenedor del BFF, `localhost` es el propio BFF, así que el cierre respondía 404 y la sesión SSO seguía viva. El BFF deriva ahora la dirección de cierre de `token_endpoint` (`…/protocol/openid-connect/token` → `…/logout`) y solo usa el `end_session_endpoint` publicado si la ruta no es la de Keycloak. Pruebas: Bff.Tests `F004_RF14_CerrarSesionAsync_EnviaClienteSecretoYRefreshTokenAlEndSessionPorElCanalTrasero` y `F004_RF14_CerrarSesionAsync_TokenEndpointSinRutaDeKeycloak_UsaElEndSessionPublicado`. Keycloak.Tests no lo veía porque corre fuera de Docker.
3. **`kc_action_status` en el regreso de "Cambiar clave".** Se asume que Keycloak 26.3.3 lo envía (`success` o `cancelled`). Si no, el BFF muestra siempre `clave-actualizada`, y la prueba de CA-21 comprueba de todas formas que solo sirve la clave nueva.
4. **ICU y el formato es-CO.** El símbolo (`$`, `US$`) y el espacio duro pueden variar entre Node, Chromium y Safari. Las pruebas Vitest corren en Node y las de CA-19 también en Chromium. Si difieren, se normaliza el espacio en las pruebas y no en el código.
5. **TypeScript 5.9 por compatibilidad con `openapi-typescript`** (ADR-0022). Riesgo de quedarse atrás. Hay que revisarlo en cada `chore` de dependencias.
6. **`WebApplicationFactory` de dos hosts en Keycloak.Tests** (el BFF hace de *proxy* hacia el `TestServer` de la API y el navegador simulado va por puerto). Es infraestructura de pruebas nueva y delicada. Si se complica, el BFF de esas pruebas puede reenviar a una API real con Kestrel en un puerto libre.
7. **Tiempo del CI.**
   - En `core-bancario`, Keycloak.Tests gana unas 15 pruebas, varias con esperas de 40 s. Se agrupan las esperas, y si molesta se mueven a un job paralelo (como prevé ADR-0018);
   - en el web, `e2e-real` construye cuatro imágenes y arranca Keycloak (unos 3 min).
8. **Cambiar el realm rompe los entornos locales actuales** (riesgo 10 de la 003). El README de T11 lo destaca: hay que borrar el volumen y poner los dos secretos nuevos.
9. **Coordinación entre dos repositorios.** El PR web de cada bloque depende del de backend ya mergeado (`e2e-real` usa `main`). Si hay que iterar, el job acepta `CORE_BANCARIO_REF` para probar contra una rama.

### Para estudiar antes o durante el Build

- **Patrón BFF** y por qué un cliente público con redirección a tu origen anula la protección frente a XSS.
- **CSRF a fondo:** `SameSite` (`Strict`, `Lax`, `None`), sitio frente a origen, *preflight* de CORS y `Sec-Fetch-Site`.
- **El manejador OIDC de ASP.NET Core:** `state`, cookie de correlación, `nonce`, `SaveTokens`, `ITicketStore` y los eventos (`OnRedirectToIdentityProvider`, `OnRemoteFailure`).
- **Rotación de refresh tokens** y por qué dos renovaciones simultáneas fallan (de ahí el candado).
- **Proxy inverso con YARP:** transformaciones de petición y de respuesta.
- **React:** componentes, hooks y contexto; **TanStack Query** (consultas frente a mutaciones, invalidación, `gcTime`).
- **Accesibilidad:** roles ARIA de diálogo (`dialog` frente a `alertdialog`), gestión del foco, WCAG 2.2.1 (tiempo ajustable).
- **Intl.NumberFormat v3** y por qué `0.1 + 0.2 ≠ 0.3` en `number`.
- **bfcache** (*back/forward cache*) del navegador y el evento `pageshow`.
