# Tareas — 003 Autenticación y autorización en la API

Plan: [plan.md](plan.md)

El Build va en **dos bloques secuenciales** (sección 1.1 del plan). Cada uno tiene su commit de pruebas en rojo, su `/sdd-review` y su PR, y cada uno compila, pasa sus pruebas y se puede mergear por sí solo. Dentro de cada bloque, el orden es de adentro hacia afuera: Domain → Application → Infrastructure → configuración de Keycloak → Api. Cada tarea compila por sí sola. El coder marca `[x]` al terminar cada una.

**Ramas:**

- **`feature/003a-proteger-api`** nace de la actual `feature/003-auth-api` (renombrarla o crear una nueva desde ella) y lleva la spec, el plan, las ADRs y el bloque 003a.
- **`feature/003b-gestionar-acceso`** nace de `main` **después** de mergear 003a.

Cada tarea lista los IDs que cubre. Una prueba de API o de punta a punta de un CA solo puede quedar en verde cuando existe la tarea de Api correspondiente.

---

# Bloque 003a — Proteger la API

**Cubre:** CA-06…CA-22; RF-05 (cliente), RF-14, RF-15; RN-12, RN-17, RN-19, RN-21…RN-24; CL-01…CL-04, CL-06…CL-11, CL-17, CL-18; RNF-01, RNF-02, RNF-04, RNF-05 y RNF-06 (operaciones de la 002 y de "mis cuentas"), RNF-08; D-08. CL-05 es un límite conocido sin prueba (se documenta en T13).

**Antes de T01 (test-writer, commit `test(003): pruebas en rojo del bloque 003a`):**

- Escribe las pruebas del bloque 003a (plan, sección 7, "Pruebas por bloque") contra esqueletos de las firmas de la sección 3 (`// SDD: esqueleto creado por test-writer`). Los esqueletos que heredan de tipos de ASP.NET Core (`EventosDeJwt`, los transformadores de OpenAPI) necesitan ya sus paquetes.
- Crea `tests/CoreBancario.Keycloak.Tests` (xUnit v3 con MTP, **sin** `--ignore-exit-code 8`) y lo añade a `CoreBancario.slnx`. Su fixture levanta:
  - Keycloak 26.3.3, que importa los realm JSON de `infra/keycloak/realms`, monta los temas y ajusta los tiempos cortos con el admin de `master`;
  - PostgreSQL;
  - la API con `WebApplicationFactory`;
  - el navegador simulado: cookies manuales, AngleSharp y Otp.NET.

  **Usuarios sin RF-13:** el fixture crea al operador en `empleados` y a cada cliente en `clientes` con la Admin REST API del admin de `master`. El cliente lleva `username = AccesoDigital.NombreDeUsuarioDe(documento)`, una clave temporal y las acciones `UPDATE_PASSWORD` y `CONFIGURE_TOTP`. El vínculo `accesos_digitales` se inserta directamente en la base. Para CA-08, el fixture deshabilita un usuario con la misma Admin API (plan, sección 1.1).
- Adapta `ApiFixture`:
  - dos emisores RSA propios con `Configure<JwtBearerOptions>(esquema, …)`;
  - `CrearCliente()` con token de Operador;
  - `CrearClienteAnonimo()`, `CrearClienteComoCliente(sub)` y `CrearClienteConToken(token)`;
  - `EmisorDePruebas.CrearToken(…)`;
  - un proveedor de logs en memoria.
- En `F002_RNF05_ContratoOpenApi_…`, añade 401, 403 y 500 a cada fila y las filas de `ListarMisCuentas` y `ObtenerMiCuenta`. El resto de pruebas de la 002 no cambia.
- En rojo, las pruebas de configuración y las de Keycloak.Tests fallan porque aún no existen los archivos de `infra/keycloak/` (T05, T06), y las de Infrastructure y Api porque falta la migración (T03). Es lo esperado.

## Domain

- [x] T01 — `AccesoDigital` (`ClienteId`, `Sub`, `FechaDeVinculo`), con `Vincular`, sus guardas y `NombreDeUsuarioDe(Documento)` (`$"{Tipo}{Numero}"`). Sin paquetes: `ArquitecturaDelDominioTests` sigue en verde · Capa: Domain · Archivos: `src/CoreBancario.Domain/AccesosDigitales/AccesoDigital.cs` · Cubre: RN-17, CL-02, CA-12 (nombre de usuario), ADR-0016

## Application

- [x] T02 — Mis cuentas:
  - `ICuentasDelTitular`, `ListarMisCuentasConsulta`/`ListarMisCuentasHandler` y `ObtenerMiCuentaConsulta`/`ObtenerMiCuentaHandler`;
  - el 404 lleva **exactamente** el mensaje de `ObtenerCuentaHandler`, con una constante `internal` compartida en `Cuentas/`;
  - `AddAplicacion` registra los dos handlers como Scoped.

  Capa: Application · Archivos: `src/CoreBancario.Application/Cuentas/{ICuentasDelTitular,ListarMisCuentasConsulta,ListarMisCuentasHandler,ObtenerMiCuentaConsulta,ObtenerMiCuentaHandler}.cs`, `src/CoreBancario.Application/Cuentas/ObtenerCuentaHandler.cs` (solo la constante), `src/CoreBancario.Application/DependencyInjection.cs` · Cubre: RF-05, RN-12, CA-14, CA-15, CA-16, CA-17, CL-17, CL-18

## Infrastructure

- [x] T03 — Vínculo en la base:
  - `AccesoDigitalConfiguracion`: PK `pk_accesos_digitales` sobre `cliente_id`, FK `Restrict` a `clientes`, `sub` `varchar(255)` con `ux_accesos_digitales_sub` y `fecha_de_vinculo`;
  - `DbSet<AccesoDigital> AccesosDigitales` en `CoreBancarioDbContext` (la traducción de errores llega en 003b, cuando la API escriba la tabla);
  - migración `VinculoDeAccesoDigital` (`dotnet ef migrations add VinculoDeAccesoDigital …`), seguida de `dotnet format`.

  Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Persistencia/Configuraciones/AccesoDigitalConfiguracion.cs`, `src/CoreBancario.Infrastructure/Persistencia/CoreBancarioDbContext.cs`, `src/CoreBancario.Infrastructure/Persistencia/Migraciones/*_VinculoDeAccesoDigital*.cs`, `CoreBancarioDbContextModelSnapshot.cs` · Cubre: RN-12, CA-14 (esquema), ADR-0016 · Pruebas en verde: `F003_RNF02_BaseVacia_TieneLasMigracionesDeLa003`

- [x] T04 — `CuentasDelTitular`: `AsNoTracking`, filtro de dueño **dentro** de la consulta (`EXISTS` sobre `accesos_digitales` por `sub`) y orden por `FechaApertura` y después `Id`. Se registra como Scoped en `AddInfraestructura` · Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Persistencia/Repositorios/CuentasDelTitular.cs`, `src/CoreBancario.Infrastructure/DependencyInjection.cs` · Cubre: RF-05, RN-12, CA-14, CA-15, CA-17, CL-17, CL-18

## Configuración de Keycloak (archivos del repo)

- [x] T05 — Realms `infra/keycloak/realms/clientes-realm.json` y `empleados-realm.json`, con los ajustes de la tabla "Ajustes de los dos realms" del plan **salvo** el cliente `core-bancario-api-admin` y el atributo `ultima_solicitud_de_acceso`, que llegan en 003b (T24). Incluye:
  - tiempos, bloqueo, rotación de refresh tokens, flujo de navegador con OTP `REQUIRED`, acciones obligatorias y `max_auth_age = 0`;
  - `passwordPolicy` con `notContainsUsername()`;
  - roles por defecto sin `offline_access`, `manage-account` ni `view-profile`;
  - cliente `core-bancario-web` (PKCE S256, sin concesión directa, scopes `basic` y `roles`, mapper de audiencia);
  - perfil de usuario sin exigir correo ni nombres;
  - eventos activados y `loginTheme`.

  Conviene exportar un realm configurado en la consola de administración y depurarlo a mano. Al terminar, confirmar en local que un usuario creado con solo `username` no recibe `VERIFY_PROFILE` · Capa: Configuración de Keycloak · Archivos: `infra/keycloak/realms/clientes-realm.json`, `infra/keycloak/realms/empleados-realm.json` · Cubre: RF-14, RF-15, RN-21, RN-22, RN-23, RN-24, RNF-01, RNF-08, CL-03, CL-04, CL-06, CL-10, CL-11, CA-06, CA-07, CA-09, CA-10, CA-11, CA-13, D-08 · Pruebas en verde: `F003_RN22_…`, `F003_RN23_…`, `F003_RN24_…` y `F003_RN21_…`, y `F003_RNF01_…` junto con T08

- [x] T06 — Temas (normalización) y entorno local:
  - `core-bancario` (`parent=keycloak.v2`, `messages_es.properties` con el mensaje genérico en las claves de CA-08);
  - `core-bancario-clientes` (`parent=core-bancario`, `scripts=js/documento.js`, etiqueta "Número de documento"), con `documento.js`: función pura `normalizarUsuario(tipo, numero)`, el selector de tipo y la escritura de `username` al enviar el login;
  - servicio `keycloak` en `docker-compose.yml`: imagen `quay.io/keycloak/keycloak:26.3.3`, `start-dev --import-realm`, montajes de `realms/` y de cada tema, volumen de datos, puerto `${KEYCLOAK_PORT:-8080}`, `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD`;
  - `.env.example` con esas tres variables, sin valores reales.

  Si `scripts=` no inyecta el script con `keycloak.v2`, se sobrescribe `login.ftl` (riesgo 8 del plan) · Capa: Configuración de Keycloak · Archivos: `infra/keycloak/themes/core-bancario/login/**`, `infra/keycloak/themes/core-bancario-clientes/login/**`, `docker-compose.yml`, `.env.example` · Cubre: RF-14, RN-17, RNF-08, CL-01, CL-02, CA-06, CA-08, CA-12

- [x] T07 — Aviso del número de documento (decisión del autor, pregunta 3; plan, sección 4, "Aviso del número de documento"):
  - en `documento.js`, la función pura `contieneNumeroDeDocumento(usuario, clave)`;
  - en la pantalla de cambio de clave de `clientes` (primer ingreso y `kc_action=UPDATE_PASSWORD`), el script lee el usuario del campo `username` oculto de la plantilla, cancela el envío si la clave nueva contiene el número y muestra "La clave no puede contener tu número de documento.";
  - si el campo no existe en la plantilla de `keycloak.v2`, se sobrescribe `login-update-password.ftl` para exponer el usuario en un atributo `data-`.

  Comentario en el script: el aviso se puede saltar, y se acepta porque la regla protege al usuario de sí mismo · Capa: Configuración de Keycloak · Archivos: `infra/keycloak/themes/core-bancario-clientes/login/resources/js/documento.js`, `infra/keycloak/themes/core-bancario-clientes/login/messages/messages_es.properties`, y `login-update-password.ftl` solo si hace falta · Cubre: RF-15, CL-06, CA-13 · Pruebas en verde: `F003_CL06_ContieneNumeroDeDocumento_DetectaElNumeroSuelto` (Jint), `F003_CL06_PaginaDeCambioDeClave_CargaElScriptYTraeElUsuario`, `F003_CL06_ClaveNuevaConElUsuarioCompleto_ElServidorLaRechaza`, `F003_CL06_ClaveNuevaConSoloElNumero_ElServidorLaAcepta`

## Api

- [x] T08 — Autenticación:
  - `EsquemasDeAutenticacion`, `Politicas`, `Roles`, `OpcionesDeAutenticacion` (con `ToleranciaDeReloj` constante de 30 s y `AlgoritmoPermitido`) y `OpcionesDeEmisor`;
  - `SelectorDeEsquema.Elegir`;
  - `EventosDeJwt` (sub obligatorio, rol según el esquema, 401 y 403 con `ProblemDetails` y `WWW-Authenticate: Bearer` sin detalles, y log del motivo sin el token);
  - `MotivoDeRechazo`, `TokenSinSujetoException` y `ClaimsPrincipalExtensiones.Sub()`;
  - `ConfiguracionDeAutenticacion.AddAutenticacionConKeycloak`: policy scheme, dos `AddJwtBearer` con los parámetros del plan, políticas y `FallbackPolicy`;
  - `CodigosDeError`: `NoAutenticado = "no-autenticado"` y `Prohibido = "prohibido"`;
  - paquete `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12.

  Se cablea en T10 · Capa: Api · Archivos: `src/CoreBancario.Api/Autenticacion/{EsquemasDeAutenticacion,Politicas,Roles,OpcionesDeAutenticacion,SelectorDeEsquema,EventosDeJwt,MotivoDeRechazo,TokenSinSujetoException,ClaimsPrincipalExtensiones,ConfiguracionDeAutenticacion}.cs`, `src/CoreBancario.Api/Errores/CodigosDeError.cs`, `src/CoreBancario.Api/CoreBancario.Api.csproj` · Cubre: RN-19, RNF-01, RNF-02, RNF-03, RNF-04, CL-07, CA-07 (401 sin token), CA-18, CA-21

- [x] T09 — Proteger la 002:
  - `DocumentacionDeErrores.DocumentarErroresComunes` (401, 403 y 500 con `ProblemDetails`);
  - `RegistroDeOperacionDelOperador : IEndpointFilter`;
  - `MapEndpointsDeClientes` y `MapEndpointsDeCuentas` declaran sus rutas, iguales a las de la 002, en un grupo con `RequireAuthorization(Politicas.Operador)` y `DocumentarErroresComunes()`;
  - el filtro va en `RegistrarCliente`, `AbrirCuenta`, `BloquearCuenta`, `DesbloquearCuenta` y `CerrarCuenta`.

  Capa: Api · Archivos: `src/CoreBancario.Api/OpenApi/DocumentacionDeErrores.cs`, `src/CoreBancario.Api/Autenticacion/RegistroDeOperacionDelOperador.cs`, `src/CoreBancario.Api/Clientes/EndpointsDeClientes.cs`, `src/CoreBancario.Api/Cuentas/EndpointsDeCuentas.cs` · Cubre: RN-19, RNF-05, RNF-06, CL-08, CA-19, CA-20

- [x] T10 — `Program.cs`:
  - `AddAutenticacionConKeycloak(builder.Configuration, builder.Environment)`;
  - `UseAuthentication()` y `UseAuthorization()` después de `UseExceptionHandler()`;
  - `/health` con `AllowAnonymous()`, y `MapOpenApi()` solo en Development con `AllowAnonymous()`.

  `appsettings.Development.json` lleva la sección `Autenticacion` del plan y `appsettings.json` solo `Autenticacion:Audiencia` · Capa: Api · Archivos: `src/CoreBancario.Api/Program.cs`, `src/CoreBancario.Api/appsettings.json`, `src/CoreBancario.Api/appsettings.Development.json` · Cubre: RNF-08, CA-18 · Pruebas en verde: todas las de la 002 en Api.Tests (con el token de operador del fixture), `F003_CA18_…`, `F003_CA19_ClienteLlamaOperacionDelOperador_…`, `F003_CA20_…`, `F003_CA21_…`, `F003_RNF04_…`, y las de punta a punta de login (CA-07…CA-13) junto con T05 y T06. CA-06 necesita además T11, porque llama a `/mis-cuentas`

- [x] T11 — `EndpointsDeMisCuentas`:
  - grupo `/mis-cuentas` con `RequireAuthorization(Politicas.Cliente)` y `DocumentarErroresComunes`;
  - `GET /mis-cuentas` (`ListarMisCuentas`) y `GET /mis-cuentas/{cuentaId:guid}` (`ObtenerMiCuenta`), con el sub de `usuario.Sub()`;
  - en `Program.cs`, `MapEndpointsDeMisCuentas()`.

  Capa: Api · Archivos: `src/CoreBancario.Api/Cuentas/EndpointsDeMisCuentas.cs`, `src/CoreBancario.Api/Program.cs` · Cubre: RF-05, RN-12, RN-19, RNF-02, CL-09, CL-17, CL-18, CA-14, CA-15, CA-16, CA-17, CA-19

- [x] T12 — OpenAPI (ADR-0019, decidido):
  - `EsquemaDeSeguridadEnOpenApi` y `RequisitoDeSeguridadEnOpenApi`, registrados en `AddOpenApi`;
  - paquete `Microsoft.Extensions.ApiDescription.Server` 10.0.12, y en el `.csproj` `OpenApiDocumentsDirectory=openapi` y `--file-name core-bancario-api`;
  - el archivo `src/CoreBancario.Api/openapi/core-bancario-api.json` se versiona en Git;
  - en `ci.yml`, un paso tras compilar: `git diff --exit-code -- src/CoreBancario.Api/openapi`;
  - `MapOpenApi()` sigue solo en Development;
  - si la generación al compilar falla por la configuración o por la migración de Development, se aplica el patrón `GetDocument.Insider`.

  Capa: Api · Archivos: `src/CoreBancario.Api/OpenApi/{EsquemaDeSeguridadEnOpenApi,RequisitoDeSeguridadEnOpenApi}.cs`, `src/CoreBancario.Api/Program.cs`, `src/CoreBancario.Api/CoreBancario.Api.csproj`, `src/CoreBancario.Api/openapi/core-bancario-api.json`, `.github/workflows/ci.yml` · Cubre: RNF-06, CA-22 · Pruebas en verde: `F003_CA22_…`, `F002_RNF05_ContratoOpenApi_…` adaptada

## Cierre del bloque 003a

- [x] T13 — README, sección "Autenticación con Keycloak":
  - cómo levantar Keycloak (`docker compose up -d`) y entrar a la consola;
  - cómo crear un operador en `empleados` (rol `operador`) y, **mientras no exista 003b**, un cliente de prueba a mano (usuario `CC…`, clave temporal y vínculo en `accesos_digitales`);
  - cómo recargar un realm modificado (borrar el volumen de Keycloak);
  - límites conocidos: la clave temporal **no vence** (CL-05), con la mejora futura; la pantalla del OTP confirma la clave; la ventana de revocación de 5 min; el aviso del número de documento se puede saltar; el operador ve la clave temporal.

  Capa: transversal · Archivos: `README.md` · Cubre: RNF-03 (documentación), CL-05 (límite documentado)

- [x] T14 — Verificación y documentación del bloque:
  - verificación: `dotnet format --verify-no-changes`, `dotnet build -c Release -warnaserror` (el contrato queda regenerado y versionado), `dotnet test` en verde con Docker en marcha (incluido `CoreBancario.Keycloak.Tests`), CI en verde en el PR y `/sdd-review` del bloque;
  - comprobaciones: ningún log ni respuesta contiene un token;
  - en `docs/producto/spec-fase-1.md`, "Resueltas": añadir "D-08 → ADR-0014";
  - en `CLAUDE.md`: quitar D-08 de "Pendiente de ADR"; en "Estructura", añadir `infra/keycloak/` y `tests/CoreBancario.Keycloak.Tests/` (pruebas contra Keycloak real) como excepción a "un proyecto por capa"; en "Comandos", anotar que `docker compose up -d` levanta PostgreSQL y Keycloak.

  Capa: transversal · Archivos: `docs/producto/spec-fase-1.md`, `CLAUDE.md` (solo esas líneas) · Cubre: RNF-07, D-08

---

# Bloque 003b — Gestionar el acceso

**Cubre:** CA-01…CA-05; RF-13; RN-20; CL-12…CL-16; RNF-03 (clave temporal y secreto de servicio) y RNF-05 y RNF-06 para las operaciones de acceso; RNF-08 (flujo completo con habilitar).

**Antes de T15 (test-writer, commit `test(003): pruebas en rojo del bloque 003b`, en `feature/003b-gestionar-acceso`):**

- Escribe las pruebas del bloque 003b (plan, sección 7, "Pruebas por bloque"), incluidas las de "intención primero" en Application y en Api, y las del adaptador en Keycloak.Tests.
- Añade los fakes de Application.Tests (`ProveedorDeIdentidadEnMemoria` con `ResponderConError`, `NoResponder` y `AplicarYNoResponder`; `AccesoDigitalRepositorioEnMemoria`, `RegistroDeAccesosEnMemoria` y `GeneradorDeClaveDeSecuencia`) y el fake de `IProveedorDeIdentidad` en `ApiFixture`.
- Amplía `F002_RNF05_ContratoOpenApi_…` con `HabilitarAccesoDigital` y `DeshabilitarAccesoDigital`, y `F003_RNF02_BaseVacia_…` con la migración `RegistroDeAccesos`.
- Añade `F003_RNF08_FlujoCompleto_OperadorHabilitaYClienteConsultaSusCuentas`: el cliente se crea con el endpoint de habilitar, no con el admin de `master`.

## Domain

- [ ] T15 — Registro de accesos:
  - `AccionDeAcceso`, `EstadoDeAcceso` y `ResultadoDeAcceso`;
  - `AccesoNoHabilitadoException : ReglaDeNegocioException`, con `EstadoActual`;
  - `SolicitudDeAcceso`: `ParaHabilitar` (Habilitado → Restablecer; si no, Habilitar), `ParaDeshabilitar` (si el estado no es Habilitado, lanza **antes** de construir), `Completar`, `Fallar`, guardas de operador y motivo, e `Id = Guid.CreateVersion7(fecha)`;
  - `ResultadoDeSolicitud`, con constructor `internal` para `SolicitudDeAcceso` y uno privado para EF.

  Capa: Domain · Archivos: `src/CoreBancario.Domain/AccesosDigitales/{AccionDeAcceso,EstadoDeAcceso,ResultadoDeAcceso,AccesoNoHabilitadoException,SolicitudDeAcceso,ResultadoDeSolicitud}.cs` · Cubre: RN-20, CL-13, CL-14, CL-16, CA-01, CA-04

## Application

- [ ] T16 — Tipos y puertos:
  - `ClaveTemporal`, con `ToString` oculto y un mínimo de 16 caracteres;
  - `IGeneradorDeClaveTemporal`, `UsuarioDeIdentidad`, `IProveedorDeIdentidad`, `IAccesoDigitalRepositorio` e `IRegistroDeAccesos`;
  - las dos excepciones del proveedor, con mensajes fijos y `Motivo`;
  - `HabilitarAccesoComando`, `DeshabilitarAccesoComando` y `RegistroDeAccesoDto`;
  - `AccesoHabilitadoDto`, con `ToString` sin la clave.

  Capa: Application · Archivos: `src/CoreBancario.Application/AccesosDigitales/{ClaveTemporal,IGeneradorDeClaveTemporal,UsuarioDeIdentidad,IProveedorDeIdentidad,IAccesoDigitalRepositorio,IRegistroDeAccesos,ProveedorDeIdentidadSinRespuestaException,ErrorDelProveedorDeIdentidadException,HabilitarAccesoComando,DeshabilitarAccesoComando,AccesoHabilitadoDto,RegistroDeAccesoDto}.cs` · Cubre: RF-13, RNF-03, CA-01

- [ ] T17 — `AccesoLocalizado`, `LocalizadorDeAcceso` y `ReconciliadorDeSolicitudes`:
  - el localizador busca por el sub del vínculo o, si no hay vínculo, por el nombre de usuario (adopción); si hay vínculo pero el usuario no existe en el proveedor, lanza `InvalidOperationException`;
  - el reconciliador da Completado si la marca coincide y Fallido si no, con un solo guardado y solo si hubo pendientes.

  Capa: Application · Archivos: `src/CoreBancario.Application/AccesosDigitales/{AccesoLocalizado,LocalizadorDeAcceso,ReconciliadorDeSolicitudes}.cs` · Cubre: RF-13, RN-20 (reconciliación (a) de ADR-0017)

- [ ] T18 — `HabilitarAccesoHandler` con la secuencia exacta de la sección 5 del plan:
  - la solicitud se confirma **antes** de llamar al proveedor;
  - desde ese punto se usa `CancellationToken.None`;
  - un error del proveedor escribe Fallido y propaga; sin respuesta, no escribe resultado y propaga;
  - si todo va bien, escribe el vínculo (si faltaba) y Completado en un solo guardado.

  Capa: Application · Archivos: `src/CoreBancario.Application/AccesosDigitales/HabilitarAccesoHandler.cs` · Cubre: RF-13, RN-20, CA-01, CA-02, CA-04, CL-12, CL-13, CL-16

- [ ] T19 — `DeshabilitarAccesoHandler`: la misma secuencia, con `ParaDeshabilitar` (CL-14 sin registrar nada) y `DeshabilitarAsync`. `AddAplicacion` registra como Scoped `LocalizadorDeAcceso`, `ReconciliadorDeSolicitudes` y los dos handlers · Capa: Application · Archivos: `src/CoreBancario.Application/AccesosDigitales/DeshabilitarAccesoHandler.cs`, `src/CoreBancario.Application/DependencyInjection.cs` · Cubre: RF-13, RN-20, CA-03, CA-04, CL-14

## Infrastructure

- [ ] T20 — Configuraciones de EF Core y traducción de errores:
  - `SolicitudDeAccesoConfiguracion`: `accion` como texto con `ck_solicitudes_de_acceso_accion`, FK `Restrict` e índice por `cliente_id`;
  - `ResultadoDeSolicitudConfiguracion`: PK `pk_resultados_de_acceso` sobre `solicitud_id`, FK `Restrict`, `resultado` con su CHECK y `motivo` `varchar(200)`;
  - dos `DbSet` en `CoreBancarioDbContext`;
  - `GuardarCambiosAsync` traduce a `ConflictoDeConcurrenciaException` las violaciones `23505` de `pk_accesos_digitales`, `ux_accesos_digitales_sub` y `pk_resultados_de_acceso`.

  Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Persistencia/Configuraciones/{SolicitudDeAccesoConfiguracion,ResultadoDeSolicitudConfiguracion}.cs`, `src/CoreBancario.Infrastructure/Persistencia/CoreBancarioDbContext.cs` · Cubre: RF-13, RN-20, CA-01

- [ ] T21 — Migración `RegistroDeAccesos` (`dotnet ef migrations add RegistroDeAccesos …`):
  - a mano, en `Up`: la función `impedir_cambios_en_registro_de_accesos()` y los dos triggers `BEFORE UPDATE OR DELETE`; en `Down`, su eliminación;
  - revisar el SQL con `dotnet ef migrations script` y después ejecutar `dotnet format`.

  Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Persistencia/Migraciones/*_RegistroDeAccesos*.cs`, `CoreBancarioDbContextModelSnapshot.cs` · Cubre: RN-20, CA-01 · Pruebas en verde: `F003_RNF02_BaseVacia_…` ampliada, `F003_RN20_EditarOBorrarElRegistro_LoRechazaLaBase`, `F003_RN20_DosResultadosParaLaMismaSolicitud_LanzaConflicto`, `F003_ADR0016_DosClientesConElMismoSub_LanzaConflicto`

- [ ] T22 — Repositorios y generador:
  - `AccesoDigitalRepositorio`;
  - `RegistroDeAccesos`, cuyo `ListarPendientesAsync` busca solicitudes del cliente sin resultado, ordenadas por fecha;
  - `GeneradorAleatorioDeClaveTemporal` (`RandomNumberGenerator.GetString(Alfabeto, 16)`);
  - registros en `AddInfraestructura`: los repositorios como Scoped y el generador como Singleton.

  Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Persistencia/Repositorios/{AccesoDigitalRepositorio,RegistroDeAccesos}.cs`, `src/CoreBancario.Infrastructure/AccesosDigitales/GeneradorAleatorioDeClaveTemporal.cs`, `src/CoreBancario.Infrastructure/DependencyInjection.cs` · Cubre: RF-13, RN-20, RNF-03, CA-01, CA-02

- [ ] T23 — Adaptador de Keycloak:
  - `OpcionesDeKeycloak`;
  - `TokenDeServicioHandler`: client credentials con un `HttpClient` con nombre y caché hasta 30 s antes de vencer;
  - `ProveedorDeIdentidadKeycloak`, con las peticiones y su orden de la tabla "Llamadas a Keycloak" del plan (marca en el último paso, `PUT` con la representación completa) y la traducción de errores;
  - en `AddInfraestructura`: `AddOptions<OpcionesDeKeycloak>().Bind(…)` y `AddHttpClient<IProveedorDeIdentidad, ProveedorDeIdentidadKeycloak>` con `BaseAddress`, `Timeout` y el handler;
  - paquetes `Microsoft.Extensions.Http` y `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12.

  Capa: Infrastructure · Archivos: `src/CoreBancario.Infrastructure/Identidad/{OpcionesDeKeycloak,TokenDeServicioHandler,ProveedorDeIdentidadKeycloak}.cs`, `src/CoreBancario.Infrastructure/DependencyInjection.cs`, `src/CoreBancario.Infrastructure/CoreBancario.Infrastructure.csproj` · Cubre: RF-13, RN-21, RN-24 (acciones obligatorias), RNF-03, CA-01, CA-02, CA-03, CL-13, CL-15, CL-16 · Pruebas en verde: las de traducción de errores en Infrastructure.Tests; las del adaptador en Keycloak.Tests junto con T24

## Configuración de Keycloak

- [ ] T24 — En `clientes-realm.json`:
  - cliente confidencial `core-bancario-api-admin` (solo cuenta de servicio, `secret: "${KC_CLIENTES_API_SECRET}"`, roles de `realm-management` `manage-users` y `view-users`);
  - atributo `ultima_solicitud_de_acceso` en el perfil de usuario, con ver y editar solo para `admin`.

  En `docker-compose.yml`, la variable `KC_CLIENTES_API_SECRET` del servicio `keycloak`. En `.env.example`, la misma variable sin valor y un comentario: el mismo secreto va en user-secrets de la API (`Keycloak:Administracion:ClientSecret`). El fixture de Keycloak.Tests le da un valor aleatorio · Capa: Configuración de Keycloak · Archivos: `infra/keycloak/realms/clientes-realm.json`, `docker-compose.yml`, `.env.example` · Cubre: RF-13, RNF-03, CA-01, ADR-0014 (menor privilegio) · Pruebas en verde: `F003_ADR0014_CuentaDeServicio_SoloTieneManageUsersYViewUsers`, `F003_RNF03_RealmClientes_NoContieneElSecretoDeServicio`, `F003_ADR0014_CuentaDeServicio_NoPuedeModificarElRealm`

## Api

- [ ] T25 — Catálogo de errores:
  - `CodigosDeError`: `AccesoNoHabilitado = "acceso-no-habilitado"`, `IdentidadError = "identidad-error"` e `IdentidadSinRespuesta = "identidad-sin-respuesta"`;
  - `CatalogoDeErrores.Clasificar`: `AccesoNoHabilitadoException` → 422; `ErrorDelProveedorDeIdentidadException` → 502 "Error del sistema de identidad"; `ProveedorDeIdentidadSinRespuestaException` → 503 "Sistema de identidad no disponible".

  `ManejadorDeErrores` no cambia · Capa: Api · Archivos: `src/CoreBancario.Api/Errores/CodigosDeError.cs`, `src/CoreBancario.Api/Errores/CatalogoDeErrores.cs` · Cubre: RF-13, RNF-02, CL-14, CA-04 · Pruebas en verde: `F002_RNF03_CadaReglaDeNegocioDelDominio_TieneCodigoPropio`, `F003_RNF02_CatalogoDeErrores_ClasificaLosErroresNuevos`

- [ ] T26 — `EndpointsDeAccesoDigital`:
  - grupo de Operador con `RegistroDeOperacionDelOperador` y `DocumentarErroresComunes`;
  - `POST /clientes/{clienteId:guid}/acceso-digital/habilitar` (200 + `Cache-Control: no-store`) y `…/deshabilitar` (200), con `WithName`, `WithSummary` y `ProducesProblem` según la tabla de endpoints;
  - el operador sale de `usuario.Sub()`;
  - en `Program.cs`, `MapEndpointsDeAccesoDigital()`;
  - en `appsettings.Development.json`, la sección `Keycloak:Administracion` del plan (sin secreto);
  - regenerar y versionar `src/CoreBancario.Api/openapi/core-bancario-api.json`.

  Capa: Api · Archivos: `src/CoreBancario.Api/AccesosDigitales/EndpointsDeAccesoDigital.cs`, `src/CoreBancario.Api/Program.cs`, `src/CoreBancario.Api/appsettings.Development.json`, `src/CoreBancario.Api/openapi/core-bancario-api.json` · Cubre: RF-13, RN-19, RN-20, RNF-03, RNF-05, RNF-06, RNF-08, CL-08, CL-12, CA-01, CA-02, CA-03, CA-04, CA-05

## Cierre del bloque 003b

- [ ] T27 — README y verificación final:
  - en el README (sección de Keycloak):
    - cómo poner el secreto de servicio en `.env` y en user-secrets;
    - que el cliente de prueba ya se habilita desde la API (y quitar la receta manual de T13);
    - la consulta SQL de "solicitudes sin resultado" (plan, sección 4; reconciliación (b));
    - los códigos 502 y 503 y qué debe hacer el operador.
  - verificación: `dotnet format --verify-no-changes`, `dotnet build -c Release -warnaserror`, `dotnet test` en verde con Docker, CI en verde en el PR y `/sdd-review` del bloque;
  - comprobaciones: ningún log ni respuesta contiene una clave temporal ni el secreto, y gitleaks no detecta nada.

  Capa: transversal · Archivos: `README.md` · Cubre: RN-20 (reconciliación (b)), RNF-03, RNF-07
