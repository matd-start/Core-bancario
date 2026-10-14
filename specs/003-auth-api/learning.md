# Aprendizaje — 003 Autenticación y autorización (bloques 003a y 003b)

Este documento explica lo que se construyó en el bloque 003a (tareas T01 a T14) para alguien que ve estos patrones por primera vez. El bloque 003b (habilitar y deshabilitar accesos desde la API) se explica en la segunda parte, a partir de "Bloque 003b".

## 1. Mapa del código

### Domain (`src/CoreBancario.Domain`)

| Archivo | Qué hace |
|---|---|
| `AccesosDigitales/AccesoDigital.cs` | El **vínculo** entre un `Cliente` y su usuario en Keycloak: guarda el `ClienteId` y el `Sub` (el identificador del usuario en el token). También sabe calcular el nombre de usuario con el que el cliente inicia sesión (`NombreDeUsuarioDe`: tipo + número, por ejemplo `CC1234567`). No sabe nada de Keycloak ni de HTTP. |

### Application (`src/CoreBancario.Application/Cuentas`)

| Archivo | Qué hace |
|---|---|
| `ICuentasDelTitular.cs` | **Puerto** de lectura: "dame las cuentas de este `sub`". Application define qué necesita; Infrastructure lo implementa. |
| `ListarMisCuentasConsulta.cs` y `ListarMisCuentasHandler.cs` | Caso de uso "listar mis cuentas". |
| `ObtenerMiCuentaConsulta.cs` y `ObtenerMiCuentaHandler.cs` | Caso de uso "ver una de mis cuentas". Si la cuenta es ajena responde con el **mismo** error que si no existiera. |
| `ObtenerCuentaHandler.cs` | Solo se le extrajo una constante con el mensaje de "cuenta no encontrada", para que los dos handlers digan exactamente lo mismo. |
| `DependencyInjection.cs` | Registra los dos handlers nuevos. |

### Infrastructure (`src/CoreBancario.Infrastructure`)

| Archivo | Qué hace |
|---|---|
| `Persistencia/Configuraciones/AccesoDigitalConfiguracion.cs` | Cómo se guarda el vínculo: tabla `accesos_digitales`, clave primaria sobre `cliente_id`, índice único sobre `sub`, clave foránea a `clientes`. |
| `Persistencia/Migraciones/*_VinculoDeAccesoDigital.cs` | La migración que crea esa tabla (la genera `dotnet ef`). |
| `Persistencia/Repositorios/CuentasDelTitular.cs` | Implementa `ICuentasDelTitular` con una sola consulta SQL que **filtra por dueño**. |
| `Persistencia/CoreBancarioDbContext.cs` | Se le añadió el `DbSet<AccesoDigital>`. |

### Api (`src/CoreBancario.Api`)

| Archivo | Qué hace |
|---|---|
| `Autenticacion/ConfiguracionDeAutenticacion.cs` | Registra todo: los dos esquemas JWT (uno por realm), el selector, las políticas `Cliente` y `Operador` y la política por defecto. |
| `Autenticacion/SelectorDeEsquema.cs` | Lee el emisor (`iss`) del token, sin validarlo, para decidir qué esquema lo valida. |
| `Autenticacion/EventosDeJwt.cs` | Reacciona a lo que pasa al validar: traduce el rol de Keycloak al rol de la API, y escribe las respuestas 401 y 403. |
| `Autenticacion/MotivoDeRechazo.cs` | Convierte una falla de validación en una palabra corta para el log ("vencida", "firma-invalida"...). |
| `Autenticacion/ClaimsPrincipalExtensiones.cs` | `usuario.Sub()`: el identificador del usuario autenticado. |
| `Autenticacion/RegistroDeOperacionDelOperador.cs` | Filtro que deja en el log qué operador hizo qué operación. |
| `Autenticacion/{Roles,Politicas,EsquemasDeAutenticacion,OpcionesDeAutenticacion,TokenSinSujetoException}.cs` | Constantes y tipos pequeños de apoyo. |
| `Cuentas/EndpointsDeMisCuentas.cs` | `GET /mis-cuentas` y `GET /mis-cuentas/{id}`. |
| `Clientes/EndpointsDeClientes.cs`, `Cuentas/EndpointsDeCuentas.cs` | Las rutas de la feature 002, ahora dentro de un grupo que exige el rol Operador. |
| `OpenApi/*.cs` | Documentan en el contrato OpenAPI el esquema Bearer y las respuestas 401, 403 y 500. |
| `openapi/core-bancario-api.json` | El contrato **generado al compilar** y guardado en Git. |
| `Program.cs`, `appsettings*.json` | Cablean todo y fijan el emisor y la audiencia. |

### Fuera de C#

| Archivo | Qué hace |
|---|---|
| `infra/keycloak/realms/clientes-realm.json` y `empleados-realm.json` | La configuración completa de los dos realms (tiempos, bloqueo, segundo factor, roles, cliente de la SPA). Keycloak los importa al arrancar. |
| `infra/keycloak/themes/core-bancario/` | Tema base: el mismo mensaje genérico para cualquier error de ingreso. |
| `infra/keycloak/themes/core-bancario-clientes/` | Tema de clientes: `documento.js` (normaliza el documento y avisa si la clave lo contiene) y una plantilla que expone el usuario en la pantalla de cambio de clave. |
| `docker-compose.yml`, `.env.example` | El servicio `keycloak` junto a PostgreSQL. |
| `tests/CoreBancario.Keycloak.Tests/` | Pruebas contra un Keycloak real en un contenedor (ya escritas por el test-writer). |

## 2. Recorrido de una petición

Caso: un cliente consulta `GET /mis-cuentas/{cuentaId}`.

**Antes de llegar a la API (Keycloak):**

1. La SPA lleva al cliente a la pantalla de ingreso del realm `clientes`. El tema `core-bancario-clientes` agrega el selector de tipo de documento; `documento.js` escribe `CC1234567` en el campo usuario.
2. Keycloak comprueba usuario y clave, pide el segundo factor (OTP) y, si es el primer ingreso, obliga a cambiar la clave temporal y a inscribir el OTP.
3. Keycloak entrega a la SPA un **access token** (un JWT firmado, de 270 segundos de vida) con el `sub`, la audiencia `core-bancario-api` y los roles del realm.

**Dentro de la API:**

1. La SPA llama a `GET /mis-cuentas/{cuentaId}` con la cabecera `Authorization: Bearer <token>`.
2. `app.UseExceptionHandler()` se coloca primero: cualquier error de lo que sigue sale con el formato de errores del proyecto.
3. `app.UseAuthentication()` necesita saber quién llama. El esquema por defecto es una **política de esquema** (`EsquemasDeAutenticacion.PorEmisor`); esta llama a `SelectorDeEsquema.Elegir`, que lee el `iss` del token y elige el esquema `Clientes` o `Empleados`.
4. El esquema elegido (un `AddJwtBearer` configurado en `ConfiguracionDeAutenticacion`) valida la firma con la clave pública del realm, el emisor, la audiencia, el algoritmo (solo RS256) y la vigencia (con 30 segundos de tolerancia de reloj).
5. Si la validación sale bien, se dispara `EventosDeJwt.TokenValidated`: comprueba que exista el `sub` y, si `realm_access.roles` contiene `cliente`, añade el rol `Cliente` de la API. El rol depende **del esquema que validó**, así que un token del realm `clientes` nunca puede ser Operador.
6. Si la validación falla, se dispara `EventosDeJwt.Challenge`: registra en el log el motivo (`MotivoDeRechazo`) **sin el token** y responde 401 con el mismo cuerpo para todos los motivos.
7. `app.UseAuthorization()` mira los metadatos del endpoint. El grupo `/mis-cuentas` exige la política `Cliente` (`RequireAuthorization(Politicas.Cliente)`). Si el usuario es Operador, se dispara `EventosDeJwt.Forbidden` y responde 403, **sin ejecutar el endpoint**.
8. `EndpointsDeMisCuentas` ejecuta el endpoint `ObtenerMiCuenta`: toma el `sub` con `usuario.Sub()` (nunca de la URL ni del cuerpo) y llama a `ObtenerMiCuentaHandler`.
9. `ObtenerMiCuentaHandler` pide la cuenta a `ICuentasDelTitular.ObtenerAsync(cuentaId, sub)`.
10. `CuentasDelTitular` (Infrastructure) ejecuta una consulta de EF Core que equivale a "la cuenta con ese id **y** que exista un vínculo en `accesos_digitales` entre su cliente y ese `sub`".
11. Si la consulta no devuelve nada, el handler lanza `RecursoNoEncontradoException` con el mismo mensaje que usa la 002; el `ManejadorDeErrores` la convierte en 404. Si devuelve la cuenta, el handler la convierte en `CuentaDto` y el endpoint responde 200.

Una cuenta ajena y una cuenta inexistente recorren **exactamente el mismo camino** y dan la misma respuesta.

## 3. Conceptos clave

### 3.1 Validación local de un JWT (y su precio)

- **Qué problema resuelve:** la API necesita saber quién llama en cada petición sin preguntarle a Keycloak cada vez (sería lento y haría a Keycloak un punto único de falla).
- **Dónde está:** `ConfiguracionDeAutenticacion` (parámetros de validación), `EventosDeJwt`, y los tiempos en `clientes-realm.json` y `empleados-realm.json`.
- **Analogía:** un billete de cine con holograma. El acomodador no llama a la taquilla para cada billete: comprueba el holograma (la firma), la fecha (la vigencia) y la función (la audiencia). El precio es que un billete anulado en taquilla sigue entrando hasta que caduca. Aquí esa ventana es de 5 minutos (270 s de vida del token + 30 s de tolerancia de reloj), y por eso `ToleranciaDeReloj` es una constante y no una opción de configuración: RNF-01 depende de ella.
- **Para estudiar:** JWT, JWKS y rotación de claves con `kid`, y la diferencia entre `Challenge` (401, "no sé quién eres") y `Forbid` (403, "sé quién eres, pero no puedes").

### 3.2 Dos esquemas y un selector por emisor

- **Qué problema resuelve:** hay dos poblaciones (clientes y empleados) en dos realms con claves de firma distintas. El rol "Operador" solo debe salir de un token firmado por el realm `empleados`.
- **Dónde está:** `SelectorDeEsquema`, `AddPolicyScheme` en `ConfiguracionDeAutenticacion`, y el `EventosDeJwt` que cada esquema recibe con su propio par de roles.
- **Analogía:** dos porterías en la entrada de un edificio, una para visitantes y otra para empleados, con un recepcionista que mira el color del carné (sin darlo por bueno todavía) y manda a cada persona a su portería. Si el recepcionista se equivoca, la portería equivocada rechaza el carné. Elegir mal nunca da acceso.
- **Para estudiar:** `AddPolicyScheme` y `ForwardDefaultSelector` en ASP.NET Core.

### 3.3 Autorización por propiedad dentro de la consulta (BOLA/IDOR)

- **Qué problema resuelve:** un cliente autenticado no debe poder ver la cuenta de otro cambiando el id en la URL. Es la vulnerabilidad nº 1 de la lista OWASP de APIs (BOLA).
- **Dónde está:** `CuentasDelTitular` (filtro `EXISTS` en la misma consulta), la tabla `accesos_digitales` (vínculo `sub` ↔ `ClienteId`) y `ObtenerMiCuentaHandler`.
- **Analogía:** en un archivo, en vez de pedir "tráeme la carpeta 5 y yo miro si es mía", pides "tráeme la carpeta 5 **de mi cajón**". Si no está en tu cajón, el archivista responde igual que si no existiera. No hay forma de saber qué carpetas ajenas existen.
- **Por qué importa aquí:** "cargar y luego comprobar" es frágil, porque basta olvidar la comprobación una vez. Filtrar en la consulta cierra el problema por construcción. Además, el `ClienteId` nunca viaja en la URL: sale del token.
- **Para estudiar:** BOLA/IDOR (OWASP API1) y por qué un 404 es mejor que un 403 para recursos ajenos.

### 3.4 Fallar de forma segura: política por defecto y autorización por rol

- **Qué problema resuelve:** si alguien añade un endpoint nuevo y olvida protegerlo, no debe quedar abierto.
- **Dónde está:** `SetFallbackPolicy(... RequireAuthenticatedUser ...)` en `ConfiguracionDeAutenticacion`; `/health` y OpenAPI en Development piden `AllowAnonymous()` expresamente en `Program.cs`; los grupos de endpoints piden su rol con `RequireAuthorization`.
- **Analogía:** una puerta que está cerrada con llave por defecto: para dejarla abierta hay que hacerlo a propósito y se nota en el código.
- **Para estudiar:** políticas de autorización en ASP.NET Core (`RequireRole`, política por defecto y política de respaldo).

### 3.5 Configuración como código y un contrato que se versiona

- **Qué problema resuelve:** que la seguridad no dependa de clics que nadie recuerda: los tiempos de sesión, el bloqueo y el OTP obligatorio están en JSON versionado, con pruebas que los leen. Y que el contrato OpenAPI no cambie sin que alguien lo note.
- **Dónde está:** `infra/keycloak/realms/*.json` (verificados por `ConfiguracionDeRealmsTests`); el contrato se genera al compilar y el CI comprueba que `core-bancario-api.json` esté al día.
- **Analogía:** una receta escrita frente a "cocinar de memoria". Cualquiera puede repetir el plato y, si alguien cambia un ingrediente, se ve en la diferencia.
- **Para estudiar:** importación de realms de Keycloak y por qué se omite si el realm ya existe.

### Nota de las correcciones de la revisión

- **El rol sale solo del esquema.** `EventosDeJwt` borra cualquier claim de rol que el token traiga con el tipo de rol de ASP.NET antes de añadir el suyo: si no, un token del realm `clientes` con ese claim fabricado se haría pasar por Operador y rompería la regla de ADR-0015.
- **"Completada" significa completada.** `RegistroDeOperacionDelOperador` también registra "no completada" cuando el endpoint responde 4xx/5xx sin lanzar excepción (por ejemplo un 400 de validación); un registro de auditoría que dice "completada" ante un rechazo engaña.

## 4. Para practicar

Ciclo **Copiar → Modificar → Recrear**: primero copia el patrón tal cual, luego cámbialo, y por último reconstrúyelo sin mirar.

### Ejercicio 1: un endpoint nuevo para el cliente

- **Copiar:** lee `EndpointsDeMisCuentas.cs` y `ListarMisCuentasHandler`, y observa cómo el endpoint nunca recibe el `sub` por la ruta.
- **Modificar:** añade `GET /mis-cuentas/{cuentaId}/estado`, que devuelva solo el estado (`"Activa"`, `"Bloqueada"` o `"Cerrada"`). Usa `ObtenerMiCuentaHandler` y comprueba con una prueba que una cuenta ajena da 404.
- **Recrear:** sin mirar el código, escribe de memoria un handler `ObtenerMiSaldoHandler` y su endpoint. Pregúntate en qué punto se filtra por dueño.
- **Qué comprobar:** el contrato `openapi/core-bancario-api.json` cambia al compilar; ¿qué pasa en el CI si no lo versionas?

### Ejercicio 2: un tercer rol

- **Copiar:** mira cómo `Roles.Operador` y `Roles.OperadorEnKeycloak` se relacionan en `EventosDeJwt` y en `ConfiguracionDeAutenticacion`.
- **Modificar:** imagina un rol `Auditor` que solo puede leer `GET /cuentas/{id}`. Escribe en papel qué archivos tocarías (política, realm, endpoint). Después implementa solo la política `Auditor` y una prueba que demuestre que un Operador recibe 403 en esa ruta.
- **Recrear:** explica sin mirar por qué el rol de la API se decide en `TokenValidated` de cada esquema y no mirando únicamente el contenido del token.

### Ejercicio 3: leer un 401 como lo haría un atacante

- **Copiar:** ejecuta `F003_RNF04_MotivosDistintos_MismaRespuesta401` y mira el log de la prueba `F003_RNF04_TokenRechazado_ElLogTieneElMotivoSinElToken`.
- **Modificar:** añade un motivo nuevo a `MotivoDeRechazo` (por ejemplo, `ClaveDeFirmaDesconocida`, separando el caso en que el `kid` del token no existe en el realm) y su caso en la teoría `FallasYMotivos`. Fíjate en que la respuesta al cliente no debe cambiar.
- **Recrear:** sin mirar, escribe una frase que explique por qué la respuesta al cliente es siempre la misma y el motivo solo va al log.

---

# Bloque 003b — Gestionar el acceso (T15 a T27)

El operador puede **habilitar** el acceso de un cliente (la API crea su usuario en Keycloak y le entrega una clave temporal), **restablecerlo** si lo olvidó y **deshabilitarlo**. Lo difícil es que hay dos sistemas (PostgreSQL y Keycloak) y no existe una transacción que cubra a los dos.

## 5. Mapa del código (003b)

### Domain (`src/CoreBancario.Domain/AccesosDigitales`)

| Archivo | Qué hace |
|---|---|
| `AccionDeAcceso.cs`, `EstadoDeAcceso.cs`, `ResultadoDeAcceso.cs` | Tres enums: lo que pidió el operador (Habilitar, Restablecer, Deshabilitar), cómo está el acceso (SinAcceso, Habilitado, Deshabilitado) y cómo terminó una solicitud (Completado, Fallido). |
| `SolicitudDeAcceso.cs` | La **intención** del operador, una fila que nunca se edita. `ParaHabilitar` decide si es Habilitar o Restablecer según el estado; `ParaDeshabilitar` rechaza antes de crear nada si no hay acceso habilitado. |
| `ResultadoDeSolicitud.cs` | El desenlace de una solicitud, **otra fila** distinta. Solo la crean `Completar` y `Fallar`. |
| `AccesoNoHabilitadoException.cs` | La regla rota "deshabilitar lo que no está habilitado" (422). |

### Application (`src/CoreBancario.Application/AccesosDigitales`)

| Archivo | Qué hace |
|---|---|
| `IProveedorDeIdentidad.cs`, `UsuarioDeIdentidad.cs` | **Puerto** hacia Keycloak: buscar, obtener, crear, reiniciar y deshabilitar usuarios. Application no sabe que existe HTTP. |
| `IAccesoDigitalRepositorio.cs`, `IRegistroDeAccesos.cs` | Puertos de persistencia. El registro solo tiene "agregar" y "listar pendientes": no hay forma de editar ni borrar. |
| `IGeneradorDeClaveTemporal.cs`, `ClaveTemporal.cs` | El azar es una frontera; `ClaveTemporal` oculta su valor en `ToString`. |
| `LocalizadorDeAcceso.cs`, `AccesoLocalizado.cs` | Encuentran el acceso actual de un cliente (vínculo en la base + usuario en Keycloak) y calculan su `Estado`. |
| `ReconciliadorDeSolicitudes.cs` | Cierra las solicitudes que quedaron sin resultado, mirando la **marca** del usuario en Keycloak. |
| `HabilitarAccesoHandler.cs`, `DeshabilitarAccesoHandler.cs` | Los dos casos de uso, con la secuencia "intención primero". |
| `ProveedorDeIdentidadSinRespuestaException.cs`, `ErrorDelProveedorDeIdentidadException.cs` | Los dos modos de falla de Keycloak: no respondió (503) / respondió con error (502). |
| `HabilitarAccesoComando.cs`, `DeshabilitarAccesoComando.cs`, `AccesoHabilitadoDto.cs`, `RegistroDeAccesoDto.cs` | Entradas y salidas. `AccesoHabilitadoDto.ToString` no imprime la clave. |

### Infrastructure (`src/CoreBancario.Infrastructure`)

| Archivo | Qué hace |
|---|---|
| `Persistencia/Configuraciones/SolicitudDeAccesoConfiguracion.cs`, `ResultadoDeSolicitudConfiguracion.cs` | Las tablas `solicitudes_de_acceso` y `resultados_de_acceso`. La clave primaria del resultado es `solicitud_id`: **un solo resultado por solicitud**. |
| `Persistencia/Migraciones/*_RegistroDeAccesos.cs` | Crea las tablas y, a mano, la función y los triggers que impiden editar o borrar el registro. |
| `Persistencia/CoreBancarioDbContext.cs` | `GuardarCambiosAsync` traduce los choques de unicidad del vínculo y del resultado a `ConflictoDeConcurrenciaException` (409). |
| `Persistencia/Repositorios/AccesoDigitalRepositorio.cs`, `RegistroDeAccesos.cs` | Implementan los puertos. `ListarPendientesAsync` = solicitudes sin fila de resultado. |
| `AccesosDigitales/GeneradorAleatorioDeClaveTemporal.cs` | 16 caracteres con `RandomNumberGenerator.GetString`, sin caracteres ambiguos. |
| `Identidad/ProveedorDeIdentidadKeycloak.cs` | El adaptador: traduce el puerto a llamadas de la Admin REST API de Keycloak y los fallos HTTP a las dos excepciones. |
| `Identidad/TokenDeServicioHandler.cs` | Pide (y guarda hasta 30 s antes de que venza) el token de la cuenta de servicio y lo pone en cada llamada. |
| `Identidad/OpcionesDeKeycloak.cs` | La sección de configuración `Keycloak:Administracion`. |
| `DependencyInjection.cs` | Registra repositorios, generador y los dos `HttpClient` (el del adaptador y el que pide el token). |

### Api y configuración

| Archivo | Qué hace |
|---|---|
| `Api/AccesosDigitales/EndpointsDeAccesoDigital.cs` | `POST /clientes/{id}/acceso-digital/habilitar` y `/deshabilitar`. El operador sale de `usuario.Sub()`; habilitar responde `Cache-Control: no-store`. |
| `Api/Errores/CatalogoDeErrores.cs`, `CodigosDeError.cs` | 422 `acceso-no-habilitado`, 502 `identidad-error`, 503 `identidad-sin-respuesta`. |
| `infra/keycloak/realms/clientes-realm.json` | Se añade el cliente `core-bancario-api-admin` (cuenta de servicio con solo `manage-users` y `view-users`) y el atributo `ultima_solicitud_de_acceso` (la marca), visible y editable solo por `admin`. |
| `docker-compose.yml`, `.env.example` | La variable `KC_CLIENTES_API_SECRET`: el secreto no está en el repositorio. |

## 6. Recorrido de una petición: habilitar el acceso de un cliente

`POST /clientes/{clienteId}/acceso-digital/habilitar` con el token de un operador.

1. La autenticación y la autorización son las del bloque 003a: el grupo exige la política `Operador`. `RegistroDeOperacionDelOperador` dejará en el log si la operación se completó.
2. `EndpointsDeAccesoDigital` arma `HabilitarAccesoComando(clienteId, usuario.Sub())` y llama a `HabilitarAccesoHandler.EjecutarAsync`.
3. El handler pide el cliente a `IClienteRepositorio`. Si no existe, lanza `RecursoNoEncontradoException` (404) **sin escribir nada ni llamar a Keycloak**.
4. `LocalizadorDeAcceso` busca el acceso: con vínculo, consulta a Keycloak por el `sub`; sin vínculo, busca por nombre de usuario (por si un intento anterior dejó un usuario a medias, para **adoptarlo** y no crearlo dos veces). Aquí `ProveedorDeIdentidadKeycloak` y `TokenDeServicioHandler` hacen las primeras llamadas HTTP. Si Keycloak no responde, 503 y todavía no se escribió nada.
5. `ReconciliadorDeSolicitudes` cierra las solicitudes pendientes de intentos anteriores: Completada si la marca del usuario coincide con su id, Fallida si no.
6. `SolicitudDeAcceso.ParaHabilitar` decide la acción (Habilitar o Restablecer, según el estado). El handler la agrega a `IRegistroDeAccesos` y llama a `GuardarCambiosAsync`: **la intención queda confirmada en PostgreSQL antes de tocar Keycloak**.
7. `IGeneradorDeClaveTemporal` produce la clave. Desde aquí el handler usa `CancellationToken.None`: aunque el operador cierre el navegador, la operación termina.
8. Si no había usuario, `CrearAsync` hace **una sola** petición con la clave temporal, las acciones obligatorias (`UPDATE_PASSWORD`, `CONFIGURE_TOTP`) y la marca (el id de la solicitud). Si ya existía, `ReiniciarAccesoAsync` hace los pasos en orden (clave temporal, borrar el OTP, cerrar sesiones) y en el **último** escribe la marca.
9. Si Keycloak respondió con un error, el handler escribe una fila `Fallido` y relanza (502). Si no respondió, **no escribe nada** y relanza (503): la solicitud queda pendiente.
10. Si todo fue bien, el handler agrega el vínculo (si faltaba) y el resultado `Completado` y guarda **una sola vez** (PostgreSQL traduce un choque de unicidad a 409).
11. Devuelve `AccesoHabilitadoDto` y el endpoint añade `Cache-Control: no-store` y responde 200. Es la única vez que la clave viaja.

## 7. Conceptos clave (003b)

### 7.1 Intención primero

- **Qué problema resuelve:** no hay transacción entre PostgreSQL y Keycloak. Si primero llamáramos a Keycloak y la base cayera, habría un usuario creado sin rastro de quién lo pidió. Si primero escribiéramos "hecho" y Keycloak fallara, el registro mentiría.
- **Dónde está:** `HabilitarAccesoHandler` y `DeshabilitarAccesoHandler` (el orden de los pasos); `SolicitudDeAcceso` (la intención).
- **Analogía:** pedir algo en un restaurante. El camarero anota el pedido en la comanda **antes** de ir a la cocina. Si la cocina no contesta, la comanda queda abierta y se sabe qué falta aclarar; nadie se queda con un plato sin comanda.
- **Para estudiar:** sagas y consistencia eventual, el "write-ahead log", y por qué un tiempo de espera agotado **no** es un "no".

### 7.2 La marca y la reconciliación

- **Qué problema resuelve:** si Keycloak no respondió, no sabemos si aplicó el cambio. La **marca** (`ultima_solicitud_de_acceso`) es el id de la solicitud escrito como último paso en el usuario de Keycloak: si coincide, se aplicó todo. Al reintentar, `ReconciliadorDeSolicitudes` compara y cierra la solicitud vieja como Completada o Fallida.
- **Dónde está:** `ProveedorDeIdentidadKeycloak.ActualizarUsuarioAsync` (escribe la marca al final), `ReconciliadorDeSolicitudes`, el atributo del perfil en `clientes-realm.json`.
- **Analogía:** poner un sello en un documento **solo cuando terminas el último paso del trámite**. Si el sello está, el trámite se completó; si no, aunque hayas hecho tres de cuatro pasos, no cuenta.
- **Detalle importante:** al deshabilitar, las sesiones se cierran **antes** de escribir la marca. Si fuera al revés y cerrar fallara, la marca diría "completado" sin serlo.
- **Para estudiar:** idempotencia, "at-least-once" y reintentos seguros.

### 7.3 Registro inmutable con trigger

- **Qué problema resuelve:** un registro de auditoría que se puede editar no es evidencia. El código nunca edita, pero la base lo garantiza también (defensa en profundidad).
- **Dónde está:** la migración `RegistroDeAccesos` (función `impedir_cambios_en_registro_de_accesos` y dos triggers `BEFORE UPDATE OR DELETE`); dos tablas separadas porque "pendiente" se define como "solicitud sin resultado" y así no hay que editar una fila para cerrarla.
- **Analogía:** un libro de actas con las hojas numeradas y selladas: solo se puede añadir hojas al final; cualquier tachón queda a la vista.
- **Para estudiar:** triggers de PostgreSQL, `RAISE EXCEPTION` (SQLSTATE `P0001`) y por qué la clave primaria `solicitud_id` en la tabla de resultados impide cerrar dos veces la misma solicitud.

### 7.4 Cuenta de servicio de mínimo privilegio

- **Qué problema resuelve:** la API necesita crear y deshabilitar usuarios en Keycloak, pero si usara el administrador de `master`, una fuga de la API sería una fuga de todo Keycloak.
- **Dónde está:** el cliente `core-bancario-api-admin` en `clientes-realm.json` (solo roles `manage-users` y `view-users` de `realm-management`), `TokenDeServicioHandler`, la prueba `ConfiguracionDeCuentaDeServicioTests`.
- **Analogía:** darle al repartidor la llave del buzón, no la llave maestra del edificio.
- **Secreto:** nunca en el repositorio. El JSON trae el marcador `${KC_CLIENTES_API_SECRET}` y Keycloak lo sustituye al importar con la variable de entorno.
- **Para estudiar:** OAuth 2.0 *client credentials* y la Admin REST API de Keycloak.

### 7.5 Traducir los fallos de un sistema externo

- **Qué problema resuelve:** Application no debe conocer `HttpClient`. El adaptador convierte "respondió con error" en `ErrorDelProveedorDeIdentidadException` (502) y "no respondió" en `ProveedorDeIdentidadSinRespuestaException` (503). El motivo guarda solo el estado HTTP, **nunca el cuerpo** de la respuesta.
- **Dónde está:** `ProveedorDeIdentidadKeycloak.EnviarAsync` y `CatalogoDeErrores`.
- **Analogía:** un intérprete que distingue "me dijeron que no" de "nadie contestó el teléfono": son noticias distintas y piden acciones distintas.

## 8. Para practicar (003b)

### Ejercicio 1: otra acción del registro

- **Copiar:** lee `SolicitudDeAcceso.ParaDeshabilitar` y `DeshabilitarAccesoHandler`, y observa que valida antes de crear la solicitud.
- **Modificar:** imagina una acción `Suspender` (Habilitado a Deshabilitado, igual que Deshabilitar pero con otro nombre en el registro). Escribe primero la prueba en `SolicitudDeAccesoTests` y después añade el valor al enum, al CHECK de la tabla y a una migración nueva.
- **Recrear:** sin mirar, explica por qué el CHECK `ck_solicitudes_de_acceso_accion` está en la base y no solo en el enum.

### Ejercicio 2: leer una reconciliación

- **Copiar:** ejecuta `F003_ADR0017_ReintentoConPendienteAplicada_LaCierraComoCompletada` y sigue con el depurador qué devuelve `ReconciliarAsync`.
- **Modificar:** cambia el motivo de `ReconciliadorDeSolicitudes` ("Confirmada al reintentar") y observa qué pruebas se rompen. ¿Cuántas fijan ese texto?
- **Recrear:** dibuja en papel la línea de tiempo de un habilitar que se cae justo después de crear el usuario en Keycloak, y marca en qué momento se escribe cada fila.

### Ejercicio 3: una pregunta al registro

- **Copiar:** ejecuta en PostgreSQL la consulta de "solicitudes sin resultado" del README.
- **Modificar:** amplíala para mostrar también cuántos minutos llevan abiertas (`now() - s.fecha`) y ordénalas de la más vieja a la más nueva. Comprueba que una solicitud con resultado no aparece.
- **Recrear:** escribe de memoria un `INSERT` en `resultados_de_acceso` para una solicitud pendiente y comprueba que el trigger rechaza cambiarlo después con un `UPDATE`.

## 9. Notas de la revisión del bloque 003b

### 9.1 Errores previstos frente a inesperados

- **Qué cambió:** `ManejadorDeErrores` ya no decide por el número (`>= 500`), sino por el **origen**. `ErrorHttp` tiene la bandera `Previsto`: los 502 `identidad-error` y 503 `identidad-sin-respuesta` del catálogo la llevan en `true`.
- **Por qué:** un fallo de Keycloak es un hecho esperado de un tercero: el operador necesita leer qué pasó y qué hacer, así que el `detail` lleva el **mensaje fijo** (nunca el motivo técnico) y se registra como Warning. Un error inesperado (500) puede traer SQL o rutas en su mensaje: sigue sin `detail` y se registra como Error, que es lo que debe hacer sonar una alerta.
- **Analogía:** una avería anunciada ("la línea 3 está cortada, usa la 4") frente a un fallo desconocido en la central: lo primero se explica al pasajero; lo segundo se investiga por dentro.

### 9.2 La cuenta de servicio no debe tener roles de realm

- **Qué cambió:** el usuario de la cuenta de servicio en `clientes-realm.json` queda con `realmRoles: []` (antes heredaba `default-roles-clientes`, y con él el rol `cliente`).
- **Por qué:** menor privilegio (ADR-0014). Con ese rol, el token de servicio habría parecido el de un cliente real. La Admin API se autoriza con los roles de `realm-management` (`manage-users`, `view-users`), que es lo único que necesita.
- **Analogía:** la llave del buzón no debería además llevar el carné de socio del club.
