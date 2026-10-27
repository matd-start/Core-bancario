# Core Bancario

Core bancario simplificado en .NET: cuentas en COP y USD, transferencias internas que no se duplican y saldos que se mantienen consistentes bajo concurrencia. Proyecto de portafolio construido para **defender decisiones**: cada regla de negocio tiene una prueba, cada decisión técnica un ADR y cada escenario de falla una demostración automatizada.

> 🚧 En construcción. Estado actual: **Sprint 0 — Fundaciones**. Ver el [roadmap](docs/producto/roadmap.md).

## Qué demuestra

Al terminar la fase 1, este repositorio responde con código y pruebas:

- Por qué el saldo nunca queda negativo aunque lleguen dos transferencias al mismo tiempo.
- Por qué reintentar una transferencia no la ejecuta dos veces.
- Cómo se convierte dinero entre COP y USD sin perder centavos ni reescribir la historia.
- Qué alternativas se descartaron en cada decisión y por qué.

## Contexto del sistema (C4 nivel 1)

```mermaid
flowchart TB
    cliente["👤 Cliente<br/><small>Consulta sus cuentas y extractos,<br/>transfiere dinero</small>"]
    operador["👤 Operador de backoffice<br/><small>Registra clientes y cuentas,<br/>depósitos, retiros y tasas</small>"]

    web["🖥️ Core Bancario Web<br/><small>React + TypeScript<br/>(repositorio core-bancario-web)</small>"]
    core["🏦 Core Bancario API<br/><small>.NET · este repositorio<br/>Cuentas, movimientos, transferencias</small>"]
    externo["🏛️ Banco externo<br/><small>Transferencias interbancarias<br/>(fase 3, simulado)</small>"]

    cliente -- "Usa" --> web
    operador -- "Usa" --> web
    web -- "HTTPS / JSON (OpenAPI)" --> core
    core -. "Mensajería asíncrona (fase 3)" .-> externo

    classDef person fill:#08427b,color:#fff,stroke:#052e56
    classDef system fill:#1168bd,color:#fff,stroke:#0b4884
    classDef external fill:#999,color:#fff,stroke:#6b6b6b
    class cliente,operador person
    class web,core system
    class externo external
```

## Stack y decisiones

| Tema | Decisión | ADR |
|---|---|---|
| Proceso | Spec-Driven Development con agentes de Claude Code | [0001](docs/adr/0001-guiar-el-desarrollo-con-sdd.md) |
| Arquitectura | Clean Architecture | [0002](docs/adr/0002-clean-architecture-para-el-backend.md) |
| Base de datos | PostgreSQL | [0003](docs/adr/0003-postgresql-como-base-de-datos.md) |
| Acceso a datos | EF Core para escribir, Dapper para leer | [0004](docs/adr/0004-ef-core-para-escritura-y-dapper-para-lectura.md) |
| Repositorios | Backend y frontend separados | [0005](docs/adr/0005-repositorios-separados-backend-y-frontend.md) |
| Pruebas | xUnit v3 con Microsoft Testing Platform | [0006](docs/adr/0006-xunit-v3-con-microsoft-testing-platform.md) |

.NET 10 · xUnit v3 · Testcontainers · Docker · GitHub Actions.

## Cómo ejecutarlo

Requisitos: .NET 10 SDK y Docker.

```bash
cp .env.example .env        # ajusta las claves (PostgreSQL y el administrador de Keycloak)
docker compose up -d        # PostgreSQL y Keycloak locales
dotnet build
dotnet test
```

### Cadena de conexión de la API

La API lee `ConnectionStrings:CoreBancario`. **Nunca se guarda en el repositorio.** En desarrollo, usa user-secrets (la API ya tiene su `UserSecretsId`) o una variable de entorno:

```bash
dotnet user-secrets set "ConnectionStrings:CoreBancario" "Host=localhost;Port=5432;Database=core_bancario;Username=core_bancario;Password=<la de tu .env>" --project src/CoreBancario.Api
# o bien: export ConnectionStrings__CoreBancario="Host=localhost;..."
```

En `Development` la API aplica las migraciones al arrancar. Las pruebas de Infrastructure y Api usan Testcontainers, así que necesitan Docker en marcha.

## Autenticación con Keycloak

La API no emite credenciales: las emite **Keycloak** (ADR-0014) y la API solo valida los tokens (ADR-0015). Hay dos realms, `clientes` y `empleados`, que se importan solos desde [`infra/keycloak/realms`](infra/keycloak/realms). Los temas de la pantalla de ingreso están en [`infra/keycloak/themes`](infra/keycloak/themes).

### Levantar Keycloak y entrar a la consola

1. Rellena en `.env` `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD` (el administrador de la consola; elige tus propios valores) y `KC_CLIENTES_API_SECRET` (ver "Secreto de la cuenta de servicio").
2. `docker compose up -d`. El primer arranque de Keycloak tarda varios minutos (compila su configuración y crea los realms).
3. Abre `http://localhost:8080` (o el `KEYCLOAK_PORT` de tu `.env`) y entra a la consola de administración con ese usuario.

**Volumen de Keycloak antiguo.** El volumen `keycloak-data` se montaba en `/opt/keycloak/data/h2` y ahora se monta en `/opt/keycloak/data`. Si el tuyo tiene `keycloakdb.mv.db` en la raíz y no una carpeta `h2/` (`docker run --rm -v core-bancario_keycloak-data:/v alpine ls /v`), Keycloak **no da error**: crea una base nueva en silencio y pierdes los usuarios de prueba. Antes de arrancar, recrea el volumen ("Recargar un realm modificado") o mueve la base a `h2/`:

```bash
docker run --rm -v core-bancario_keycloak-data:/v alpine sh -c "mkdir /v/h2 && mv /v/keycloakdb.* /v/h2/ && chown -R 1000:0 /v"
```

En `Development` la API espera a Keycloak en `http://localhost:8080` (sección `Autenticacion` de `appsettings.Development.json`). Si cambias `KEYCLOAK_PORT`, ajusta `Autenticacion:Clientes:Emisor` y `Autenticacion:Empleados:Emisor` con user-secrets: el emisor debe ser **exactamente** el `iss` de los tokens.

### Crear un operador (realm `empleados`)

1. Consola → realm `empleados` → *Users* → *Add user*: escribe un nombre de usuario y guarda.
2. Pestaña *Credentials* → *Set password*, con *Temporary* desactivado (mínimo 12 caracteres y sin contener el usuario).
3. Pestaña *Role mapping* → *Assign role* → `operador`.

En su primer ingreso Keycloak le pedirá inscribir la app autenticadora (OTP): el segundo factor es obligatorio (RN-24).

### Secreto de la cuenta de servicio

Para habilitar y deshabilitar accesos, la API usa la cuenta de servicio `core-bancario-api-admin` del realm `clientes`, que solo puede gestionar y ver usuarios (ADR-0014). Su secreto **nunca** está en el repositorio: el realm trae el marcador `${KC_CLIENTES_API_SECRET}` y Keycloak lo resuelve con la variable de entorno al importar.

1. Genera un secreto largo y aleatorio (por ejemplo `openssl rand -hex 32`) y ponlo en `.env` como `KC_CLIENTES_API_SECRET`. Sin este valor `docker compose up` no arranca Keycloak.
2. Pon **el mismo valor** en los user-secrets de la API:

```bash
dotnet user-secrets set "Keycloak:Administracion:ClientSecret" "<el mismo secreto>" --project src/CoreBancario.Api
# o bien: export Keycloak__Administracion__ClientSecret="<el mismo secreto>"
```

Si ya tenías un volumen de Keycloak de antes de la 003b, el realm `clientes` no trae esta cuenta: recárgalo (sección siguiente).

### Habilitar el acceso de un cliente de prueba

Ya no hace falta crear al cliente a mano en la consola ni vincularlo con SQL:

1. Registra el cliente con `POST /clientes` usando el token de un operador y guarda su `id`.
2. Llama a `POST /clientes/{id}/acceso-digital/habilitar` con el token de un operador. La respuesta trae la `claveTemporal` (solo aparece ahí) para entregársela al cliente. El usuario se llama como su documento sin separadores, por ejemplo `CC1234567`.
3. El cliente entra con esa clave: Keycloak le pedirá cambiarla e inscribir su app autenticadora (OTP).

Llamar otra vez a `habilitar` sobre un acceso ya habilitado lo **restablece**: clave temporal nueva, segundo factor borrado y sesiones cerradas. `POST /clientes/{id}/acceso-digital/deshabilitar` lo deshabilita y cierra sus sesiones.

### Solicitudes de acceso sin resultado

Cada habilitar o deshabilitar deja primero una **solicitud** en la base y después un **resultado** en otra fila; nada se edita ni se borra (RN-20, ADR-0017). Una solicitud sin resultado es una operación cuyo desenlace no se confirmó (por ejemplo, Keycloak no respondió). El siguiente intento sobre el mismo cliente las cierra solo, mirando la marca del usuario en Keycloak. Para auditar las que sigan abiertas:

```sql
SELECT s.id, s.cliente_id, s.operador, s.accion, s.fecha
FROM solicitudes_de_acceso s
LEFT JOIN resultados_de_acceso r ON r.solicitud_id = s.id
WHERE r.solicitud_id IS NULL
ORDER BY s.fecha;
```

### Errores 502 y 503 al gestionar accesos

| Estado | `codigo` | Qué pasó | Qué debe hacer el operador |
|---|---|---|---|
| 502 | `identidad-error` | Keycloak respondió con un error y la operación no se completó (pudo quedar a medias). La solicitud queda como `Fallido`. | Leer el `detail` de la respuesta (trae el mensaje fijo, no el motivo técnico) y volver a intentar la misma operación. Si se repite, avisar a quien administra Keycloak. |
| 503 | `identidad-sin-respuesta` | Keycloak no respondió (tiempo de espera o conexión). **No se sabe si se aplicó.** La solicitud queda pendiente. | Leer el `detail` y volver a intentar la **misma** operación: el reintento confirma o descarta la anterior. No entregar ninguna clave anterior; la que sirve es la de la respuesta 200. |

Un `422 acceso-no-habilitado` al deshabilitar significa que el cliente ya no tiene un acceso habilitado (por ejemplo, un intento anterior sí se aplicó).

### Recargar un realm modificado

Keycloak importa los realms **solo si no existen**: si cambias un JSON de `infra/keycloak/realms`, hay que borrar el volumen de Keycloak para que lo vuelva a importar (esto borra los usuarios de prueba):

```bash
docker compose rm -sf keycloak
docker volume rm core-bancario_keycloak-data   # el prefijo es el nombre de la carpeta del proyecto
docker compose up -d keycloak
```

Los temas sí se recargan en caliente: no hace falta borrar nada para ver un cambio en un tema.

### Contrato OpenAPI

El contrato se genera al compilar y se versiona en [`src/CoreBancario.Api/openapi/core-bancario-api.json`](src/CoreBancario.Api/openapi/core-bancario-api.json) (ADR-0019). Si cambias un endpoint, compila y haz commit del archivo: el CI falla si el contrato versionado no está al día. En producción no se expone ninguna ruta de OpenAPI.

### Límites conocidos

Se aceptaron a propósito y están explicados en el [plan de la 003](specs/003-auth-api/plan.md):

- **La clave temporal no vence (CL-05).** Keycloak 26 no permite que una clave temporal caduque: sirve hasta que el operador restablece o deshabilita el acceso. Mejora futura: un proceso que deshabilite los accesos con la clave temporal sin usar tras 72 horas.
- **La pantalla del OTP confirma que la clave era correcta.** Es inherente al ingreso en dos pasos; lo mitiga el bloqueo, porque un OTP erróneo también cuenta como intento fallido.
- **Ventana de revocación de hasta 5 minutos.** La API valida el token sin consultar a Keycloak (270 s de vida + 30 s de tolerancia de reloj), así que un acceso deshabilitado o una sesión cerrada siguen sirviendo hasta que vence el token que ya se emitió.
- **El aviso del número de documento se puede saltar.** El navegador avisa si la clave nueva contiene el número de documento, pero un cliente sin JavaScript o una petición hecha a mano lo evitan. El servidor sí rechaza la clave que contiene el usuario completo (por ejemplo `CC1234567`).
- **El operador ve la clave temporal** que entrega al cliente.

## Cómo se trabaja

Cada feature pasa por cuatro fases con aprobación del autor entre ellas: **Spec → Design → Build (pruebas primero) → Review**. Todo queda en el repositorio:

- [`docs/producto/spec-fase-1.md`](docs/producto/spec-fase-1.md): spec de producto con reglas de negocio, requisitos y escenarios de falla.
- [`docs/adr/`](docs/adr/): decisiones de arquitectura con las alternativas descartadas.
- `specs/NNN-feature/`: spec, plan, tareas, notas de aprendizaje y revisión de cada feature.
- [`docs/sdd/guia.md`](docs/sdd/guia.md): guía del flujo y de los agentes.

## Trade-offs y escenarios de falla

_Se completa a medida que avanzan los sprints: cada escenario de falla tendrá aquí su explicación y su prueba._
