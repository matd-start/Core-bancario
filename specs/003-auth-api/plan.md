# Plan — 003 Autenticación y autorización en la API

Estado: Aprobado (2026-10-13)
Spec: [spec.md](spec.md)

## 1. Resumen del diseño

**Keycloak emite las credenciales y la API solo las valida y gestiona accesos** (ADR-0014). Hay dos realms, `clientes` y `empleados`, con su configuración versionada en `infra/keycloak/` y un tema de login propio. Iniciar y cerrar sesión, el segundo factor, el bloqueo, las claves temporales y el cambio de clave (RF-14, RF-15, RN-21…RN-24) son **configuración de Keycloak**, no código de la API.

La API valida los JWT sin preguntarle a Keycloak en cada petición. Usa dos esquemas `JwtBearer`, uno por realm, y un selector que elige el esquema por el emisor (`iss`). Fija RS256, una vida de 270 s y una tolerancia de reloj de 30 s: 5 min en total (ADR-0015). El rol sale del realm que firmó el token **y** de su `realm_access.roles`, así que un token de `clientes` nunca puede ser Operador.

El cliente se identifica por una **tabla de vínculo `sub` ↔ `ClienteId`**. "Mis cuentas" filtra por dueño **en la misma consulta**, así que una cuenta ajena y una inexistente siguen el mismo camino y dan el mismo 404 (ADR-0016).

Gestionar el acceso (RF-13) sigue **"intención primero"** (ADR-0017):

1. Se valida la petición.
2. Se confirma en la base una solicitud.
3. Se llama a Keycloak con una credencial de servicio de mínimo privilegio.
4. Se escribe un resultado en otra fila. Las filas nunca se editan.

Si Keycloak no responde, la solicitud queda pendiente y el reintento del operador la cierra.

Las pruebas van en cuatro niveles (ADR-0018). Solo la última batería arranca Keycloak.

### 1.1 Build en dos bloques (decisión del autor, 2026-10-13)

Hay una sola spec y un solo plan. El Build se hace en **dos bloques secuenciales**, cada uno con sus pruebas en rojo, su `/sdd-review` y su PR. Cada bloque compila, pasa sus pruebas y se puede mergear por sí solo. La lista exacta de tareas está en [tasks.md](tasks.md).

| Bloque | Qué entrega | CA que cubre | Tareas |
|---|---|---|---|
| **003a — Proteger la API** | Realms y temas de Keycloak (incluido el aviso del número de documento), validación de tokens, roles, protección de la 002 con el registro del operador, tabla de vínculo y "mis cuentas", OpenAPI generado y versionado, login de punta a punta | CA-06…CA-22 | T01–T14 |
| **003b — Gestionar el acceso** | RF-13: dominio y registro de accesos, intención primero y reconciliación, adaptador de administración de Keycloak (cuenta de servicio y marca), errores 422/502/503 y endpoints de habilitar y deshabilitar | CA-01…CA-05 | T15–T27 |

**Cómo se crean los usuarios en las pruebas de punta a punta de 003a, sin RF-13.** El fixture de `CoreBancario.Keycloak.Tests` usa la **Admin REST API con el administrador de `master`** del contenedor (`KC_BOOTSTRAP_ADMIN_*`):

1. Registra al `Cliente` por HTTP en la API, con un token de operador real obtenido por login, como en el resto de pruebas.
2. Crea en `clientes` un usuario con `username = AccesoDigital.NombreDeUsuarioDe(documento)`, una clave temporal y las acciones `UPDATE_PASSWORD` y `CONFIGURE_TOTP`. Es lo mismo que hará `CrearAsync` en 003b, de modo que CA-06 y CA-07 prueban el primer ingreso real.
3. Inserta el vínculo `accesos_digitales` (`ClienteId`, `sub`) **directamente en la base** con el `DbContext`.
4. Para CA-08 deshabilita un usuario con la misma Admin API (`enabled: false`).
5. Crea al operador en `empleados` con el rol `operador`.

En 003b, la prueba del flujo completo (`F003_RNF08_FlujoCompleto_…`) sustituye los pasos 2 y 3 por el endpoint de habilitar.

**Ramas (recomendación; no hace falta preguntarlo).** CLAUDE.md pide ramas cortas, una por trabajo, que se borran tras el merge. Reutilizar `feature/003-auth-api` para un segundo PR después de mergear el primero choca con esa regla. Además, el repositorio tiene activado "Automatically delete head branches", así que GitHub borra la rama al hacer el merge (con "Create a merge commit"). Por eso, **dos ramas**:

- **`feature/003a-proteger-api`**, que nace de la actual `feature/003-auth-api` y lleva la spec, el plan, las ADRs y el bloque 003a. Basta con renombrar la rama o crear una nueva desde ella.
- **`feature/003b-gestionar-acceso`**, que nace de `main` **después** de mergear 003a.

Los commits siguen siendo `tipo(003): …`.

## 2. Impacto por capa

| Capa | Qué cambia | Archivos |
|---|---|---|
| Domain | Nuevo `AccesosDigitales/`: `AccesoDigital` (vínculo y nombre de usuario), `SolicitudDeAcceso`, `ResultadoDeSolicitud`, tres enums y `AccesoNoHabilitadoException` | `src/CoreBancario.Domain/AccesosDigitales/*.cs` |
| Application | Nuevo `AccesosDigitales/`: puertos (`IProveedorDeIdentidad`, `IAccesoDigitalRepositorio`, `IRegistroDeAccesos`, `IGeneradorDeClaveTemporal`), `ClaveTemporal`, dos excepciones del proveedor, DTOs, `LocalizadorDeAcceso`, `ReconciliadorDeSolicitudes` y dos handlers. En `Cuentas/`: puerto de lectura `ICuentasDelTitular` y dos handlers de "mis cuentas". `AddAplicacion` registra lo nuevo | `src/CoreBancario.Application/AccesosDigitales/*.cs`, `src/CoreBancario.Application/Cuentas/{ICuentasDelTitular,ListarMisCuentasConsulta,ListarMisCuentasHandler,ObtenerMiCuentaConsulta,ObtenerMiCuentaHandler}.cs`, `src/CoreBancario.Application/DependencyInjection.cs` |
| Infrastructure | Tres tablas nuevas en dos migraciones: `VinculoDeAccesoDigital` (003a) y `RegistroDeAccesos` (003b, con el trigger de inmutabilidad). Además: traducción de los índices nuevos en `GuardarCambiosAsync` (003b), tres repositorios, el generador de claves temporales y el adaptador de Keycloak (cliente HTTP de la Admin REST API con *client credentials*, 003b) | `src/CoreBancario.Infrastructure/Persistencia/**`, `src/CoreBancario.Infrastructure/AccesosDigitales/*.cs`, `src/CoreBancario.Infrastructure/Identidad/*.cs`, `src/CoreBancario.Infrastructure/DependencyInjection.cs`, `.csproj` |
| Configuración de Keycloak (repo) | Realms `clientes` y `empleados` (JSON de import), temas de login, servicio `keycloak` en `docker-compose.yml`, variables en `.env.example` | `infra/keycloak/realms/*.json`, `infra/keycloak/themes/**`, `docker-compose.yml`, `.env.example` |
| Api | Autenticación (dos esquemas + selector, eventos que registran el motivo y responden 401/403 con `ProblemDetails`, mapeo de roles), políticas `Cliente`/`Operador` con política por defecto "autenticado", filtro que registra el operador (RNF-05), endpoints de acceso digital y de "mis cuentas", errores nuevos en el catálogo y OpenAPI con esquema de seguridad y 401/403/500 | `src/CoreBancario.Api/Autenticacion/*.cs`, `src/CoreBancario.Api/AccesosDigitales/*.cs`, `src/CoreBancario.Api/Cuentas/EndpointsDeMisCuentas.cs`, `src/CoreBancario.Api/OpenApi/*.cs`, `src/CoreBancario.Api/Errores/{CodigosDeError,CatalogoDeErrores}.cs`, `src/CoreBancario.Api/{Clientes,Cuentas}/Endpoints*.cs`, `Program.cs`, `appsettings*.json`, `.csproj` |
| Tests | Fakes nuevos en Application.Tests; `ApiFixture` con emisores RSA propios; un **proyecto nuevo** `CoreBancario.Keycloak.Tests` (adaptador y punta a punta contra Keycloak real) | `tests/**` |

**Por qué un proyecto de tests más.** CLAUDE.md pide un proyecto de tests por capa. Las pruebas que necesitan Keycloak real tardan minutos en arrancar (unos 188 s el primer arranque) y comparten un solo contenedor. Si vivieran en Infrastructure.Tests o Api.Tests, todo `dotnet test` local esperaría a Keycloak. Separarlas deja rápidas las otras cuatro baterías. Es una desviación consciente y se anota en CLAUDE.md (tarea T14).

## 3. Contratos

### Endpoints

Todas las rutas exigen sesión (política por defecto "usuario autenticado"), salvo `/health` y `/openapi/*` en Development. Cada ruta sirve a **un solo rol** (RN-19). El `ClienteId` del cliente nunca va en la URL: sale del token.

| Método | Ruta | Rol | Request | Respuestas | Cubre |
|---|---|---|---|---|---|
| POST | `/clientes/{clienteId:guid}/acceso-digital/habilitar` | Operador | — | 200 `AccesoHabilitadoDto` (con `Cache-Control: no-store`) · 401 · 403 · 404 · 409 · 500 · 502 · 503 | RF-13, RN-20, CA-01, CA-02, CA-04, CA-05, CL-12, CL-13, CL-16 |
| POST | `/clientes/{clienteId:guid}/acceso-digital/deshabilitar` | Operador | — | 200 `RegistroDeAccesoDto` · 401 · 403 · 404 · 409 · 422 `acceso-no-habilitado` · 500 · 502 · 503 | RF-13, RN-20, CA-03, CA-04, CL-14, CL-15 |
| GET | `/mis-cuentas` | Cliente | — | 200 `CuentaDto[]` · 401 · 403 · 500 | RF-05, RN-12, CA-14, CA-17, CL-09, CL-18 |
| GET | `/mis-cuentas/{cuentaId:guid}` | Cliente | — | 200 `CuentaDto` · 401 · 403 · 404 · 500 | RF-05, RN-12, CA-15, CA-16, CL-17 |
| (las ocho de la 002) | sin cambios de ruta ni de cuerpo | Operador | igual | igual que en la 002 **+ 401 · 403 · 500** | CA-19, CA-20 |

- **Bloques:** `/mis-cuentas`, `/mis-cuentas/{id}` y la protección de la 002 llegan en **003a**; `…/acceso-digital/habilitar` y `…/deshabilitar`, en **003b**.
- **No hay endpoint de "restablecer".** CL-13 dice que habilitar un acceso ya habilitado es un restablecimiento, así que una sola operación cubre las dos acciones. La respuesta dice cuál se aplicó (`accion`).
- **Habilitar y deshabilitar son acciones con nombre (`POST …/habilitar`)**, como los cambios de estado de la 002, y no un `PUT` del estado deseado: la acción que queda en el registro depende del estado actual (CL-13, CL-16).
- **`Cache-Control: no-store`** en la respuesta de habilitar: lleva la clave temporal, y ningún proxy ni el navegador deben guardarla (RNF-03).
- **409** en las dos operaciones de acceso: cubre la carrera improbable de dos operadores sobre el mismo cliente, que choca con un índice único (sección 5).
- **502/503** son nuevos (sección 5, catálogo): 502 significa que Keycloak respondió con un error y la operación no se completó; 503, que Keycloak no respondió y la operación **quedó pendiente de confirmar**.
- Iniciar sesión, cerrar sesión y cambiar la clave **no son endpoints de la API**. La SPA (004) los hace contra Keycloak: autorización con código + PKCE, `end_session_endpoint` y `kc_action=UPDATE_PASSWORD` (ADR-0014).

**Nombres de operación (`operationId`):** `HabilitarAccesoDigital`, `DeshabilitarAccesoDigital`, `ListarMisCuentas`, `ObtenerMiCuenta`. Los de la 002 no cambian.

**Cuerpos (JSON, camelCase):**

```json
// AccesoHabilitadoDto — 200 de habilitar
{ "solicitudId": "0199…", "clienteId": "0199…", "accion": "Habilitar", "claveTemporal": "Xq7m…(16)",
  "fecha": "2026-10-13T15:02:11.123456+00:00" }

// RegistroDeAccesoDto — 200 de deshabilitar
{ "solicitudId": "0199…", "clienteId": "0199…", "accion": "Deshabilitar", "fecha": "2026-10-13T15:04:00+00:00" }

// GET /mis-cuentas → arreglo de CuentaDto (el mismo DTO de la 002), ordenado por fechaApertura y después por id
[ { "id": "0199…", "numero": "1234567897", "clienteId": "0199…", "moneda": "COP", "estado": "Cerrada",
    "saldo": { "monto": "0", "moneda": "COP" }, "fechaApertura": "…", "fechaCierre": "…" } ]

// 401 (igual para todos los motivos: la respuesta no revela cuál fue, RNF-04)
// Cabecera: WWW-Authenticate: Bearer   (sin error ni error_description)
{ "title": "No autenticado", "status": 401, "codigo": "no-autenticado", "instance": "/mis-cuentas", "traceId": "00-…" }

// 403
{ "title": "Prohibido", "status": 403, "codigo": "prohibido", "instance": "/clientes", "traceId": "00-…" }
```

- `accion` toma los valores `Habilitar`, `Restablecer` o `Deshabilitar`.
- Se reutiliza `CuentaDto` de la 002 (decisión 8 de su plan). Incluye el `clienteId` propio del cliente, que no revela nada ajeno.

### Comandos, consultas y eventos

No hay eventos de dominio (YAGNI).

| Tipo | Nombre | Datos | Productor → consumidor | Cubre |
|---|---|---|---|---|
| Comando | `HabilitarAccesoComando` | `Guid ClienteId`, `string Operador` (el `sub` del token) | Endpoint → `HabilitarAccesoHandler` | RF-13, RN-20 |
| Comando | `DeshabilitarAccesoComando` | `Guid ClienteId`, `string Operador` | Endpoint → `DeshabilitarAccesoHandler` | RF-13, RN-20 |
| Consulta | `ListarMisCuentasConsulta` | `string Sub` | Endpoint → `ListarMisCuentasHandler` | RF-05, RN-12 |
| Consulta | `ObtenerMiCuentaConsulta` | `Guid CuentaId`, `string Sub` | Endpoint → `ObtenerMiCuentaHandler` | RF-05, RN-12 |

El `Operador` y el `Sub` los pone el endpoint a partir del token validado (`ClaimsPrincipal`). Nunca vienen del cuerpo, la ruta ni la query (RN-19). Se pasan dentro del comando en vez de leer `HttpContext` desde Application: el handler sigue sin depender de ASP.NET y se prueba con un string (KISS).

### Firmas públicas

Las pruebas comprueban **tipos, estados HTTP y `codigo`**, no textos. Los mensajes van en español.

```
src/CoreBancario.Domain/AccesosDigitales/          namespace CoreBancario.Domain.AccesosDigitales
  AccionDeAcceso.cs, EstadoDeAcceso.cs, ResultadoDeAcceso.cs, AccesoNoHabilitadoException.cs,
  AccesoDigital.cs, SolicitudDeAcceso.cs, ResultadoDeSolicitud.cs
src/CoreBancario.Application/AccesosDigitales/     namespace CoreBancario.Application.AccesosDigitales
  ClaveTemporal.cs, IGeneradorDeClaveTemporal.cs, IProveedorDeIdentidad.cs, UsuarioDeIdentidad.cs,
  ProveedorDeIdentidadSinRespuestaException.cs, ErrorDelProveedorDeIdentidadException.cs,
  IAccesoDigitalRepositorio.cs, IRegistroDeAccesos.cs, HabilitarAccesoComando.cs, DeshabilitarAccesoComando.cs,
  AccesoHabilitadoDto.cs, RegistroDeAccesoDto.cs, AccesoLocalizado.cs, LocalizadorDeAcceso.cs,
  ReconciliadorDeSolicitudes.cs, HabilitarAccesoHandler.cs, DeshabilitarAccesoHandler.cs
src/CoreBancario.Application/Cuentas/              (se añaden)
  ICuentasDelTitular.cs, ListarMisCuentasConsulta.cs, ListarMisCuentasHandler.cs,
  ObtenerMiCuentaConsulta.cs, ObtenerMiCuentaHandler.cs
src/CoreBancario.Infrastructure/
  Persistencia/Configuraciones/ AccesoDigitalConfiguracion.cs, SolicitudDeAccesoConfiguracion.cs,
                                ResultadoDeSolicitudConfiguracion.cs
  Persistencia/Repositorios/    AccesoDigitalRepositorio.cs, RegistroDeAccesos.cs, CuentasDelTitular.cs
  Persistencia/Migraciones/     (migraciones VinculoDeAccesoDigital, en 003a, y RegistroDeAccesos, en 003b)
  AccesosDigitales/             GeneradorAleatorioDeClaveTemporal.cs
  Identidad/                    OpcionesDeKeycloak.cs, ProveedorDeIdentidadKeycloak.cs, TokenDeServicioHandler.cs
src/CoreBancario.Api/
  Autenticacion/  EsquemasDeAutenticacion.cs, Politicas.cs, Roles.cs, OpcionesDeAutenticacion.cs,
                  ConfiguracionDeAutenticacion.cs, SelectorDeEsquema.cs, EventosDeJwt.cs, MotivoDeRechazo.cs,
                  TokenSinSujetoException.cs, ClaimsPrincipalExtensiones.cs, RegistroDeOperacionDelOperador.cs
  AccesosDigitales/ EndpointsDeAccesoDigital.cs
  Cuentas/        EndpointsDeMisCuentas.cs
  OpenApi/        EsquemaDeSeguridadEnOpenApi.cs, RequisitoDeSeguridadEnOpenApi.cs, DocumentacionDeErrores.cs
```

#### Domain

```csharp
// ───────────── src/CoreBancario.Domain/AccesosDigitales/AccionDeAcceso.cs ─────────────
namespace CoreBancario.Domain.AccesosDigitales;
/// <summary>RN-20. Lo que hizo el operador; se guarda como texto.</summary>
public enum AccionDeAcceso { Habilitar, Restablecer, Deshabilitar }

// ───────────── EstadoDeAcceso.cs ─────────────
/// <summary>Estado del acceso de un cliente tal como lo informa el sistema de identidad (única fuente de verdad, ADR-0017).</summary>
public enum EstadoDeAcceso { SinAcceso, Habilitado, Deshabilitado }

// ───────────── ResultadoDeAcceso.cs ─────────────
/// <summary>Cómo terminó una solicitud (ADR-0017). Una solicitud sin resultado está pendiente.</summary>
public enum ResultadoDeAcceso { Completado, Fallido }

// ───────────── AccesoNoHabilitadoException.cs ─────────────
/// <summary>CL-14: deshabilitar un acceso que no existe o que ya está deshabilitado. 422 acceso-no-habilitado.</summary>
public sealed class AccesoNoHabilitadoException : ReglaDeNegocioException
{
    public AccesoNoHabilitadoException(EstadoDeAcceso estadoActual);   // mensaje: "El cliente no tiene un acceso digital habilitado."
    public EstadoDeAcceso EstadoActual { get; }
}

// ───────────── AccesoDigital.cs ─────────────
/// <summary>
/// Vínculo entre un cliente y su usuario en el sistema de identidad (ADR-0016). El estado del acceso (habilitado,
/// segundo factor) NO vive aquí, sino en el sistema de identidad (ADR-0017). Un cliente tiene como máximo un vínculo.
/// </summary>
public sealed class AccesoDigital
{
    public Guid ClienteId { get; }
    /// <summary>Identificador del usuario en el sistema de identidad (claim "sub" del token).</summary>
    public string Sub { get; }
    public DateTimeOffset FechaDeVinculo { get; }

    private AccesoDigital(Guid clienteId, string sub, DateTimeOffset fechaDeVinculo);

    /// <exception cref="ArgumentException">clienteId es Guid.Empty, o sub es null, vacío, solo espacios o tiene más de 255 caracteres.</exception>
    public static AccesoDigital Vincular(Guid clienteId, string sub, DateTimeOffset fechaDeVinculo);

    /// <summary>
    /// Nombre de usuario con el que el cliente inicia sesión (sección 4 de la spec, RN-17): el código del tipo seguido
    /// del número ya normalizado, sin separador. CC 1.234.567 → "CC1234567"; PA ab-12345 → "PAAB12345".
    /// El prefijo tiene siempre dos letras, así que dos documentos distintos nunca dan el mismo nombre.
    /// </summary>
    /// <exception cref="ArgumentNullException">documento es null.</exception>
    public static string NombreDeUsuarioDe(Documento documento);
}

// ───────────── SolicitudDeAcceso.cs ─────────────
/// <summary>
/// RN-20. Fila inmutable del registro de accesos: la intención del operador, escrita ANTES de llamar al sistema de
/// identidad (ADR-0017). Su resultado es otra fila (ResultadoDeSolicitud). Id = Guid.CreateVersion7(Fecha).
/// </summary>
public sealed class SolicitudDeAcceso
{
    public const int LongitudMaximaDelOperador = 255;

    public Guid Id { get; }
    public Guid ClienteId { get; }
    /// <summary>Sub del operador, tomado de su token (RN-19).</summary>
    public string Operador { get; }
    public AccionDeAcceso Accion { get; }
    public DateTimeOffset Fecha { get; }

    private SolicitudDeAcceso(Guid id, Guid clienteId, string operador, AccionDeAcceso accion, DateTimeOffset fecha);

    /// <summary>SinAcceso o Deshabilitado → Habilitar (CL-16). Habilitado → Restablecer (CL-13).</summary>
    /// <exception cref="ArgumentException">clienteId vacío u operador null, vacío, solo espacios o de más de 255 caracteres.</exception>
    public static SolicitudDeAcceso ParaHabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha);

    /// <summary>Habilitado → Deshabilitar. Valida antes de crear nada: si lanza, no hay solicitud (CA-04).</summary>
    /// <exception cref="AccesoNoHabilitadoException">estadoActual es SinAcceso o Deshabilitado (CL-14).</exception>
    /// <exception cref="ArgumentException">Igual que ParaHabilitar.</exception>
    public static SolicitudDeAcceso ParaDeshabilitar(Guid clienteId, string operador, EstadoDeAcceso estadoActual, DateTimeOffset fecha);

    /// <summary>Resultado Completado de ESTA solicitud. No modifica la solicitud: devuelve una fila nueva.</summary>
    public ResultadoDeSolicitud Completar(DateTimeOffset fecha, string? motivo = null);

    /// <exception cref="ArgumentException">motivo null, vacío o de más de 200 caracteres.</exception>
    public ResultadoDeSolicitud Fallar(DateTimeOffset fecha, string motivo);
}

// ───────────── ResultadoDeSolicitud.cs ─────────────
/// <summary>RN-20 / ADR-0017. Fila inmutable con el desenlace de una solicitud; como máximo una por solicitud (la base lo garantiza).</summary>
public sealed class ResultadoDeSolicitud
{
    public const int LongitudMaximaDelMotivo = 200;

    public Guid SolicitudId { get; }
    public ResultadoDeAcceso Resultado { get; }
    public DateTimeOffset Fecha { get; }
    /// <summary>Texto técnico corto sin secretos (por ejemplo "HTTP 500 al restablecer la clave" o "Confirmada al reintentar"). null si no hace falta.</summary>
    public string? Motivo { get; }

    // Solo la crean SolicitudDeAcceso.Completar/Fallar (constructor internal) y EF Core (constructor privado).
}
```

#### Application

```csharp
// ───────────── src/CoreBancario.Application/AccesosDigitales/ClaveTemporal.cs ─────────────
namespace CoreBancario.Application.AccesosDigitales;

/// <summary>
/// Clave de un solo uso para el primer ingreso (sección 4 de la spec). Clase y no record a propósito: ToString NUNCA
/// devuelve el valor, para que un log estructurado o una interpolación descuidada no la filtre (RNF-03).
/// </summary>
public sealed class ClaveTemporal
{
    public const int LongitudMinima = 16;
    public string Valor { get; }
    /// <exception cref="ArgumentException">valor es null o tiene menos de 16 caracteres.</exception>
    public ClaveTemporal(string valor);
    /// <returns>"[clave temporal oculta]".</returns>
    public override string ToString();
}

/// <summary>Fuente de claves temporales aleatorias. Puerto por CA-02 (las pruebas fijan la secuencia) y porque el azar es infraestructura, como en ADR-0013.</summary>
public interface IGeneradorDeClaveTemporal
{
    ClaveTemporal Generar();
}

// ───────────── UsuarioDeIdentidad.cs ─────────────
/// <summary>Lo que la API necesita saber de un usuario del realm clientes. UltimaSolicitud: la marca de ADR-0017 (null si nunca se puso o no es un Guid).</summary>
public sealed record UsuarioDeIdentidad(string Sub, bool Habilitado, Guid? UltimaSolicitud);

// ───────────── IProveedorDeIdentidad.cs ─────────────
/// <summary>
/// Puerto hacia el sistema de identidad (Keycloak, realm clientes), solo para gestionar accesos (RF-13). Cada operación
/// que cambia algo escribe la marca (Id de la solicitud) en su ÚLTIMO paso: si la marca coincide, se aplicó entera (ADR-0017).
/// Todas las operaciones pueden lanzar:
///   ProveedorDeIdentidadSinRespuestaException: no hubo respuesta (tiempo de espera, conexión); el cambio pudo aplicarse o no.
///   ErrorDelProveedorDeIdentidadException: respondió con un estado de error; la operación no se completó (pudo quedar a medias).
/// </summary>
public interface IProveedorDeIdentidad
{
    /// <summary>Búsqueda exacta, sin distinguir mayúsculas. null si no existe.</summary>
    Task<UsuarioDeIdentidad?> BuscarPorNombreDeUsuarioAsync(string nombreDeUsuario, CancellationToken cancellationToken);

    /// <summary>null si no existe.</summary>
    Task<UsuarioDeIdentidad?> ObtenerAsync(string sub, CancellationToken cancellationToken);

    /// <summary>
    /// Crea el usuario en UNA sola petición: habilitado, con la clave temporal (temporary = true), las acciones
    /// obligatorias UPDATE_PASSWORD y CONFIGURE_TOTP y la marca. Devuelve el sub asignado.
    /// </summary>
    Task<string> CrearAsync(string nombreDeUsuario, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken);

    /// <summary>
    /// Habilitar un acceso existente o restablecerlo (RN-20), en este orden: clave temporal nueva → borrar las
    /// credenciales OTP → cerrar todas sus sesiones → habilitado + UPDATE_PASSWORD + CONFIGURE_TOTP + marca.
    /// </summary>
    Task ReiniciarAccesoAsync(string sub, ClaveTemporal claveTemporal, Guid marca, CancellationToken cancellationToken);

    /// <summary>En este orden: cerrar todas sus sesiones → deshabilitado + marca.</summary>
    Task DeshabilitarAsync(string sub, Guid marca, CancellationToken cancellationToken);
}

// ───────────── Excepciones del proveedor ─────────────
/// <summary>503 identidad-sin-respuesta. La solicitud queda SIN resultado (pendiente) y el reintento la reconcilia (ADR-0017).</summary>
public sealed class ProveedorDeIdentidadSinRespuestaException : Exception
{
    // Mensaje fijo: "El sistema de identidad no respondió. La operación quedó pendiente de confirmar; vuelve a intentarla."
    public ProveedorDeIdentidadSinRespuestaException(Exception? causa = null);
}

/// <summary>502 identidad-error. Motivo: texto técnico corto, SIN el cuerpo de la respuesta (puede traer datos internos).</summary>
public sealed class ErrorDelProveedorDeIdentidadException : Exception
{
    // Mensaje fijo: "El sistema de identidad rechazó la operación y no se completó. Vuelve a intentarla."
    public ErrorDelProveedorDeIdentidadException(string motivo, Exception? causa = null);
    public string Motivo { get; }
}

// ───────────── Puertos de persistencia ─────────────
public interface IAccesoDigitalRepositorio
{
    void Agregar(AccesoDigital acceso);
    Task<AccesoDigital?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken cancellationToken);
}

/// <summary>Registro de accesos (RN-20): solo inserciones. No hay métodos para editar ni borrar.</summary>
public interface IRegistroDeAccesos
{
    void AgregarSolicitud(SolicitudDeAcceso solicitud);
    void AgregarResultado(ResultadoDeSolicitud resultado);
    /// <summary>Solicitudes del cliente que todavía no tienen resultado, de la más antigua a la más reciente.</summary>
    Task<IReadOnlyList<SolicitudDeAcceso>> ListarPendientesAsync(Guid clienteId, CancellationToken cancellationToken);
}

// ───────────── Comandos y DTOs ─────────────
public sealed record HabilitarAccesoComando(Guid ClienteId, string Operador);
public sealed record DeshabilitarAccesoComando(Guid ClienteId, string Operador);

/// <summary>ToString se sobrescribe para que NO incluya la clave (los records imprimen todas sus propiedades, RNF-03).</summary>
public sealed record AccesoHabilitadoDto(Guid SolicitudId, Guid ClienteId, string Accion, string ClaveTemporal, DateTimeOffset Fecha)
{
    public override string ToString();   // "AccesoHabilitadoDto { SolicitudId = …, ClienteId = …, Accion = …, Fecha = … }"
}
public sealed record RegistroDeAccesoDto(Guid SolicitudId, Guid ClienteId, string Accion, DateTimeOffset Fecha);

// ───────────── Localización y reconciliación (públicas porque los handlers las reciben por constructor) ─────────────
/// <summary>Vínculo de la base (si hay) y usuario del proveedor (si hay).</summary>
public sealed record AccesoLocalizado(AccesoDigital? Vinculo, UsuarioDeIdentidad? Usuario)
{
    /// <summary>Usuario null → SinAcceso; Habilitado → Habilitado; si no → Deshabilitado.</summary>
    public EstadoDeAcceso Estado { get; }
}

public sealed class LocalizadorDeAcceso(IAccesoDigitalRepositorio accesos, IProveedorDeIdentidad proveedor)
{
    /// <summary>
    /// Con vínculo: ObtenerAsync(vinculo.Sub). Sin vínculo: BuscarPorNombreDeUsuarioAsync(AccesoDigital.NombreDeUsuarioDe(cliente.Documento)),
    /// que encuentra al usuario que dejó un habilitar interrumpido, para ADOPTARLO en vez de crearlo dos veces (ADR-0017).
    /// </summary>
    /// <exception cref="InvalidOperationException">Hay vínculo pero el usuario ya no existe en el proveedor (inconsistencia; 500, sección 10).</exception>
    public Task<AccesoLocalizado> LocalizarAsync(Cliente cliente, CancellationToken cancellationToken);
}

public sealed class ReconciliadorDeSolicitudes(IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>
    /// Reconciliación (a) de ADR-0017. Cada solicitud pendiente del cliente se cierra: Completado (motivo "Confirmada al
    /// reintentar") si usuario?.UltimaSolicitud == solicitud.Id; si no, Fallido (motivo "No se aplicó; cerrada al reintentar").
    /// Un solo GuardarCambiosAsync si cerró alguna; ninguno si no había pendientes. Devuelve los resultados escritos.
    /// </summary>
    public Task<IReadOnlyList<ResultadoDeSolicitud>> ReconciliarAsync(Guid clienteId, UsuarioDeIdentidad? usuario, CancellationToken cancellationToken);
}

// ───────────── Handlers ─────────────
public sealed class HabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IGeneradorDeClaveTemporal generador, IAccesoDigitalRepositorio accesos,
    IRegistroDeAccesos registro, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>Secuencia exacta en la sección 5 ("Intención primero").</summary>
    /// <exception cref="RecursoNoEncontradoException">CL-12 (sin escribir nada ni llamar al proveedor).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException">Solicitud pendiente.</exception>
    /// <exception cref="ErrorDelProveedorDeIdentidadException">Solicitud con resultado Fallido.</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">Carrera con otro operador (índice único).</exception>
    public Task<AccesoHabilitadoDto> EjecutarAsync(HabilitarAccesoComando comando, CancellationToken cancellationToken);
}

public sealed class DeshabilitarAccesoHandler(
    IClienteRepositorio clientes, LocalizadorDeAcceso localizador, ReconciliadorDeSolicitudes reconciliador,
    IProveedorDeIdentidad proveedor, IAccesoDigitalRepositorio accesos, IRegistroDeAccesos registro,
    IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <exception cref="RecursoNoEncontradoException">Cliente inexistente.</exception>
    /// <exception cref="AccesoNoHabilitadoException">CL-14 (sin escribir la solicitud).</exception>
    /// <exception cref="ProveedorDeIdentidadSinRespuestaException"/>
    /// <exception cref="ErrorDelProveedorDeIdentidadException"/>
    /// <exception cref="ConflictoDeConcurrenciaException"/>
    public Task<RegistroDeAccesoDto> EjecutarAsync(DeshabilitarAccesoComando comando, CancellationToken cancellationToken);
}

// ───────────── src/CoreBancario.Application/Cuentas (se añade) ─────────────
namespace CoreBancario.Application.Cuentas;

/// <summary>
/// Puerto de LECTURA de las cuentas de un titular identificado por su sub (RN-12, ADR-0016). El filtro de dueño va
/// DENTRO de la consulta: una cuenta ajena y una inexistente devuelven lo mismo (null), por construcción (CA-15).
/// Puerto aparte de ICuentaRepositorio porque es una lectura sin seguimiento con otro criterio (ADR-0004) y así los
/// fakes de la 002 no cambian.
/// </summary>
public interface ICuentasDelTitular
{
    /// <summary>Todas, incluidas las Cerradas (CL-18), por FechaApertura y después Id. Vacía si no tiene cuentas o no hay vínculo.</summary>
    Task<IReadOnlyList<Cuenta>> ListarAsync(string sub, CancellationToken cancellationToken);
    /// <summary>null si no existe O si no es del titular.</summary>
    Task<Cuenta?> ObtenerAsync(Guid cuentaId, string sub, CancellationToken cancellationToken);
}

public sealed record ListarMisCuentasConsulta(string Sub);
public sealed record ObtenerMiCuentaConsulta(Guid CuentaId, string Sub);

public sealed class ListarMisCuentasHandler(ICuentasDelTitular cuentas)
{
    public Task<IReadOnlyList<CuentaDto>> EjecutarAsync(ListarMisCuentasConsulta consulta, CancellationToken cancellationToken);
}

public sealed class ObtenerMiCuentaHandler(ICuentasDelTitular cuentas)
{
    /// <exception cref="RecursoNoEncontradoException">
    /// Inexistente o ajena, con EXACTAMENTE el mismo mensaje que ObtenerCuentaHandler: "No existe una cuenta con ese identificador." (CA-15).
    /// </exception>
    public Task<CuentaDto> EjecutarAsync(ObtenerMiCuentaConsulta consulta, CancellationToken cancellationToken);
}

// DependencyInjection.AddAplicacion: además de lo de la 002, registra como Scoped ListarMisCuentasHandler y
// ObtenerMiCuentaHandler (003a), y LocalizadorDeAcceso, ReconciliadorDeSolicitudes, HabilitarAccesoHandler y
// DeshabilitarAccesoHandler (003b).
```

**Por qué `LocalizadorDeAcceso` y `ReconciliadorDeSolicitudes` son clases y no código dentro de cada handler.** Los dos handlers necesitan exactamente lo mismo antes de decidir: encontrar el acceso y cerrar lo pendiente. Es el mismo *conocimiento*, no dos códigos parecidos (DRY bien aplicado). Son clases concretas, sin interfaz, porque solo tienen una implementación (skill `csharp-clean-code`). `HabilitarAccesoHandler` recibe nueve dependencias. Es mucho, pero cada una es una frontera real (base, proveedor, reloj, azar). Ocultarlas en una fachada solo movería el problema.

#### Infrastructure

```csharp
// ───────────── Persistencia (públicos para las pruebas de integración) ─────────────
namespace CoreBancario.Infrastructure.Persistencia.Repositorios;
public sealed class AccesoDigitalRepositorio(CoreBancarioDbContext db) : IAccesoDigitalRepositorio { /* … */ }
public sealed class RegistroDeAccesos(CoreBancarioDbContext db) : IRegistroDeAccesos { /* … */ }
/// <summary>EF Core, AsNoTracking: cuentas c WHERE (c.Id = @id) AND EXISTS (accesos_digitales a WHERE a.cliente_id = c.cliente_id AND a.sub = @sub).</summary>
public sealed class CuentasDelTitular(CoreBancarioDbContext db) : ICuentasDelTitular { /* … */ }

// CoreBancarioDbContext: se añaden
//   public DbSet<AccesoDigital> AccesosDigitales => Set<AccesoDigital>();                  // 003a
//   public DbSet<SolicitudDeAcceso> SolicitudesDeAcceso => Set<SolicitudDeAcceso>();       // 003b
//   public DbSet<ResultadoDeSolicitud> ResultadosDeAcceso => Set<ResultadoDeSolicitud>();  // 003b
// y, en 003b (cuando la API empieza a escribir esas tablas), GuardarCambiosAsync traduce además a
// ConflictoDeConcurrenciaException las violaciones de unicidad (23505) de "pk_accesos_digitales",
// "ux_accesos_digitales_sub" y "pk_resultados_de_acceso".

namespace CoreBancario.Infrastructure.AccesosDigitales;
/// <summary>RandomNumberGenerator.GetString(Alfabeto, 16). Alfabeto sin caracteres ambiguos (sin 0, O, 1, I, l): ~93 bits.</summary>
public sealed class GeneradorAleatorioDeClaveTemporal : IGeneradorDeClaveTemporal
{
    public const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    public ClaveTemporal Generar();
}

// ───────────── Identidad (adaptador de Keycloak) ─────────────
namespace CoreBancario.Infrastructure.Identidad;

/// <summary>Sección "Keycloak:Administracion". ClientSecret NUNCA en el repositorio: user-secrets o variable de entorno.</summary>
public sealed class OpcionesDeKeycloak
{
    public const string Seccion = "Keycloak:Administracion";
    public Uri? UrlBase { get; set; }                                     // http://localhost:8080
    public string Realm { get; set; } = "clientes";
    public string ClientId { get; set; } = "core-bancario-api-admin";
    public string ClientSecret { get; set; } = "";
    public TimeSpan TiempoDeEspera { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Cliente tipado de la Admin REST API (sección 4, "Llamadas a Keycloak"). Traduce:
///   respuesta 2xx → éxito; GET de usuario con 404 → null;
///   cualquier otra respuesta → ErrorDelProveedorDeIdentidadException("HTTP {estado} al {paso}") (sin el cuerpo);
///   HttpRequestException sin respuesta, o TaskCanceledException por tiempo de espera (no por el token de quien
///   llama) → ProveedorDeIdentidadSinRespuestaException.
/// Compara nombres de usuario en minúsculas: Keycloak los guarda así.
/// </summary>
public sealed class ProveedorDeIdentidadKeycloak(HttpClient http, IOptions<OpcionesDeKeycloak> opciones) : IProveedorDeIdentidad { /* … */ }

/// <summary>DelegatingHandler: obtiene con client credentials el token de la cuenta de servicio y lo reutiliza hasta 30 s antes de que venza.</summary>
public sealed class TokenDeServicioHandler(IHttpClientFactory fabrica, IOptions<OpcionesDeKeycloak> opciones, TimeProvider reloj) : DelegatingHandler { /* … */ }

// AddInfraestructura: además de lo de la 002, registra CuentasDelTitular (Scoped, 003a). En 003b añade
// AccesoDigitalRepositorio y RegistroDeAccesos (Scoped), GeneradorAleatorioDeClaveTemporal (Singleton),
// AddOptions<OpcionesDeKeycloak>().Bind(…) y AddHttpClient<IProveedorDeIdentidad, ProveedorDeIdentidadKeycloak> con
// BaseAddress = UrlBase, Timeout = TiempoDeEspera y AddHttpMessageHandler<TokenDeServicioHandler>(). Como en la 002, la
// configuración se lee de forma perezosa: la Api arranca aunque falte (las pruebas sustituyen el puerto).
```

#### Api

```csharp
// ───────────── src/CoreBancario.Api/Autenticacion/ ─────────────
namespace CoreBancario.Api.Autenticacion;

public static class EsquemasDeAutenticacion
{
    public const string PorEmisor = "Bearer";       // esquema de política (por defecto): elige uno de los dos siguientes
    public const string Clientes = "Clientes";      // JwtBearer del realm clientes
    public const string Empleados = "Empleados";    // JwtBearer del realm empleados
}

public static class Politicas
{
    public const string Cliente = "Cliente";        // RequireRole(Roles.Cliente)
    public const string Operador = "Operador";      // RequireRole(Roles.Operador)
}

public static class Roles
{
    public const string Cliente = "Cliente";               // rol de la API (claim ClaimTypes.Role)
    public const string Operador = "Operador";
    public const string ClienteEnKeycloak = "cliente";     // rol de realm en clientes (realm_access.roles)
    public const string OperadorEnKeycloak = "operador";   // rol de realm en empleados
}

/// <summary>Sección "Autenticacion". La tolerancia de reloj NO es configurable: RNF-01 depende de ella (ADR-0015).</summary>
public sealed class OpcionesDeAutenticacion
{
    public const string Seccion = "Autenticacion";
    public static readonly TimeSpan ToleranciaDeReloj = TimeSpan.FromSeconds(30);
    public const string AlgoritmoPermitido = "RS256";
    public string Audiencia { get; set; } = "core-bancario-api";
    public OpcionesDeEmisor Clientes { get; set; } = new();
    public OpcionesDeEmisor Empleados { get; set; } = new();
}
public sealed class OpcionesDeEmisor
{
    /// <summary>URL del realm, igual al "iss" de sus tokens: http://localhost:8080/realms/clientes.</summary>
    public string Emisor { get; set; } = "";
}

public static class ConfiguracionDeAutenticacion
{
    /// <summary>
    /// AddAuthentication(PorEmisor) + AddPolicyScheme(PorEmisor, ForwardDefaultSelector = SelectorDeEsquema.Elegir)
    /// + AddJwtBearer(Clientes) + AddJwtBearer(Empleados), cada uno con: Authority = Emisor; MapInboundClaims = false;
    /// IncludeErrorDetails = false; RequireHttpsMetadata = !entorno.IsDevelopment(); TokenValidationParameters {
    /// ValidIssuer = Emisor, ValidAudience = Audiencia, ValidAlgorithms = [RS256], ClockSkew = ToleranciaDeReloj,
    /// RequireExpirationTime = true, RequireSignedTokens = true, ValidateLifetime = true, NameClaimType = "preferred_username" };
    /// Events = new EventosDeJwt(Roles.ClienteEnKeycloak, Roles.Cliente) o (OperadorEnKeycloak, Operador).
    /// AddAuthorization: políticas Cliente y Operador; FallbackPolicy = RequireAuthenticatedUser (una ruta nueva sin
    /// política queda protegida por defecto, no abierta).
    /// </summary>
    public static IServiceCollection AddAutenticacionConKeycloak(this IServiceCollection services,
        IConfiguration configuracion, IHostEnvironment entorno);
}

public static class SelectorDeEsquema
{
    /// <summary>
    /// Lee el "iss" del Bearer SIN validarlo (JsonWebTokenHandler.ReadJsonWebToken), solo para elegir quién lo valida.
    /// iss == Empleados.Emisor → Empleados. Cualquier otro caso (sin cabecera, ilegible, otro emisor) → Clientes,
    /// que lo rechazará y registrará el motivo. Nunca lanza.
    /// </summary>
    public static string Elegir(HttpContext contexto, OpcionesDeAutenticacion opciones);
}

/// <summary>
/// Eventos compartidos por los dos esquemas (resuelve ILogger y IProblemDetailsService de RequestServices):
///  TokenValidated: si no hay "sub" → context.Fail(new TokenSinSujetoException()). Después borra TODOS los claims de
///    rol que traiga el token (tipos identidad.RoleClaimType y ClaimTypes.Role; revisión de 003a) y, si
///    realm_access.roles contiene rolEnKeycloak, añade Claim(identidad.RoleClaimType, rolDeLaApi). El rol de la API
///    depende del ESQUEMA, no del token: un token de clientes nunca obtiene Operador, ni aunque alguien cree ese rol en
///    su realm ni aunque el token traiga un claim de rol propio.
///  Challenge: registra (Information) "Petición no autenticada en {Metodo} {Ruta}: {Motivo}" con
///    MotivoDeRechazo.Desde(context.AuthenticateFailure), NUNCA el token; HandleResponse(); 401;
///    WWW-Authenticate: Bearer (sin detalles); ProblemDetails "No autenticado", codigo no-autenticado.
///  Forbidden: registra (Information) el rechazo; 403; ProblemDetails "Prohibido", codigo prohibido.
/// </summary>
public sealed class EventosDeJwt(string rolEnKeycloak, string rolDeLaApi) : JwtBearerEvents
{
    public override Task TokenValidated(TokenValidatedContext context);
    public override Task Challenge(JwtBearerChallengeContext context);
    public override Task Forbidden(ForbiddenContext context);
}

/// <summary>RNF-04. Motivo para el log; jamás se envía al cliente.</summary>
public static class MotivoDeRechazo
{
    public const string SinCredencial = "sin-credencial";             // falla == null
    public const string Vencida = "vencida";                          // SecurityTokenExpiredException
    public const string AunNoValida = "aun-no-valida";                // SecurityTokenNotYetValidException
    public const string FirmaInvalida = "firma-invalida";             // SecurityTokenInvalidSignatureException, SecurityTokenSignatureKeyNotFoundException
    public const string EmisorIncorrecto = "emisor-incorrecto";       // SecurityTokenInvalidIssuerException
    public const string AudienciaIncorrecta = "audiencia-incorrecta"; // SecurityTokenInvalidAudienceException
    public const string AlgoritmoNoPermitido = "algoritmo-no-permitido"; // SecurityTokenInvalidAlgorithmException
    public const string SinIdentificador = "sin-identificador";       // TokenSinSujetoException
    public const string MalFormada = "mal-formada";                   // cualquier otra
    public static string Desde(Exception? falla);
}

public sealed class TokenSinSujetoException : Exception { public TokenSinSujetoException(); }

public static class ClaimsPrincipalExtensiones
{
    /// <summary>El claim "sub" (MapInboundClaims = false). EventosDeJwt garantiza que existe.</summary>
    /// <exception cref="InvalidOperationException">No hay "sub" (error de programación).</exception>
    public static string Sub(this ClaimsPrincipal usuario);
}

/// <summary>
/// RNF-05. Filtro de los endpoints del operador que cambian datos. Tras ejecutar, registra (Information)
/// "Operación {Operacion} del operador {OperadorId}: {Resultado}". Operacion es el nombre del endpoint (WithName),
/// Resultado vale "completada" o "no completada". Es "no completada" si la ejecución lanzó (y relanza) o si devolvió
/// un IStatusCodeHttpResult con código >= 400 (revisión de 003a), mirando dentro de Results<A, B> (INestedHttpResult).
/// </summary>
public sealed class RegistroDeOperacionDelOperador(ILogger<RegistroDeOperacionDelOperador> logger) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next);
}

// ───────────── Endpoints ─────────────
namespace CoreBancario.Api.AccesosDigitales;
public static class EndpointsDeAccesoDigital
{
    /// <summary>Grupo con RequireAuthorization(Politicas.Operador), RegistroDeOperacionDelOperador y DocumentarErroresComunes.</summary>
    public static IEndpointRouteBuilder MapEndpointsDeAccesoDigital(this IEndpointRouteBuilder app);
}

namespace CoreBancario.Api.Cuentas;
public static class EndpointsDeMisCuentas
{
    /// <summary>Grupo /mis-cuentas con RequireAuthorization(Politicas.Cliente) y DocumentarErroresComunes.</summary>
    public static IEndpointRouteBuilder MapEndpointsDeMisCuentas(this IEndpointRouteBuilder app);
}
// MapEndpointsDeClientes y MapEndpointsDeCuentas (002): mismas rutas, ahora dentro de un grupo con
// RequireAuthorization(Politicas.Operador) y DocumentarErroresComunes; RegistroDeOperacionDelOperador en
// RegistrarCliente, AbrirCuenta, BloquearCuenta, DesbloquearCuenta y CerrarCuenta.

// ───────────── src/CoreBancario.Api/OpenApi/ ─────────────
namespace CoreBancario.Api.OpenApi;
public static class DocumentacionDeErrores
{
    /// <summary>Declara en el OpenAPI las respuestas 401, 403 y 500 con ProblemDetails (RNF-06).</summary>
    public static TBuilder DocumentarErroresComunes<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder;
}
/// <summary>Documento: components.securitySchemes["Bearer"] = http/bearer/JWT con una descripción de los dos realms y la audiencia.</summary>
public sealed class EsquemaDeSeguridadEnOpenApi : IOpenApiDocumentTransformer { /* … */ }
/// <summary>Operación: añade el requisito "Bearer" a toda operación con metadatos de autorización y sin AllowAnonymous.</summary>
public sealed class RequisitoDeSeguridadEnOpenApi : IOpenApiOperationTransformer { /* … */ }
```

**Forma de un endpoint (ilustrativa, no es contrato):**

```csharp
grupo.MapPost("/clientes/{clienteId:guid}/acceso-digital/habilitar", async Task<Ok<AccesoHabilitadoDto>> (
        Guid clienteId, ClaimsPrincipal usuario, HttpContext http, HabilitarAccesoHandler handler, CancellationToken ct) =>
    {
        var acceso = await handler.EjecutarAsync(new HabilitarAccesoComando(clienteId, usuario.Sub()), ct);
        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(acceso);
    })
    .WithName("HabilitarAccesoDigital")
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
```

**`Program.cs` (cambios):** `AddAutenticacionConKeycloak(builder.Configuration, builder.Environment)` y `AddOpenApi(o => { o.AddDocumentTransformer<EsquemaDeSeguridadEnOpenApi>(); o.AddOperationTransformer<RequisitoDeSeguridadEnOpenApi>(); })`. Después de `UseExceptionHandler()` van `UseAuthentication()` y `UseAuthorization()`. `/health` lleva `.AllowAnonymous()` y, en Development, también `MapOpenApi()`. Al final se mapean `MapEndpointsDeMisCuentas()` (003a) y `MapEndpointsDeAccesoDigital()` (003b).

**Configuración (`appsettings.Development.json`, sin secretos):**

```json
"Autenticacion": { "Audiencia": "core-bancario-api",
  "Clientes":  { "Emisor": "http://localhost:8080/realms/clientes" },
  "Empleados": { "Emisor": "http://localhost:8080/realms/empleados" } },
"Keycloak": { "Administracion": { "UrlBase": "http://localhost:8080", "Realm": "clientes",
  "ClientId": "core-bancario-api-admin", "TiempoDeEspera": "00:00:10" } }
```

La sección `Autenticacion` llega en 003a y `Keycloak:Administracion` en 003b. `Keycloak:Administracion:ClientSecret` va por `dotnet user-secrets` o `Keycloak__Administracion__ClientSecret`. En `appsettings.json` (producción) solo va `Autenticacion:Audiencia`: las URL las pone el despliegue (D-11).

## 4. Modelo de datos y persistencia

### Modelo de dominio

| Tipo | Clase | Identidad | Mutabilidad |
|---|---|---|---|
| `AccesoDigital` | Entidad (vínculo) | `ClienteId` | Inmutable (si cambia el `sub`, ver sección 10) |
| `SolicitudDeAcceso` | Entidad | `Id` (UUID v7 de `Fecha`) | Inmutable |
| `ResultadoDeSolicitud` | Entidad | `SolicitudId` | Inmutable |

**El estado del acceso no se guarda en la base.** Habilitado o Deshabilitado, y si el segundo factor está inscrito, viven solo en Keycloak, que es quien los aplica al iniciar sesión. Si también vivieran en la base, habría dos verdades que pueden contradecirse (ADR-0017). La base guarda el **vínculo** (quién es quién) y el **registro** (qué se pidió y cómo terminó).

### Tablas (snake_case)

**`accesos_digitales`** (bloque 003a, migración `VinculoDeAccesoDigital`)

| Columna | Tipo | Nulo | Origen |
|---|---|---|---|
| `cliente_id` | `uuid` PK (`pk_accesos_digitales`), FK → `clientes(id)` `ON DELETE RESTRICT` | No | `AccesoDigital.ClienteId` |
| `sub` | `varchar(255)` | No | `AccesoDigital.Sub`; índice único `ux_accesos_digitales_sub` |
| `fecha_de_vinculo` | `timestamptz` | No | `AccesoDigital.FechaDeVinculo` |

**`solicitudes_de_acceso`** (bloque 003b, migración `RegistroDeAccesos`)

| Columna | Tipo | Nulo | Origen |
|---|---|---|---|
| `id` | `uuid` PK | No | `SolicitudDeAcceso.Id` |
| `cliente_id` | `uuid` FK → `clientes(id)` `ON DELETE RESTRICT`, índice `ix_solicitudes_de_acceso_cliente_id` | No | |
| `operador` | `varchar(255)` | No | sub del operador |
| `accion` | `varchar(12)`, CHECK `ck_solicitudes_de_acceso_accion` (`Habilitar`, `Restablecer`, `Deshabilitar`) | No | `HasConversion<string>()` |
| `fecha` | `timestamptz` | No | |

**`resultados_de_acceso`** (bloque 003b, migración `RegistroDeAccesos`)

| Columna | Tipo | Nulo | Origen |
|---|---|---|---|
| `solicitud_id` | `uuid` PK (`pk_resultados_de_acceso`), FK → `solicitudes_de_acceso(id)` `ON DELETE RESTRICT` | No | La PK garantiza **un solo resultado por solicitud**: dos reconciliaciones simultáneas no pueden cerrar dos veces la misma (la segunda recibe 409) |
| `resultado` | `varchar(10)`, CHECK `ck_resultados_de_acceso_resultado` (`Completado`, `Fallido`) | No | |
| `fecha` | `timestamptz` | No | |
| `motivo` | `varchar(200)` | Sí | |

- **Inmutabilidad en la base (defensa en profundidad, como los CHECK de la 002).** La migración crea una función `impedir_cambios_en_registro_de_accesos()` que lanza una excepción. La disparan dos triggers `BEFORE UPDATE OR DELETE`, uno en `solicitudes_de_acceso` y otro en `resultados_de_acceso` (`migrationBuilder.Sql`). El código nunca edita ni borra filas, así que el trigger solo actúa si alguien lo intenta a mano o por error.
- **Por qué dos tablas y no una con una columna de estado.** Con una sola fila por solicitud habría que **editarla** para poner el resultado, y el registro dejaría de ser inmutable. Con dos tablas, "pendiente" se define como "solicitud sin resultado", y eso se consulta con un `LEFT JOIN`.
- **Consulta documentada de solicitudes sin resultado** (reconciliación (b) de ADR-0017; va al README y tiene su prueba de integración):

```sql
SELECT s.id, s.cliente_id, s.operador, s.accion, s.fecha
FROM solicitudes_de_acceso s
LEFT JOIN resultados_de_acceso r ON r.solicitud_id = s.id
WHERE r.solicitud_id IS NULL
ORDER BY s.fecha;
```

### Migraciones

Hay dos, una por bloque, para que cada PR lleve solo su esquema:

- **`VinculoDeAccesoDigital` (003a):** solo `accesos_digitales`.
- **`RegistroDeAccesos` (003b):** `solicitudes_de_acceso`, `resultados_de_acceso`, la función y los dos triggers, escritos a mano con `Sql(...)` y con su `Down`.

Las dos se generan con `dotnet ef migrations add <Nombre> --project src/CoreBancario.Infrastructure --output-dir Persistencia/Migraciones`, y después se ejecuta `dotnet format`.

### Keycloak (configuración versionada en `infra/keycloak/`)

```
infra/keycloak/
  realms/clientes-realm.json        (Keycloak exige el nombre <realm>-realm.json para importar al arrancar)
  realms/empleados-realm.json
  themes/core-bancario/login/                 theme.properties (parent=keycloak.v2), messages/messages_es.properties
  themes/core-bancario-clientes/login/        theme.properties (parent=core-bancario, scripts=js/documento.js),
                                              messages/messages_es.properties, resources/js/documento.js
```

Imagen **`quay.io/keycloak/keycloak:26.3.3`**, la misma de los experimentos, en `docker-compose.yml` y en las pruebas. El servicio corre `start-dev --import-realm` y monta `realms/` en `/opt/keycloak/data/import` y cada tema en `/opt/keycloak/themes/<tema>`. También monta un volumen para los datos. Las variables `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD` (003a) y `KC_CLIENTES_API_SECRET` (003b) se leen de `.env`. Los realms usan el marcador `${KC_CLIENTES_API_SECRET}`: Keycloak resuelve variables de entorno en los archivos de import, así que **el secreto nunca está en el repositorio**. Ojo: si el realm ya existe, el import se omite. Para recargar un realm modificado hay que borrar el volumen de Keycloak (README).

**Ajustes de los dos realms** (los marcados con ✔ los verifica una prueba de configuración que lee el JSON, sección 7):

| Ajuste | `clientes` | `empleados` | Para qué |
|---|---|---|---|
| `accessTokenLifespan` ✔ | 270 | 270 | RNF-01 (ADR-0015) |
| `ssoSessionIdleTimeout` / `ssoSessionMaxLifespan` ✔ | 300 / 1800 | 900 / 32400 | RN-22 |
| `revokeRefreshToken` / `refreshTokenMaxReuse` ✔ | true / 0 | true / 0 | Rotación: un refresh token robado sirve una sola vez |
| `bruteForceProtected`, `permanentLockout` ✔ | true, false | true, false | RN-23 |
| `failureFactor` / `waitIncrementSeconds` / `maxFailureWaitSeconds` ✔ | 5 / 900 / 900 | 5 / 900 / 900 | 5 fallos → 15 min, y se libera sola. Con la estrategia por múltiplos, la espera al 5.º fallo es 900 × (5 ÷ 5) |
| `maxDeltaTimeSeconds` | 43200 | 43200 | Tiempo de reinicio del contador (12 h por defecto). Keycloak recomienda que sea mayor que la espera máxima |
| `quickLoginCheckMilliSeconds` / `minimumQuickLoginWaitSeconds` | 1000 / 60 | 1000 / 60 | Por defecto: dos intentos con menos de 1 s de diferencia bloquean 1 min. Es más estricto que RN-23 y se acepta |
| `browserFlow` ✔ | copia del flujo de navegador con **OTP Form `REQUIRED`** (no condicional) | ídem | RN-24. Según la documentación de Keycloak, si un segundo factor es `REQUIRED` y el usuario no lo tiene, lo inscribe durante el login. Por eso, aunque un administrador cree un operador sin acciones obligatorias, el OTP es inevitable |
| `requiredActions` | `CONFIGURE_TOTP` (acción por defecto), `UPDATE_PASSWORD` con `max_auth_age = 0`; `VERIFY_PROFILE`, `UPDATE_PROFILE`, `VERIFY_EMAIL` y `delete_credential` desactivadas (revisión de 003a: `delete_credential` permitiría al usuario borrar su OTP con `kc_action`, y el restablecimiento es del operador) | ídem | RN-21. Con `max_auth_age = 0`, cambiar la clave **siempre** pide la actual (RF-15: "exige la clave actual"). Si no, Keycloak no la pide si se inició sesión hace menos de 5 min |
| `otpPolicy*` | TOTP, HmacSHA1, 6 dígitos, 30 s, sin reutilizar código | ídem | RN-24 |
| `passwordPolicy` ✔ | `length(12) and maxLength(64) and notUsername() and notContainsUsername()` | `length(12) and maxLength(64) and notUsername() and notContainsUsername()` | Sección 4 de la spec. El servidor rechaza la clave que contiene el usuario completo (`CC1234567`). Del número suelto **avisa el tema en el navegador** (decisión del autor, pregunta 3; ver "Aviso del número de documento") |
| `loginWithEmailAllowed`, `registrationAllowed`, `resetPasswordAllowed`, `rememberMe`, `editUsernameAllowed` ✔ | false | false | Sin autorregistro ni recuperación sin operador (sección 9). No se inicia sesión con correo: solo con el documento (RN-19) |
| `defaultSignatureAlgorithm` | RS256 | RS256 | `ValidAlgorithms` de la API (ADR-0015) |
| Roles de realm | `cliente`, incluido en los roles por defecto | `operador` (lo asigna el administrador) | Mapeo de roles de la API |
| Roles por defecto ✔ | **sin** `offline_access`, `manage-account` ni `view-profile` | ídem | `offline_access` permitiría sesiones que no caducan (rompe RN-22). La consola de cuenta permitiría a un usuario **borrar su OTP**: quien conozca su clave podría inscribir su propio celular en el siguiente login |
| Cliente `core-bancario-web` ✔ | público, solo código de autorización con **PKCE S256**, `directAccessGrantsEnabled = false`, scopes por defecto `basic`, `roles` y un mapper de audiencia `core-bancario-api`; sin scope opcional `offline_access` | ídem | Sin concesión directa (usuario y clave enviados por la SPA sin pasar por el formulario con OTP). `basic` es el scope que, desde Keycloak 25, pone el `sub` en el token. Sin él, la API no tendría a quién atribuir la petición |
| Cliente `core-bancario-api-admin` ✔ (**bloque 003b**) | confidencial, **solo** cuenta de servicio, `secret = ${KC_CLIENTES_API_SECRET}`, roles de `realm-management`: `manage-users` y `view-users` | **no existe** | Menor privilegio (ADR-0014). La documentación dice que `manage-users` no permite escalar a roles de administración que la propia cuenta no tenga |
| Perfil de usuario (declarativo) | `email`, `firstName` y `lastName` no obligatorios, o fuera del perfil (003a); atributo `ultima_solicitud_de_acceso` con permisos de ver y editar **solo `admin`** (003b); sin atributos no gestionados | `email` no obligatorio | Hallazgo 6: Keycloak no exige completar el perfil (ver la decisión 3 de la sección 8). El atributo es la marca de ADR-0017 |
| `loginTheme` | `core-bancario-clientes` | `core-bancario` | CA-08, CA-12 |
| `internationalizationEnabled`, `supportedLocales`, `defaultLocale` | true, `es`, `es` | ídem | Mensajes en español |
| `eventsEnabled`, `adminEventsEnabled` | true | true | Rastro de inicios de sesión y cambios de administración en Keycloak. Es una segunda fuente de evidencia si alguna vez hay que investigar una solicitud pendiente |

**Tema `core-bancario` (base de los dos).** `messages_es.properties` sobrescribe `invalidUserMessage`, `invalidPasswordMessage`, `accountDisabledMessage`, `accountTemporarilyDisabledMessage` y `accountPermanentlyDisabledMessage` con **el mismo texto**: "Los datos de ingreso no son correctos." (CA-08, CL-01, hallazgo 1). Si al probarlo aparece otra clave con un texto distinto en alguno de los tres casos, se añade a la lista.

**Tema `core-bancario-clientes`.** Además, `documento.js`:

- expone una función pura `normalizarUsuario(tipo, numero)` que hace lo mismo que `Documento` + `AccesoDigital.NombreDeUsuarioDe`: quita espacios, `.` y `-` y pasa a mayúsculas solo las letras ASCII. Con `"CC"` y `"1.234.567"` devuelve `"CC1234567"`;
- añade al formulario de login un selector de tipo (CC, CE o PA);
- al enviar, escribe en el campo `username` el resultado de `normalizarUsuario` (hallazgo 2).

`messages_es.properties` cambia la etiqueta del usuario a "Número de documento". Si la inyección por `scripts=` resultara frágil con `keycloak.v2`, la alternativa es sobrescribir `login.ftl` (sección 10).

**Aviso del número de documento (decisión del autor, pregunta 3).** Es parte de `documento.js` y actúa solo en `clientes`, en la pantalla de cambio de clave (`login-update-password.ftl`). Esa pantalla aparece en los dos casos: la acción obligatoria del primer ingreso y `kc_action=UPDATE_PASSWORD` (RF-15).

- **Función pura** `contieneNumeroDeDocumento(usuario, clave)`. El número es `usuario` sin sus dos primeras letras (el tipo). Devuelve `true` si `clave`, en mayúsculas ASCII, contiene ese número. Por ejemplo: `("cc1234567", "Abc1234567xyz!")` → `true`; `("cc1234567", "OtraClaveLarga!")` → `false`.
- **Al enviar el formulario:** si devuelve `true`, el script cancela el envío y muestra junto al campo de la clave nueva un mensaje que indica la regla rota (CL-06): "La clave no puede contener tu número de documento."
- **De dónde sale el usuario:** del campo `username` oculto que la plantilla de Keycloak incluye en esa pantalla para los gestores de contraseñas. **Hay que verificarlo en el Build.** Si no estuviera, se sobrescribe `login-update-password.ftl` para exponer `${username}` en un atributo `data-`.
  - **Resultado del Build (003a):** la plantilla de `keycloak.v2` en 26.3.3 **no** trae el campo `username`. Se aplicó la alternativa: el tema `core-bancario-clientes` sobrescribe `login-update-password.ftl` y expone el usuario en `data-usuario`. Esa plantilla queda acoplada a Keycloak 26.3.3 (ver el riesgo 8).
- **Límite aceptado:** el aviso **se puede saltar** (sin JavaScript, o con una petición hecha a mano). Se acepta porque la regla protege al usuario de sí mismo, no frente a un atacante. Lo que sí garantiza el servidor es `notContainsUsername`.
- **Cómo se prueba:**
  - **Jint** ejecuta `contieneNumeroDeDocumento` con casos positivos y negativos.
  - **Punta a punta (viable, con `HttpClient`):**
    - la página de cambio de clave carga `documento.js` y trae el campo `username` con el usuario;
    - el servidor **rechaza** una clave que contiene `CC1234567` (`notContainsUsername`);
    - el servidor **acepta** una clave que contiene solo `1234567`. Esta prueba documenta el hueco aceptado: si algún día el servidor la rechaza, la prueba avisa de que el límite cambió.
  - Ninguna prueba ejecuta el aviso *dentro* de un navegador (riesgo 2).

### Llamadas a Keycloak (Admin REST API, realm `clientes`)

Rutas de la Admin REST API que usa el adaptador. Las pruebas del adaptador contra Keycloak real son las que confirman que cada una hace lo esperado:

| Operación del puerto | Peticiones (en este orden) |
|---|---|
| Token de servicio | `POST /realms/clientes/protocol/openid-connect/token` (`grant_type=client_credentials`) |
| `BuscarPorNombreDeUsuarioAsync` | `GET /admin/realms/clientes/users?username={minúsculas}&exact=true` |
| `ObtenerAsync` | `GET /admin/realms/clientes/users/{sub}` (404 → null) |
| `CrearAsync` | `POST /admin/realms/clientes/users` con `username`, `enabled: true`, `requiredActions: [UPDATE_PASSWORD, CONFIGURE_TOTP]`, `credentials: [{ type: password, value, temporary: true }]` y `attributes.ultima_solicitud_de_acceso = [marca]`. El sub sale de la cabecera `Location` |
| `ReiniciarAccesoAsync` | `PUT …/users/{sub}/reset-password` (`temporary: true`) → `GET …/users/{sub}/credentials` y `DELETE …/credentials/{id}` de cada una de tipo `otp` → `POST …/users/{sub}/logout` → `GET …/users/{sub}` y `PUT …/users/{sub}` con la representación **completa** modificada (`enabled`, `requiredActions`, marca). Keycloak reemplaza `attributes` entero, así que no se envía una representación parcial |
| `DeshabilitarAsync` | `POST …/users/{sub}/logout` → `GET` y `PUT …/users/{sub}` con `enabled: false` y la marca |

En los dos casos, **la marca va en el último paso.** Si coincide, se aplicó todo lo anterior. Deshabilitar cierra las sesiones *antes* de deshabilitar: si cerrarlas fallara después, la marca diría "completado" sin serlo. Un usuario deshabilitado tampoco puede renovar su sesión, así que, si alguien entra justo en esos milisegundos, deshabilitar lo detiene igual.

### Paquetes (verificar el parche exacto al instalar; todos los `Microsoft.*` con el mismo parche que la 002)

| Proyecto | Paquete | Versión |
|---|---|---|
| Infrastructure (003b) | `Microsoft.Extensions.Http` | 10.0.12 |
| Infrastructure (003b) | `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.12 |
| Api (003a) | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12 |
| Api (003a) | `Microsoft.Extensions.ApiDescription.Server` (ADR-0019) | 10.0.12 |
| Keycloak.Tests | `Testcontainers.Keycloak`, `Testcontainers.PostgreSql` | 4.15.0 |
| Keycloak.Tests | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 |
| Keycloak.Tests | `Otp.NET` (TOTP, RFC 6238), `AngleSharp` (leer formularios HTML), `Jint` (ejecutar `documento.js`) | última estable |
| Keycloak.Tests | `xunit.v3.mtp-v2` | 4.0.1 |

Domain sigue **sin paquetes**.

## 5. Casos límite y su manejo

### Intención primero: secuencia de `HabilitarAccesoHandler` (ADR-0017)

1. `clientes.ObtenerAsync` → si no existe, `RecursoNoEncontradoException`. **No se escribe nada ni se llama al proveedor** (CL-12, CA-04).
2. `localizador.LocalizarAsync(cliente)` → vínculo y usuario. Si el proveedor no responde, se lanza 503 y **no se escribe nada**: todavía no se pidió nada.
3. `reconciliador.ReconciliarAsync(cliente.Id, acceso.Usuario)` → cierra las solicitudes pendientes de intentos anteriores (reconciliación (a)).
4. `SolicitudDeAcceso.ParaHabilitar(cliente.Id, comando.Operador, acceso.Estado, ahora)` → `AgregarSolicitud` → **`GuardarCambiosAsync` (se confirma la intención)**.
5. `generador.Generar()`. Desde aquí se usa **`CancellationToken.None`**: con la intención ya confirmada, un operador que cierra el navegador no debe dejar la operación a medias. La duración la acota el tiempo de espera del `HttpClient` (10 s).
   - Usuario null → `CrearAsync(nombre, clave, solicitud.Id)` → sub.
   - Usuario existente (habilitado, deshabilitado o huérfano de un intento anterior) → `ReiniciarAccesoAsync(usuario.Sub, clave, solicitud.Id)`.
6. `ErrorDelProveedorDeIdentidadException` → `AgregarResultado(solicitud.Fallar(ahora, ex.Motivo))` → `GuardarCambiosAsync` → se relanza (502).
   `ProveedorDeIdentidadSinRespuestaException` → **no se escribe resultado** → se relanza (503). Un tiempo de espera agotado no es un "no".
7. Éxito → si no había vínculo, `accesos.Agregar(AccesoDigital.Vincular(cliente.Id, sub, ahora))` (adopción o alta) → `AgregarResultado(solicitud.Completar(ahora))` → `GuardarCambiosAsync`. Si esta escritura falla (la base cae), la solicitud queda **pendiente** y el siguiente intento la confirma por la marca.
8. Devuelve `AccesoHabilitadoDto(solicitud.Id, cliente.Id, solicitud.Accion.ToString(), clave.Valor, solicitud.Fecha)`.

`DeshabilitarAccesoHandler` sigue los mismos pasos. En el paso 4 usa `ParaDeshabilitar`, que lanza `AccesoNoHabilitadoException` **antes** de agregar la solicitud (CL-14, CA-04). En el paso 5 llama a `DeshabilitarAsync(usuario.Sub, solicitud.Id)`.

**Ejemplo de reintento.** Un deshabilitar agota el tiempo de espera, pero Keycloak sí lo aplicó. Al reintentar, el paso 3 ve que la marca coincide y cierra la solicitud anterior como Completada. El paso 4 ve el acceso Deshabilitado y responde 422 `acceso-no-habilitado`. El operador recibe un "ya está deshabilitado" correcto, no a ciegas, y el registro queda cerrado. Ese 422 no escribe una solicitud nueva (CA-04). El resultado que se escribió es el de la solicitud *anterior*.

**Ejemplo de habilitar interrumpido.** `CrearAsync` se aplicó, pero la base cayó antes del paso 7. Queda un usuario en Keycloak sin vínculo y una solicitud pendiente. Al reintentar, el localizador encuentra al usuario por su nombre y el reconciliador cierra la solicitud anterior como Completada. Como el usuario está habilitado, la nueva solicitud es un Restablecer, que adopta al usuario (crea el vínculo) y entrega una clave nueva. La clave del intento anterior nunca llegó a nadie y deja de servir.

### Casos de la spec

**Convención de nombres (revisión de 003a).** Cada CL o RF que se prueba lleva al menos una prueba con su ID en el nombre (`F003_CLxx_…`), siempre que su CA siga cubierto por otra prueba con el ID del CA. Cuando la única prueba de un CA es también la que cubre el CL o el RF, conserva el ID del CA y la trazabilidad queda en la tabla de abajo: CL-04 → `F003_CA07_…`, CL-07 → `F003_CA18_…` y RF-14 → CA-06, CA-07, CA-08 y CA-11.

| ID | Caso | Manejo | Test esperado |
|---|---|---|---|
| CL-01 | Documento inexistente, clave incorrecta, deshabilitado o bloqueado: mismo mensaje | Tema `core-bancario`: las claves de mensaje del hallazgo 1 con el mismo texto. Durante el bloqueo, Keycloak ya muestra el genérico | E2E: `F003_CA08_DocumentoInexistenteClaveIncorrectaYDeshabilitado_MismoMensaje`, `F003_CL01_CredencialBloqueada_MismoMensajeGenerico` |
| CL-02 | `1.234.567` = `1234567` | `documento.js` normaliza igual que `Documento` (RN-17) | Keycloak.Tests: `F003_CL02_ScriptDelTema_NormalizaComoElDominio` (Jint; teoría con `1.234.567`, ` 1 234 567 `, `1-234-567`, PA `ab-12.345`; compara con `AccesoDigital.NombreDeUsuarioDe`). E2E: `F003_CA12_UsuarioNormalizado_EntraComoElCliente`. Domain: `F003_RN17_NombreDeUsuario_EsTipoMasNumeroNormalizado` |
| CL-03 | Código OTP incorrecto cuenta como fallo | Keycloak (hallazgo 3) | E2E: `F003_CL03_CodigoOtpIncorrecto_CuentaComoIntentoFallido` (4 claves erróneas + 1 OTP erróneo → bloqueado) |
| CL-04 | Con clave temporal no se accede a nada | Keycloak no emite el código de autorización hasta completar `UPDATE_PASSWORD` y `CONFIGURE_TOTP` (hallazgo 5); sin token, la API responde 401 | E2E: `F003_CA07_ConClaveTemporalSinCompletarLosPasos_NoSeEmiteTokenYLaApiResponde401` |
| CL-05 | Clave temporal vencida a las 72 h | **Límite conocido, sin implementación** (decisión del autor, pregunta 2; spec enmendada el 2026-10-13). Keycloak 26 no permite que una clave temporal venza: "Expire password" obliga a cambiarla, pero no impide usarla. La clave sirve hasta que el operador restablece o deshabilita el acceso. Mejora futura en la sección 10 | Sin prueba (límite conocido) |
| CL-06 | Clave nueva que rompe la política | `passwordPolicy` del realm. Keycloak indica la regla rota en español (bundle `es`). El número de documento suelto lo avisa `documento.js` (sección 4) | E2E: `F003_CA13_ClaveNuevaDemasiadoCorta_SeRechazaIndicandoLaRegla`, `F003_CL06_ClaveNuevaConElUsuarioCompleto_ElServidorLaRechaza`, `F003_CL06_ClaveNuevaConSoloElNumero_ElServidorLaAcepta` (documenta el límite), `F003_CL06_PaginaDeCambioDeClave_CargaElScriptYTraeElUsuario`. Jint: `F003_CL06_ContieneNumeroDeDocumento_DetectaElNumeroSuelto` (teoría) |
| CL-07 | Sin credencial, vencida, firma inválida u otra audiencia → 401 | `JwtBearer` con `ValidIssuer`, `ValidAudience`, `ValidAlgorithms`, `ClockSkew`; `EventosDeJwt.Challenge` → 401 `no-autenticado` | Api: `F003_CA18_OperacionProtegidaConCredencialInvalida_Responde401` (teoría: cada operación × {sin token, vencido, firma de otra clave, otra audiencia, otro emisor, HS256}) |
| CL-08 | Cliente en operación del operador → 403, exista o no el recurso | La autorización corre **antes** del endpoint: el handler ni se ejecuta | Api: `F003_CA19_ClienteLlamaOperacionDelOperador_Responde403` (teoría con las 10 operaciones del operador, con ids inexistentes), `F003_CL08_ClienteRegistraCliente_Responde403YNoCreaElCliente`, `F003_CA05_ClienteHabilitaAcceso_Responde403SinRegistros` |
| CL-09 | Operador en "mis cuentas" → 403 | Política `Cliente` en el grupo `/mis-cuentas` | Api: `F003_CL09_OperadorLlamaMisCuentas_Responde403` |
| CL-10 | Cerrar sesión: no se renueva; el access token vigente sigue ≤ 5 min | Keycloak revoca la sesión y su refresh token. La API valida sin consultar (ADR-0015) | E2E: `F003_CA11_CerrarSesion_ElRefreshTokenYaNoSirve` |
| CL-11 | Inactividad o vida máxima | `ssoSessionIdleTimeout`/`ssoSessionMaxLifespan`; el refresh token vence con la inactividad | E2E (realm con tiempos cortos): `F003_CL11_ClienteInactivo_SesionNoSeRenueva`, `F003_CA10_OperadorInactivo_SesionNoSeRenueva`. Configuración: `F003_RN22_RealmsDeProduccion_TienenLosTiemposDeSesion` |
| CL-12 | Habilitar un cliente inexistente | Paso 1 | App: `F003_CA04_HabilitarClienteInexistente_LanzaNoEncontradoSinRegistrarNiLlamarAlProveedor`. Api: `F003_CA04_HabilitarClienteInexistente_Responde404SinRegistros` |
| CL-13 | Habilitar uno ya habilitado = restablecer | `ParaHabilitar` con Habilitado → Restablecer; `ReiniciarAccesoAsync` | Domain: `F003_CL13_ParaHabilitarConAccesoHabilitado_EsRestablecer`. App: `F003_CA02_HabilitarClienteHabilitado_RestableceConClaveDistintaYRegistraRestablecer`. Keycloak.Tests: `F003_CA02_ReiniciarAcceso_BorraOtpCierraSesionesYPoneClaveTemporal` |
| CL-14 | Deshabilitar inexistente o ya deshabilitado | `ParaDeshabilitar` lanza antes de crear la solicitud → 422 `acceso-no-habilitado` | Domain: `F003_CL14_ParaDeshabilitarSinAccesoODeshabilitado_LanzaAccesoNoHabilitado` (teoría). App: `F003_CA04_DeshabilitarYaDeshabilitado_LanzaAccesoNoHabilitadoSinRegistrar`. Api: `F003_CA04_DeshabilitarYaDeshabilitado_Responde422SinRegistros` |
| CL-15 | Tras deshabilitar o restablecer, no se renueva la sesión | `logout` del usuario en Keycloak, y además `enabled = false` al deshabilitar | E2E: `F003_CA03_Deshabilitar_SesionNoSeRenuevaYNoPuedeIniciarSesion`, `F003_CL15_Restablecer_SesionNoSeRenueva` |
| CL-16 | Rehabilitar un deshabilitado | `ParaHabilitar` con Deshabilitado → Habilitar; `ReiniciarAccesoAsync` (vuelve a habilitar) | Domain: `F003_CL16_ParaHabilitarConAccesoDeshabilitado_EsHabilitar`. App: `F003_CL16_HabilitarClienteDeshabilitado_RegistraHabilitarYLoHabilita` |
| CL-17 | Cuenta ajena = inexistente | Filtro por dueño en la consulta; mismo mensaje y mismo código | Infra: `F003_CA15_CuentaAjenaEInexistente_DevuelvenNull`. Api: `F003_CL17_CuentaAjenaEInexistente_RespuestasIdenticasSalvoTraceId` (compara estado, cabeceras relevantes y cuerpo sin `traceId`) |
| CL-18 | Cliente sin cuentas → lista vacía; Cerradas incluidas | `ListarAsync` sin filtrar por estado | Api: `F003_CA17_ClienteSinCuentas_DevuelveListaVacia`, `F003_CA14_MisCuentas_DevuelveActivaYCerradaSinCuentasAjenas` |

### Intención primero (pruebas de Application con el fake, ADR-0017)

| Escenario | Esperado | Test |
|---|---|---|
| La intención se confirma antes de llamar al proveedor | El fake comprueba, en el momento de la llamada, que `UnidadDeTrabajoEspia` ya guardó la solicitud | `F003_ADR0017_SolicitudSeConfirmaAntesDeLlamarAlProveedor` |
| El proveedor responde con error | Solicitud + resultado Fallido con el motivo; se propaga `ErrorDelProveedorDeIdentidadException` | `F003_ADR0017_ProveedorRespondeConError_RegistraFallidoYPropaga` |
| El proveedor no responde | Solicitud **sin** resultado; se propaga `ProveedorDeIdentidadSinRespuestaException` | `F003_ADR0017_ProveedorNoResponde_DejaLaSolicitudPendiente` |
| Se aplicó pero no hubo respuesta, y luego se reintenta | La anterior se cierra Completada ("Confirmada al reintentar") y la nueva es Restablecer | `F003_ADR0017_ReintentoConPendienteAplicada_LaCierraComoCompletada` |
| No se aplicó, no hubo respuesta, y luego se reintenta | La anterior se cierra Fallida y la nueva es Habilitar | `F003_ADR0017_ReintentoConPendienteNoAplicada_LaCierraComoFallida` |
| Usuario en el proveedor sin vínculo | No se llama a `CrearAsync`: se adopta (`ReiniciarAccesoAsync` + vínculo) | `F003_ADR0017_UsuarioExisteSinVinculo_LoAdoptaSinCrearOtro` |
| La base falla después de aplicar en el proveedor | La solicitud queda pendiente (sin resultado) | `F003_ADR0017_FallaLaBaseTrasAplicar_LaSolicitudQuedaPendiente` |
| El proveedor no responde al localizar | No se escribe nada | `F003_ADR0017_ProveedorNoRespondeAlLocalizar_NoRegistraNada` |

### Catálogo de errores (se añade a la tabla de ADR-0011)

| Origen | HTTP | `codigo` | `title` |
|---|---|---|---|
| Sin sesión válida (`EventosDeJwt.Challenge`) | 401 | `no-autenticado` | No autenticado (sin `detail`) |
| Rol incorrecto (`EventosDeJwt.Forbidden`) | 403 | `prohibido` | Prohibido (sin `detail`) |
| `AccesoNoHabilitadoException` | 422 | `acceso-no-habilitado` | Regla de negocio rota |
| `ErrorDelProveedorDeIdentidadException` | 502 | `identidad-error` | Error del sistema de identidad |
| `ProveedorDeIdentidadSinRespuestaException` | 503 | `identidad-sin-respuesta` | Sistema de identidad no disponible |

`ManejadorDeErrores` no cambia: usa el mensaje (fijo y en español) como `detail` en 422, 502 y 503, y registra cualquier 5xx con nivel Error. La prueba por reflexión de la 002 obliga a dar código propio a `AccesoNoHabilitadoException`. Los 401 y 403 no pasan por el catálogo, porque los escriben los eventos de JwtBearer. Sus códigos van en `CodigosDeError` para que el contrato esté en un solo archivo. Esto **amplía** ADR-0011 sin contradecirla: su consecuencia ya decía "En el S3 se añadirán 401 y 403 con la misma forma".

## 6. Estrategia para los requisitos no funcionales

| RNF | Cómo se cumple | Cómo se verifica |
|---|---|---|
| RNF-01 | `accessTokenLifespan = 270` en los dos realms + `ClockSkew = 30 s` constante en el código = 300 s (ADR-0015) | Api: `F003_RNF01_VidaDelTokenMasToleranciaDeReloj_NoSuperaCincoMinutos` (lee los dos realm JSON y la `ClockSkew` de `IOptionsMonitor<JwtBearerOptions>` de cada esquema). `F003_CA21_TokenEmitidoHaceMasDeCincoMinutos_SeRechaza` (`iat = ahora − 301 s`, `exp = iat + 270`). `F003_CA21_TokenVencidoDentroDeLaTolerancia_SeAcepta` (`exp = ahora − 20 s`) |
| RNF-02 | 401 desde `Challenge`, 403 desde `Forbidden`, 404 por el filtro de dueño en la consulta (ADR-0016) | CA-15, CA-18, CA-19 |
| RNF-03 | `ClaveTemporal.ToString` oculto; `AccesoHabilitadoDto.ToString` sin la clave; `no-store`; `IncludeErrorDetails = false`; la API nunca registra cabeceras ni cuerpos; el secreto de servicio va por user-secrets o entorno y por `${KC_CLIENTES_API_SECRET}` en el realm; gitleaks en el CI | App: `F003_RNF03_ClaveTemporal_ToStringNoRevelaElValor`, `F003_RNF03_AccesoHabilitadoDto_ToStringNoRevelaLaClave`. Api: `F003_RNF03_HabilitarAcceso_NingunLogContieneLaClaveTemporal` (proveedor de logs en memoria), `F003_RNF03_HabilitarAcceso_RespondeNoStore`. Configuración: `F003_RNF03_RealmClientes_NoContieneElSecretoDeServicio` (el valor es exactamente el marcador) |
| RNF-04 | `EventosDeJwt.Challenge` registra `MotivoDeRechazo` y nunca el token; la respuesta es idéntica para todos los motivos | Api: `F003_RNF04_TokenVencido_ElLogTieneElMotivoSinElToken` (teoría por motivo; el log no contiene el token ni su firma), `F003_RNF04_MotivosDistintos_MismaRespuesta401` |
| RNF-05 | `RegistroDeOperacionDelOperador` en las siete operaciones que cambian datos | Api: `F003_CA20_OperadorUsaOperacionesDe002_ElLogIncluyeSuIdentificador` (teoría con las cinco de la 002), `F003_RNF05_GestionarAcceso_ElLogIncluyeElOperador` |
| RNF-06 | `DocumentarErroresComunes` en todas las operaciones protegidas + esquema y requisito de seguridad en el OpenAPI. El documento se genera al compilar, se versiona en `src/CoreBancario.Api/openapi/core-bancario-api.json` y el CI comprueba que está al día. No se expone en producción (ADR-0019) | Api: `F003_CA22_ContratoFueraDeDevelopment_*` (5: esquema Bearer, requisito en cada operación, 401/403/500, operaciones de mis cuentas y errores como ProblemDetails) y `F002_RNF05_ContratoOpenApi_…` adaptada (con 401, 403 y 500 en cada operación, y las cuatro nuevas) |
| RNF-07 | Flujo `/sdd-build`: test-writer primero | Commit `test(003): pruebas en rojo` |
| RNF-08 | `CoreBancario.Keycloak.Tests` recorre el flujo real; Api.Tests firma tokens con RSA propio (ADR-0018) | 003a: `F003_CA06_PrimerIngreso_…`, que hace login real y llama a `/mis-cuentas` con un usuario creado por el fixture. 003b: `F003_RNF08_FlujoCompleto_OperadorHabilitaYClienteConsultaSusCuentas` |

## 7. Estrategia de pruebas

En cuatro niveles (ADR-0018), cada uno responde a una pregunta distinta:

| Nivel | Pregunta que responde | Qué se prueba | Proyecto |
|---|---|---|---|
| Unitarias (Domain) | ¿Las reglas de acción son correctas? | `SolicitudDeAcceso` (CL-13, CL-14, CL-16), `Completar`/`Fallar`, `NombreDeUsuarioDe`, guardas | `tests/CoreBancario.Domain.Tests` |
| Unitarias (Application) | ¿"Intención primero" deja siempre rastro? | Handlers y reconciliación con fakes deterministas (sección 5) | `tests/CoreBancario.Application.Tests` |
| Integración (Infrastructure) | ¿La base protege el registro y filtra por dueño? | Migración, trigger, PK de resultados, índices del vínculo, `CuentasDelTitular`, `ListarPendientesAsync`, la consulta SQL documentada, el generador de claves y la traducción de errores HTTP del adaptador (con un `HttpMessageHandler` falso, sin Keycloak) | `tests/CoreBancario.Infrastructure.Tests` |
| API | ¿La API autentica, autoriza y responde el contrato? | CA-01…CA-05 con el proveedor falso, CA-14…CA-22, 401/403/404, configuración de los realms | `tests/CoreBancario.Api.Tests` |
| Adaptador + punta a punta | ¿Keycloak hace lo que el adaptador cree, y el flujo real funciona? | Adaptador contra Keycloak real; login real (CA-03, CA-06…CA-13, CL-15, RNF-08); `documento.js` con Jint | `tests/CoreBancario.Keycloak.Tests` (**nuevo**) |

### Pruebas por bloque

Cada bloque tiene su propio commit en rojo: `test(003): pruebas en rojo del bloque 003a` y después `… del bloque 003b`.

- **003a:** todas las pruebas de CA-06…CA-22; CL-01…CL-11, CL-17 y CL-18; las de configuración de los realms, salvo las de la cuenta de servicio y su secreto; RNF-01, RNF-02, RNF-04, RNF-05 y RNF-06 para las operaciones de la 002 y de "mis cuentas"; Domain de `AccesoDigital`; Infra de la migración `VinculoDeAccesoDigital` y de `CuentasDelTitular`; Jint.
- **003b:** todas las de CA-01…CA-05 y CL-12…CL-16; las de "intención primero" (Application y API); Domain de `SolicitudDeAcceso`; Infra de `RegistroDeAccesos` (trigger, PK de resultados, unicidad del `sub` con su traducción, pendientes, consulta documentada, generador de claves, traducción HTTP del adaptador); Keycloak.Tests del adaptador; configuración de la cuenta de servicio (`F003_ADR0014_…`, `F003_RNF03_RealmClientes_…`); RNF-03 de la clave temporal; las filas del contrato de `HabilitarAccesoDigital` y `DeshabilitarAccesoDigital`; y `F003_RNF08_FlujoCompleto_…`.

### Dónde va cada CA (RNF-07)

| CA | Prueba(s) | Proyecto |
|---|---|---|
| CA-01 | `F003_CA01_HabilitarClienteSinAcceso_CreaUsuarioYRegistraSolicitudYCompletado`, `F003_CA01_HabilitarClienteSinAcceso_VinculaElSubConElCliente` · `F003_CA01_HabilitarAcceso_Responde200ConClaveTemporalYRegistraAlOperadorDelToken` · `F003_CA01_CrearAsync_UsuarioHabilitadoConClaveTemporalAccionesObligatoriasYMarca` | App · Api · Keycloak |
| CA-02 | `F003_CA02_HabilitarClienteHabilitado_RestableceConClaveDistintaYRegistraRestablecer` · `F003_CA02_HabilitarDosVeces_LaSegundaEsRestablecerConOtraClave` · `F003_CA02_ReiniciarAcceso_BorraOtpCierraSesionesYPoneClaveTemporal` | App · Api · Keycloak |
| CA-03 | `F003_CA03_DeshabilitarClienteHabilitado_RegistraDeshabilitar` · `F003_CA03_DeshabilitarAcceso_Responde200YRegistraDeshabilitar` · `F003_CA03_Deshabilitar_SesionNoSeRenuevaYNoPuedeIniciarSesion` | App · Api · Keycloak (E2E) |
| CA-04 | `F003_CA04_HabilitarClienteInexistente_…`, `F003_CA04_DeshabilitarYaDeshabilitado_…` (en App y en Api; la de Api cuenta filas antes y después) | App · Api |
| CA-05 | `F003_CA05_ClienteHabilitaAcceso_Responde403SinRegistros` | Api |
| CA-06 | `F003_CA06_PrimerIngreso_ExigeCambiarClaveEInscribirOtpYLuegoEntraConRolCliente` (el token obtenido llama a `/mis-cuentas` con 200) | Keycloak (E2E) |
| CA-07 | `F003_CA07_ConClaveTemporalSinCompletarLosPasos_NoSeEmiteTokenYLaApiResponde401` | Keycloak (E2E) |
| CA-08 | `F003_CA08_DocumentoInexistenteClaveIncorrectaYDeshabilitado_MismoMensaje` | Keycloak (E2E) |
| CA-09 | `F003_CA09_CincoFallos_ElSextoCorrectoSeRechazaYTrasLaEsperaEntra` | Keycloak (E2E, tiempos cortos) |
| CA-10 | `F003_CL11_ClienteInactivo_SesionNoSeRenueva`, `F003_CA10_OperadorInactivo_SesionNoSeRenueva` · `F003_RN22_RealmsDeProduccion_TienenLosTiemposDeSesion` | Keycloak (E2E) · Api |
| CA-11 | `F003_CA11_CerrarSesion_ElRefreshTokenYaNoSirve` | Keycloak (E2E) |
| CA-12 | `F003_CL02_ScriptDelTema_NormalizaComoElDominio` (Jint) + `F003_CA12_UsuarioNormalizado_EntraComoElCliente` + `F003_CA12_PaginaDeLogin_CargaElScriptDelTema` | Keycloak |
| CA-13 | `F003_CA13_CambiarClave_PideLaActualYDespuesSoloFuncionaLaNueva`, `F003_CA13_ClaveNuevaDemasiadoCorta_SeRechazaIndicandoLaRegla` | Keycloak (E2E) |
| CA-14 | `F003_CA14_MisCuentas_DevuelveActivaYCerradaSinCuentasAjenas` · `F003_RN12_CuentasDelTitular_SoloDevuelveLasDelClienteVinculado` | Api · Infra |
| CA-15 | `F003_CL17_CuentaAjenaEInexistente_RespuestasIdenticasSalvoTraceId` · `F003_CA15_CuentaAjenaEInexistente_DevuelvenNull` | Api · Infra |
| CA-16 | `F003_CA16_MiCuenta_DevuelveNumeroMonedaEstadoSaldoYFechas` · `F003_CA16_ObtenerAsync_CuentaPropia_DevuelveLaCuentaConSuSaldoYFechas` · `F003_RF05_MiCuenta_DevuelveNumeroMonedaEstadoSaldoYFechas` | Application · Infra · Api |
| CA-17 | `F003_CA17_ClienteSinCuentas_DevuelveListaVacia` | Api |
| CA-18 | `F003_CA18_OperacionProtegidaConCredencialInvalida_Responde401` | Api |
| CA-19 | `F003_CA19_ClienteLlamaOperacionDelOperador_Responde403`, `F003_CL09_OperadorLlamaMisCuentas_Responde403` | Api |
| CA-20 | Las pruebas de API de la 002, que siguen con el nombre `F002_…` y ahora llevan un token de operador, + `F003_CA20_OperadorUsaOperacionesDe002_ElLogIncluyeSuIdentificador` | Api |
| CA-21 | `F003_CA21_TokenEmitidoHaceMasDeCincoMinutos_SeRechaza`, `F003_CA21_TokenVencidoDentroDeLaTolerancia_SeAcepta` | Api |
| CA-22 | `F003_CA22_ContratoFueraDeDevelopment_*` (5: esquema Bearer, requisito en cada operación, 401/403/500, operaciones de mis cuentas y errores como ProblemDetails) | Api |

Más pruebas que no son de un CA:

- **Correcciones de la revisión de 003a (Api):** `F003_RN21_RealmsDeProduccion_AccionesQueBorranElOtpOEditanElPerfilEstanDesactivadas` (teoría: `delete_credential`, `UPDATE_PROFILE` y `VERIFY_EMAIL` en los dos realms), `F003_RN19_TokenDeClientesConClaimDeRolOperador_Responde403` y `F003_RN19_TokenDeEmpleadosConClaimDeRolCliente_Responde403EnMisCuentas` (teorías con el claim de rol largo de Microsoft y con `role`), `F003_RNF05_OperacionDelOperadorQueRespondeError400_ElLogDiceNoCompletada`.

- **Domain:** `F003_RN20_SolicitudSinOperador_LanzaArgumentException`, `F003_RN20_Solicitud_IdEsVersion7DeLaFecha`, `F003_RN20_Completar_DevuelveResultadoDeEsaSolicitud`, `F003_RN20_FallarSinMotivo_LanzaArgumentException`, `F003_ADR0016_VincularSinSub_LanzaArgumentException`.
- **Infra:** `F003_RNF02_BaseVacia_TieneLasMigracionesDeLa003` (en 003a comprueba `VinculoDeAccesoDigital`; en 003b se amplía con `RegistroDeAccesos`), `F003_RN20_EditarOBorrarElRegistro_LoRechazaLaBase`, `F003_RN20_DosResultadosParaLaMismaSolicitud_LanzaConflicto`, `F003_ADR0016_DosClientesConElMismoSub_LanzaConflicto`, `F003_ADR0017_ListarPendientes_SoloDevuelveSolicitudesSinResultado`, `F003_ADR0017_ConsultaDocumentadaDeSolicitudesSinResultado_DevuelveLasPendientes` (ejecuta el SQL de la sección 4 tal cual, porque ADR-0004 exige probar el SQL escrito a mano), `F003_RNF03_ClaveTemporalGenerada_Tiene16CaracteresDelAlfabeto`, `F003_CA02_ClavesGeneradas_SonDistintas`, `F003_ADR0017_RespuestaDeError_LanzaErrorDelProveedorSinElCuerpo`, `F003_ADR0017_TiempoDeEsperaAgotado_LanzaSinRespuesta`, `F003_ADR0017_UsuarioInexistente_DevuelveNull`.
- **Api (configuración de los realms, leyendo los JSON de `infra/keycloak/realms`):** `F003_RN22_…`, `F003_RN23_RealmsDeProduccion_BloqueanCincoFallosQuinceMinutos`, `F003_RN24_RealmsDeProduccion_ExigenOtpEnElFlujoDeNavegador`, `F003_RN21_ClienteWeb_SinConcesionDirectaNiScopeOfflineAccess`, `F003_RN21_RealmsDeProduccion_RolesPorDefectoSinOfflineAccessNiConsolaDeCuenta`, `F003_ADR0014_CuentaDeServicio_SoloTieneManageUsersYViewUsers`, `F003_RNF03_RealmClientes_NoContieneElSecretoDeServicio`. Más el catálogo: `F003_RNF02_CatalogoDeErrores_ClasificaLosErroresNuevos`.
- **Keycloak.Tests (adaptador):** `F003_CA03_DeshabilitarAsync_DeshabilitaCierraSesionesYPoneMarca`, `F003_ADR0017_BuscarPorNombreDeUsuario_IgnoraMayusculasYDevuelveNullSiNoExiste`, `F003_ADR0014_CuentaDeServicio_NoPuedeModificarElRealm` (un `PUT /admin/realms/clientes` con su token responde 403).

### Infraestructura de pruebas (la escribe el test-writer)

- **Application.Tests:** fakes nuevos `ProveedorDeIdentidadEnMemoria` y `AccesoDigitalRepositorioEnMemoria`, `RegistroDeAccesosEnMemoria` y `GeneradorDeClaveDeSecuencia`. El proveedor guarda usuarios por sub, registra las llamadas y ofrece modos de falla: `ResponderConError()`, `NoResponder()`, `AplicarYNoResponder()`. `AplicarYNoResponder` aplica el cambio con la marca y luego lanza sin respuesta: es el caso que importa. `UnidadDeTrabajoEspia` (ya existe) debe poder registrar *qué* había agregado en cada guardado, o el fake del registro marca las filas confirmadas al guardar.
- **Api.Tests: `ApiFixture` cambia.**
  - Genera dos pares de claves RSA (uno por emisor) y fija la configuración con emisores ficticios (`https://pruebas.local/realms/clientes` y `…/empleados`).
  - En `ConfigureTestServices`, para cada esquema, hace `Configure<JwtBearerOptions>(esquema, o => o.Configuration = new OpenIdConnectConfiguration { Issuer = … })` y añade la clave a `o.Configuration.SigningKeys`. Con una configuración fija, JwtBearer no descarga nada.
  - En 003b, sustituye `IProveedorDeIdentidad` por un fake propio de este proyecto. Duplicar un fake pequeño entre dos proyectos es aceptable (DRY a la tercera).
  - **`CrearCliente()` devuelve ahora un cliente con token de Operador**, así que las pruebas de la 002 cambian lo mínimo y conservan su nombre `F002_…`.
  - Añade `CrearClienteAnonimo()`, `CrearClienteComoCliente(string sub)`, `CrearClienteConToken(string token)` y un `EmisorDePruebas` con `CrearToken(EmisorDeToken emisor, string sub, string[] roles, TimeSpan? vida = null, DateTimeOffset? emitido = null, string? audiencia = null, SigningCredentials? firma = null)`.
  - Para el vínculo de las pruebas de "mis cuentas", el test inserta un `AccesoDigital` con un `sub` único directamente en la base (como la 002 preparaba cuentas con saldo).
  - Un `ProveedorDeLogsEnMemoria` (propio, sin paquete) captura los logs para RNF-03, RNF-04 y RNF-05.
  - Las pruebas de configuración localizan `infra/keycloak/realms` subiendo directorios hasta encontrar `CoreBancario.slnx`.
- **Keycloak.Tests (nuevo, `[assembly: AssemblyFixture]`):**
  - Un solo contenedor de Keycloak (`quay.io/keycloak/keycloak:26.3.3`) que importa **los realm JSON de producción** y monta los temas. `KC_CLIENTES_API_SECRET` toma un valor aleatorio. Además, un PostgreSQL y la API con `WebApplicationFactory`, apuntando a ese Keycloak.
  - **Los "tiempos cortos" no son otro JSON.** Tras arrancar, el fixture usa el admin de `master` para bajar en los dos realms la inactividad, la espera del bloqueo y la verificación de login rápido. También registra la `redirect_uri` de las pruebas en `core-bancario-web` y crea un operador en `empleados`. En 003a también crea los usuarios cliente y su vínculo (sección 1.1). Así no hay dos copias del realm que puedan desincronizarse, y la prueba de configuración de Api.Tests sigue garantizando los valores de producción.
  - Navegador simulado con `HttpClient` (`AllowAutoRedirect = false`) y un `DelegatingHandler` que guarda y reenvía las cookies a mano, porque llevan `Secure` y el `CookieContainer` no las reenvía por HTTP (hallazgo 7). AngleSharp lee la acción y los campos de cada formulario. Para inscribir el OTP, la prueba lee el secreto del formulario de configuración y calcula el código con Otp.NET (RFC 6238). **Antes de un segundo ingreso, espera al siguiente periodo de 30 s**, porque Keycloak no acepta reutilizar un código.
  - Flujo: código de autorización + PKCE con `core-bancario-web`, y luego `POST /token`.
- **Al crear `CoreBancario.Keycloak.Tests`**, su `.csproj` nace sin `--ignore-exit-code 8` (ADR-0006: ya tiene pruebas) y se añade a `CoreBancario.slnx`.

### Pruebas de la 002 que cambian (las adapta el test-writer en el commit en rojo)

- Ninguna cambia de nombre ni de aserciones de negocio. Siguen en verde porque `CrearCliente()` ya lleva token de operador.
- `F002_RNF05_ContratoOpenApi_DescribeCadaOperacionYSusErrores`: cada fila añade `401,403,500` (por ejemplo `RegistrarCliente` → `"201,400,401,403,409,500"`). En 003a se añaden filas para `ListarMisCuentas` (`200,401,403,500`) y `ObtenerMiCuenta` (`200,401,403,404,500`). En 003b, para `HabilitarAccesoDigital` (`200,401,403,404,409,500,502,503`) y `DeshabilitarAccesoDigital` (`200,401,403,404,409,422,500,502,503`). Las filas nuevas son comportamiento de la 003, pero conviven en la misma teoría para no duplicarla.
- `F002_RNF02_BaseVacia_QuedaConTodasLasMigracionesAplicadas` sigue igual (no hay pendientes y `Inicial` está aplicada).

## 8. Decisiones

ADRs nuevas (todas en `Aceptada` desde el 2026-10-13):

- **ADR-0014: Keycloak como emisor de tokens, con dos realms (D-08).** Decidido por el autor. Incluye la cuenta de servicio de mínimo privilegio.
- **ADR-0015: Validación local de JWT con ventana de revocación de 5 minutos.** Dos esquemas y un selector por emisor, 270 s + 30 s, RS256 y el rol según el esquema.
- **ADR-0016: Vínculo de identidad en una tabla propia y autorización por propiedad en la consulta.** BOLA cerrado por construcción y rutas separadas por rol. El S5 lo reutilizará para RN-12 en transferencias.
- **ADR-0017: Consistencia entre Keycloak y la base: intención primero.** Registro inmutable, marca en el último paso y reconciliación al reintentar.
- **ADR-0018: Pruebas de autenticación en cuatro niveles.**
- **ADR-0019: Contrato OpenAPI fuera de Development.** Decidido por el autor: se genera al compilar, se versiona en Git, el CI comprueba que está al día y no se expone en producción.

Decisiones del plan sin ADR (locales y fáciles de cambiar):

1. **Nombre de usuario `CC1234567`** (tipo + número normalizado, sin separador). El prefijo de dos letras hace imposible que dos documentos distintos den el mismo nombre. Keycloak lo guarda en minúsculas (hallazgo 2), y el adaptador compara en minúsculas. El dominio no se entera de esa manía de Keycloak.
2. **Una sola operación "habilitar" para habilitar y restablecer** (CL-13). Dos endpoints harían lo mismo según el estado.
3. **Keycloak sin datos personales del cliente.** El perfil del realm no exige correo, nombre ni apellido, así que la API crea el usuario solo con el nombre de usuario. La otra opción del hallazgo 6 era copiar los datos del `Cliente`. Se descarta por tres motivos:
   - **una fuente de verdad por dato:** los datos del cliente viven en nuestra base;
   - **minimización de datos:** Keycloak no necesita saber cómo se llama el cliente para autenticarlo;
   - con correo en Keycloak, "iniciar sesión con correo" o un cambio de correo serían otra puerta que vigilar.

   Si el Build descubre que Keycloak 26 no permite quitar `email` del perfil, se deja sin `required`: el correo no se puede borrar del perfil, pero sí hacerlo opcional.
4. **El rol se mapea en `TokenValidated` y no con `IClaimsTransformation`.** `IClaimsTransformation` se ejecuta en cada autenticación y no sabe qué esquema validó el token. En el evento de cada esquema, el rol de la API depende del realm (estructural, RN-19).
5. **Política por defecto "usuario autenticado".** Una ruta nueva que alguien olvide marcar queda cerrada, no abierta. Es el principio de "fallar de forma segura".
6. **`ICuentasDelTitular` como puerto de lectura aparte**, con EF Core y `AsNoTracking`. Es una consulta con un `EXISTS`: no justifica Dapper (ADR-0004).
7. **`CancellationToken.None` después de confirmar la intención** (sección 5), para que una desconexión del operador no deje pendiente lo que podía terminarse.
8. **Reutilizar `CuentaDto`** para "mis cuentas" (decisión 8 de la 002).
9. **Mensaje genérico único** ("Los datos de ingreso no son correctos.") en los dos realms.
10. **Tema en dos niveles** (`core-bancario` y `core-bancario-clientes`). Los empleados no inician sesión con documento, así que el script del documento (normalización y aviso del número) solo va en `clientes`.
11. **Dos migraciones, una por bloque** (`VinculoDeAccesoDigital` y `RegistroDeAccesos`), para que cada PR lleve solo el esquema que usa.
12. **La cuenta de servicio y el atributo de la marca entran en el realm en 003b**, junto con el código que los usa. Así, el realm de 003a no tiene una credencial con `manage-users` que nadie usa (menor privilegio).

### 8.1 Contrato OpenAPI fuera de Development (ADR-0019, decidido)

| Pieza | Qué se hace |
|---|---|
| Paquete | `Microsoft.Extensions.ApiDescription.Server` 10.0.12 |
| `.csproj` | `<OpenApiDocumentsDirectory>openapi</OpenApiDocumentsDirectory>` y `<OpenApiGenerateDocumentsOptions>--file-name core-bancario-api</OpenApiGenerateDocumentsOptions>` |
| Archivo versionado | `src/CoreBancario.Api/openapi/core-bancario-api.json`, en Git (no como artefacto del CI). Lo regenera cada compilación |
| `Program.cs` | `MapOpenApi()` sigue **solo en Development**. No se expone en producción |
| CI | Paso tras compilar: `git diff --exit-code -- src/CoreBancario.Api/openapi` (falla si el contrato cambió y no se versionó) |
| Prueba CA-22 | Lee el archivo versionado y comprueba el esquema de seguridad y 401, 403 y 500 en cada operación protegida |
| Tareas | T12 (003a). En 003b, T26 regenera y versiona el archivo con los endpoints nuevos |

## 9. Trazabilidad

| ID de la spec | Sección del plan | Bloque | Tareas |
|---|---|---|---|
| RF-05 (parte del cliente) | 3 (`/mis-cuentas`), 4 (`ICuentasDelTitular`); ADR-0016 | 003a | T02, T04, T11 |
| RF-13 | 3 (acceso digital), 5 (intención primero); ADR-0017 | 003b | T15, T16, T17, T18, T19, T20, T21, T22, T23, T24, T25, T26 |
| RF-14 | 4 (Keycloak: flujo, sesiones, tema); ADR-0014 | 003a | T05, T06 |
| RF-15 | 4 (`UPDATE_PASSWORD` con `max_auth_age = 0`, `kc_action`, aviso del número); ADR-0014 | 003a | T05, T07 |
| RN-12 | 3, 4 (filtro en la consulta); ADR-0016 | 003a | T02, T04, T11 |
| RN-17 | 3 (`NombreDeUsuarioDe`), 4 (`documento.js`) | 003a | T01, T06 |
| RN-19 | 3 (una ruta, un rol; `Operador`/`Sub` del token), 8 (decisión 4); ADR-0015 | 003a, 003b | T08, T09, T11, T26 |
| RN-20 | 3, 4 (registro inmutable), 5; ADR-0017 | 003b | T15, T17, T18, T19, T20, T21, T22, T26, T27 |
| RN-21 | 4 (acciones obligatorias), 5 (CL-04) | 003a, 003b | T05, T23 |
| RN-22 | 4 (tiempos por realm) | 003a | T05 |
| RN-23 | 4 (bloqueo) | 003a | T05 |
| RN-24 | 4 (OTP `REQUIRED`, política OTP) | 003a, 003b | T05, T23 |
| RNF-01 | 6; ADR-0015 | 003a | T05, T08 |
| RNF-02 | 3, 6; ADR-0016 | 003a, 003b | T08, T11, T25 |
| RNF-03 | 3 (`no-store`), 4 (marcador del secreto), 6 | 003a, 003b | T08, T13, T16, T22, T23, T24, T26, T27 |
| RNF-04 | 3 (`MotivoDeRechazo`), 6 | 003a | T08 |
| RNF-05 | 3 (`RegistroDeOperacionDelOperador`), 6 | 003a, 003b | T09, T26 |
| RNF-06 | 3 (OpenAPI), 6, 8.1; ADR-0019 | 003a, 003b | T09, T12, T26 |
| RNF-07 | 6, 7 | 003a, 003b | Todas (flujo de `/sdd-build`, un commit en rojo por bloque) |
| RNF-08 | 1.1, 7; ADR-0018 | 003a, 003b | T05, T06, T10 (login real → API con usuarios del fixture), T26 (flujo completo con habilitar) |
| CL-01 | 4 (tema base), 5 | 003a | T06 |
| CL-02 | 4 (`documento.js`), 5 | 003a | T01, T06 |
| CL-03 | 4 (bloqueo), 5 | 003a | T05 |
| CL-04 | 4, 5 | 003a | T05 |
| CL-05 | 5, 10 (límites conocidos); spec enmendada el 2026-10-13 | — | **Límite conocido, sin prueba.** Solo se documenta (T13) |
| CL-06 | 4 (`passwordPolicy`, aviso del número), 5 | 003a | T05, T07 |
| CL-07 | 3 (401), 5 | 003a | T08 |
| CL-08 | 3, 5 | 003a, 003b | T09, T26 |
| CL-09 | 3, 5 | 003a | T11 |
| CL-10 | 4 (revocación de sesión), 5; ADR-0015 | 003a | T05 |
| CL-11 | 4 (tiempos), 5 | 003a | T05 |
| CL-12 | 5 (paso 1) | 003b | T18, T26 |
| CL-13 | 3 (Domain), 5 | 003b | T15, T18, T23 |
| CL-14 | 3 (Domain), 5 | 003b | T15, T19, T25 |
| CL-15 | 4 (llamadas a Keycloak), 5 | 003b | T05, T23 |
| CL-16 | 3 (Domain), 5 | 003b | T15, T18, T23 |
| CL-17 | 3 (`ObtenerMiCuentaHandler`), 5; ADR-0016 | 003a | T02, T04, T11 |
| CL-18 | 3, 5 | 003a | T02, T04, T11 |
| CA-01 | 3, 5, 7 | 003b | T15, T16, T18, T20, T21, T22, T23, T24, T26 |
| CA-02 | 5 (CL-13), 7 | 003b | T18, T22, T23, T26 |
| CA-03 | 5 (CL-15), 7 | 003b | T19, T23, T26 |
| CA-04 | 5 (CL-12, CL-14), 7 | 003b | T15, T18, T19, T25, T26 |
| CA-05 | 5 (CL-08), 7 | 003b | T26 |
| CA-06 | 1.1, 4, 7 | 003a | T05, T06 |
| CA-07 | 4, 5 (CL-04), 7 | 003a | T05, T08 |
| CA-08 | 4 (tema), 7 | 003a | T06 |
| CA-09 | 4 (bloqueo), 7 | 003a | T05 |
| CA-10 | 4 (tiempos), 7 | 003a | T05 |
| CA-11 | 4, 7 | 003a | T05 |
| CA-12 | 4 (`documento.js`), 7 | 003a | T01, T06 |
| CA-13 | 4 (`UPDATE_PASSWORD`, política, aviso del número), 7 | 003a | T05, T07 |
| CA-14 | 3, 7 | 003a | T02, T04, T11 |
| CA-15 | 3, 5 (CL-17), 7 | 003a | T02, T04, T11 |
| CA-16 | 3, 7 | 003a | T02, T11 |
| CA-17 | 5 (CL-18), 7 | 003a | T02, T04, T11 |
| CA-18 | 5 (CL-07), 7 | 003a | T08, T10 |
| CA-19 | 5 (CL-08, CL-09), 7 | 003a | T09, T11 |
| CA-20 | 6 (RNF-05), 7 | 003a | T09 |
| CA-21 | 6 (RNF-01) | 003a | T08 |
| CA-22 | 6 (RNF-06), 8.1 | 003a | T12 |
| D-08 | ADR-0014 | 003a | T05, T14 |

## 10. Riesgos y preguntas abiertas

### Preguntas abiertas para el autor

Ninguna. Las tres de la primera versión del plan y la propuesta de alcance quedaron resueltas por el autor el 2026-10-13:

1. **OpenAPI:** se genera al compilar, se versiona en Git, el CI lo comprueba y no se expone en producción (ADR-0019, sección 8.1).
2. **CL-05:** pasa a ser un límite conocido y la spec quedó enmendada (ver "Límites conocidos"). Razón del autor: el riesgo principal es el personal interno, que ya está auditado, y el operador puede restablecer el acceso cuando haga falta; el impacto residual no justifica construir la caducidad (palabras completas en la sección 11 de la spec).
3. **"No contener el número de documento":** `notContainsUsername` en el servidor más un aviso en `documento.js` (sección 4, "Aviso del número de documento").
4. **Alcance:** una spec y un plan, y el Build en dos bloques con PR propio (sección 1.1).

### Límites conocidos (aceptados por el autor)

- **La clave temporal no vence (CL-05).** Keycloak 26 no lo permite. La clave sirve hasta que el operador restablece o deshabilita el acceso, y quien la obtuviera sin usarla podría inscribir su propio OTP. **Mejora futura:** un `BackgroundService` de la API que, cada cierto tiempo, liste los usuarios del realm `clientes` con `UPDATE_PASSWORD` pendiente y una clave de más de 72 h y los deshabilite con la misma intención primero (una solicitud a nombre del sistema). Iría junto al `BackgroundService` de reconciliación de ADR-0017.
- **"No contener el número de documento" solo se garantiza en parte.** El servidor rechaza la clave que contiene el usuario completo. Del número suelto avisa el navegador, y ese aviso se puede saltar. Se acepta porque la regla protege al usuario de sí mismo, no frente a un atacante.
- **La pantalla del OTP confirma que la clave era correcta** (riesgo 3).
- **Ventana de revocación de hasta 5 min** (RNF-01, ADR-0015).
- **El operador ve la clave temporal** (sección 11 de la spec).

### Riesgos

1. **Tamaño de la feature.** Son 22 CA, cuatro niveles de pruebas, un componente nuevo (Keycloak) con su configuración y su tema, y el patrón de consistencia. El autor decidió el Build en dos bloques (sección 1.1), y la 004 puede empezar en cuanto se mergee 003a. El riesgo que queda es que 003a, el bloque más grande, ocupe casi toda la primera semana del S3.
2. **La lógica del navegador se prueba fuera del navegador.** Jint comprueba `normalizarUsuario` (CA-12) y `contieneNumeroDeDocumento` (CL-06). El punta a punta comprueba que las páginas cargan el script y tienen los campos que necesita, y lo que hace el servidor. Ninguna prueba ejecuta el script *dentro* de un navegador. Playwright cerraría ese hueco a costa de descargar navegadores en el CI. Se deja como mejora.
3. **Límite conocido (hallazgo 4):** la pantalla del OTP confirma que la clave era correcta. Es inherente al login en dos pasos. Se documenta en el README. Lo mitiga el bloqueo, porque el OTP erróneo también cuenta.
4. **Ventana de gracia de la inactividad.** La documentación de Keycloak dice que la inactividad tiene una gracia de 2 min **cuando las sesiones persistentes no están activas**. En Keycloak 26 están activas por defecto y, además, el refresh token vence a los 5 min exactos. Aun así, CA-10 se comprueba con el refresh token. Hay que verificar en el Build que la cookie SSO tampoco reabre la sesión pasada la inactividad. Si lo hace, se documenta.
   - **Pendiente tras 003a:** las pruebas de CA-10 comprueban el refresh token, no la cookie SSO. La verificación de la cookie queda abierta: se hace en 003b o se documenta como límite conocido.
5. **Dos operadores habilitan a la vez al mismo cliente.** Las dos solicitudes quedan registradas, y la clave que vale es la de la última que llega a Keycloak (el otro operador tendrá una clave inútil). Si los dos crean el vínculo, el índice único da 409 a uno y su solicitud queda pendiente hasta el siguiente intento. No hay hueco de seguridad y todo deja rastro. Se acepta.
6. **Carrera de la reconciliación.** Si Keycloak termina de aplicar una petición *después* de que el reintento la cerró como Fallida, el registro diría Fallido de algo que se aplicó. Exige una petición de más de 10 s más un reintento inmediato, y la solicitud nueva sobrescribe el estado de todas formas. Los eventos de administración de Keycloak (`adminEventsEnabled`) dejan el rastro para investigarlo. Se acepta.
7. **Usuario borrado a mano en Keycloak con el vínculo vivo.** `LocalizadorDeAcceso` lanza `InvalidOperationException` (500) y lo registra en el log. Arreglarlo es manual (borrar el vínculo). No hay requisito que pida automatizarlo (YAGNI).
8. **Inyección del script en el tema `keycloak.v2`.** Si `scripts=` en `theme.properties` no basta, hay que sobrescribir `login.ftl`, que se acopla más a la versión de Keycloak. Por eso la imagen queda fijada en 26.3.3, la misma de los experimentos (la documentación ya va por 26.8). Actualizarla es una tarea `chore` aparte que vuelve a pasar Keycloak.Tests.
   - **Resultado del Build (003a):** `scripts=` sí basta en la pantalla de login (no se sobrescribió `login.ftl`). En cambio, sí se sobrescribió `login-update-password.ftl` (sección 4, "Aviso del número de documento"). Esa es la plantilla que hay que revisar al actualizar la imagen.
9. **Tiempo del CI.** El arranque de Keycloak (unos 188 s el primero) y las esperas de las pruebas de punta a punta (ventanas OTP de 30 s y tiempos cortos) suman varios minutos. Se empieza en el mismo job. Si molesta, se separa Keycloak.Tests en un job paralelo.
10. **El import se omite si el realm existe.** En local, cambiar un realm JSON exige borrar el volumen de Keycloak (README). Si falta `KC_CLIENTES_API_SECRET`, el marcador podría importarse literal. El README y `.env.example` lo advierten.
11. **HTTPS.** Las cookies `Secure` y `RequireHttpsMetadata` funcionan en local porque es Development. En producción hace falta HTTPS de punta a punta, y se decide con D-11.
12. **Para la 004 (aviso, no es de esta feature):**
    - la SPA en otro origen necesitará **CORS** en la API;
    - la ficha del cliente no muestra el estado del acceso digital. Si la pantalla del operador lo necesita, es un endpoint nuevo (un `GET` que consulte Keycloak).
13. **Pendientes de la revisión de 003a (sugerencias no aplicadas):**
    - **Emisor sin configurar** (para D-11): fuera de Development, si falta `Autenticacion:Clientes:Emisor` o `Autenticacion:Empleados:Emisor`, la API responde 401 a todo, con `firma-invalida` en el log. Falla cerrado, pero es difícil de diagnosticar. Remedio: `ValidateOnStart()` sobre `OpcionesDeAutenticacion`.
    - **`security` en el OpenAPI** (para 003b): `RequisitoDeSeguridadEnOpenApi` solo marca las rutas con `IAuthorizeData`. Una ruta protegida solo por la `FallbackPolicy` no aparecería como protegida en el contrato. Las rutas de 003b deben declarar su política.
    - **`admin-cli`** sigue con la concesión directa por defecto de Keycloak. Sus tokens no llevan la audiencia de la API y se rechazan, pero se podría desactivar.
14. **Mayúsculas en `src/CoreBancario.Api/`:** `OpenApi/` tiene los transformadores (`.cs`) y `openapi/` tiene el contrato JSON (ADR-0019). En Windows son la misma carpeta y en Linux dos. Git en Windows tiende a indexar el JSON bajo `OpenApi/`, y entonces el paso del CI `git diff --exit-code -- src/CoreBancario.Api/openapi` no compararía nada. En 003a se forzó la ruta en minúscula en el índice. Mejora a decidir: renombrar una de las dos carpetas para que no difieran solo en las mayúsculas.

### Para estudiar antes o durante el Build

- **OAuth 2.0 con código de autorización + PKCE**, y por qué la concesión directa (*password grant*) está desaconsejada.
- **JWKS y la rotación de claves con `kid`:** cómo valida la API sin preguntarle a Keycloak.
- **BOLA/IDOR (OWASP API1):** por qué "cargar y luego comprobar" es frágil y "filtrar en la consulta" no.
- **Consistencia entre dos sistemas sin transacción común:** intención primero, outbox y compensación. Es la antesala de las sagas de la fase 3.
- **Esquemas de autenticación en ASP.NET Core:** `AddPolicyScheme`, `ForwardDefaultSelector` y la diferencia entre `Challenge` (401) y `Forbid` (403).
