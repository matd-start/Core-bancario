# Aprendizaje — 002 Backoffice: clientes y cuentas

Este documento explica lo que se construyó, pensado para quien ve estos patrones por primera vez. Léelo con el código abierto al lado.

## 1. Mapa del código

Las dependencias van siempre hacia adentro: **Api / Infrastructure → Application → Domain**. Domain no conoce a nadie.

### Domain (las reglas del negocio; sin paquetes externos)

| Archivo | Qué hace |
|---|---|
| `Domain/Clientes/Documento.cs` | Value object: tipo (CC/CE/PA) + número **normalizado** (sin puntos, guiones ni espacios). Decide si el formato es válido. |
| `Domain/Clientes/NombreDePersona.cs`, `Correo.cs`, `Telefono.cs` | Value objects: cada uno limpia y valida su dato (`TryCrear` / `Crear`). |
| `Domain/Clientes/Cliente.cs` | La entidad cliente. Solo se crea con `Cliente.Registrar(...)`. |
| `Domain/Cuentas/NumeroDeCuenta.cs` | Value object: 10 dígitos con dígito verificador Luhn (RN-18). |
| `Domain/Cuentas/Cuenta.cs` | Ahora lleva `NumeroDeCuenta`, `FechaApertura` y `FechaCierre`. `Cerrar(fecha)` fija la fecha. |
| `Domain/Monetario/Moneda.cs`, `Dinero.cs` | `Moneda.TryDesdeCodigo("COP")`; el constructor de `Dinero` deja el monto en su forma canónica. |

### Application (los casos de uso; define "puertos", no los implementa)

| Archivo | Qué hace |
|---|---|
| `Application/Comun/Resultado.cs` | `Resultado<T>`: o éxito con un valor, o "datos inválidos" con **todos** los errores por campo. |
| `Application/Comun/IUnidadDeTrabajo.cs` | Puerto: "guarda todo lo pendiente en una sola transacción". |
| `Application/Comun/*Exception.cs`, `Clientes/DocumentoDuplicadoException.cs` | Los errores que la capa HTTP traducirá a 404 y 409. |
| `Application/Clientes/RegistrarClienteHandler.cs` | Caso de uso: valida los seis campos, crea el cliente, guarda. |
| `Application/Clientes/ObtenerClienteHandler.cs`, `BuscarClientePorDocumentoHandler.cs` | Casos de uso de lectura: la ficha del cliente con sus cuentas. |
| `Application/Cuentas/AbrirCuentaHandler.cs` | Valida la moneda, busca al cliente, genera un número libre y abre la cuenta. |
| `Application/Cuentas/CambiarEstadoDeCuentaHandler.cs` | Bloquear, desbloquear o cerrar (un solo handler, una `AccionDeEstado`). |
| `Application/Cuentas/ObtenerCuentaHandler.cs` | Detalle de una cuenta. |
| `Application/*/I...Repositorio.cs`, `IGeneradorDeNumeroDeCuenta.cs` | Puertos: lo que Application necesita del mundo exterior, expresado como interfaz. |
| `Application/*/*Dto.cs` | Lo que se devuelve hacia afuera (el dinero viaja como texto: `"0.00"`). |
| `Application/DependencyInjection.cs` | `AddAplicacion()`: registra los seis handlers. |

### Infrastructure (los detalles técnicos: EF Core y PostgreSQL)

| Archivo | Qué hace |
|---|---|
| `Infrastructure/Persistencia/CoreBancarioDbContext.cs` | La puerta a la base. Implementa `IUnidadDeTrabajo` y **traduce** errores de PostgreSQL a excepciones de Application. |
| `Infrastructure/Persistencia/Configuraciones/ClienteConfiguracion.cs`, `CuentaConfiguracion.cs` | Cómo se guarda cada entidad: columnas, índices únicos, CHECK, `xmin`. |
| `Infrastructure/Persistencia/Repositorios/*.cs` | Implementan los puertos de repositorio con EF Core. |
| `Infrastructure/Persistencia/Migraciones/*_Inicial.cs` | El script que crea las tablas (generado por `dotnet ef`, revisado como código). |
| `Infrastructure/Persistencia/ConfiguracionDeBaseDeDatos.cs` | `UsarPostgres(...)`: una única configuración compartida por la app, las pruebas y `dotnet ef`. |
| `Infrastructure/Cuentas/GeneradorAleatorioDeNumeroDeCuenta.cs` | Número aleatorio criptográfico; el verificador lo añade el dominio. |
| `Infrastructure/DependencyInjection.cs` | `AddInfraestructura(...)`: conecta puertos con implementaciones. |

### Api (traduce HTTP; no decide nada)

| Archivo | Qué hace |
|---|---|
| `Api/Clientes/EndpointsDeClientes.cs`, `Api/Cuentas/EndpointsDeCuentas.cs` | Las 8 rutas. Cada una: arma el comando, llama al handler, devuelve el resultado. |
| `Api/Errores/CatalogoDeErrores.cs` | La tabla "excepción → estado HTTP + código estable". |
| `Api/Errores/ManejadorDeErrores.cs` | Convierte cualquier excepción en un `ProblemDetails` (JSON de error estándar). |
| `Api/Errores/RespuestasDeError.cs` | Construye el 400 con la lista de errores por campo. |
| `Api/Program.cs` | Ensambla todo y aplica las migraciones al arrancar (solo en `Development`). |

## 2. Recorrido de una petición: `POST /clientes`

Cuerpo: `{ "tipoDocumento": "CC", "numeroDocumento": "1.234.567", ... }`

1. **ASP.NET** enruta a `EndpointsDeClientes` (`MapPost("/clientes")`). Convierte el JSON en `RegistrarClienteRequest` y le entrega el `RegistrarClienteHandler` que el contenedor de dependencias construyó (registrado por `AddAplicacion`).
2. El endpoint copia el request a un `RegistrarClienteComando` y llama `handler.EjecutarAsync(...)`. Es un cambio de "idioma": el request es el contrato HTTP; el comando es el idioma interno.
3. **`RegistrarClienteHandler`** valida los **seis** campos sin detenerse en el primer fallo. Cada validación es un `TryCrear` de un value object del dominio (`Documento.TryCrear` quita los puntos y comprueba el formato de CC, `Correo.TryCrear`, etc.). Si algo falla, devuelve `Resultado.Invalido(errores)` y **no toca la base**.
4. Si todo es válido, llama a `Cliente.Registrar(...)` (dominio) con la hora del `TimeProvider`, lo pasa a `IClienteRepositorio.Agregar(...)` y llama a `IUnidadDeTrabajo.GuardarCambiosAsync(...)`.
5. **En Infrastructure**, `ClienteRepositorio.Agregar` hace `db.Clientes.Add(...)` (solo anota en memoria) y `CoreBancarioDbContext.GuardarCambiosAsync` ejecuta el `INSERT` en PostgreSQL, en una transacción.
6. Si ya existía el documento, PostgreSQL rechaza el `INSERT` con el código `23505` (violación de índice único) y el nombre del índice `ux_clientes_documento`. El `DbContext` lo traduce a `DocumentoDuplicadoException`.
7. Vuelta a la Api: si el handler devolvió éxito, el endpoint responde `201 Created` con `Location: /clientes/{id}` y el `ClienteDto`. Si devolvió `Invalido`, responde `400` (con `RespuestasDeError.DatosInvalidos`). Si lanzó una excepción, el **`ManejadorDeErrores`** la captura, consulta el `CatalogoDeErrores` (`DocumentoDuplicadoException` → 409 `documento-duplicado`) y escribe el `ProblemDetails`.

Observa lo que **no** pasa: el endpoint no valida, el handler no sabe de HTTP ni de EF, y el dominio no sabe que existe una base.

## 3. Conceptos clave

### 3.1 Value objects con "normalizar y validar" (`Documento`, `Correo`, `Telefono`...)

- **Qué problema resuelve:** `"1.234.567"`, `"1234567"` y `" 1 234 567 "` son el mismo documento. Si cada pantalla o endpoint normaliza a su manera, aparecen clientes duplicados.
- **Dónde está:** `Domain/Clientes/Documento.cs` (y hermanos). Application no repite las reglas: solo llama a `TryCrear` y traduce un `false` en un mensaje.
- **Analogía:** el aduanero del aeropuerto. Todo lo que entra pasa por él y sale ya revisado y con sello; dentro del país nadie vuelve a preguntar si el pasaporte es válido.
- **Detalle útil:** `TryCrear` (devuelve `false`) es para datos que vienen de **fuera** y pueden estar mal; `Crear` (lanza excepción) es para cuando **ya validaste** y un fallo sería un bug.

### 3.2 `Resultado<T>`: acumular todos los errores

- **Qué problema resuelve:** si el usuario escribe mal el documento, el correo y el teléfono, queremos decírselo **de una vez**, no uno por intento. Una excepción corta en el primer error; un `Resultado` puede cargar la lista completa.
- **Dónde está:** `Application/Comun/Resultado.cs`; se usa en `RegistrarClienteHandler`. Solo representa "datos inválidos". Lo demás (no existe, duplicado, regla rota) sigue siendo excepción.
- **Analogía:** el corrector de un examen que marca todas las faltas de la hoja, no solo la primera.

### 3.3 Índice único como árbitro (unicidad sin consulta previa)

- **Qué problema resuelve:** "no puede haber dos clientes con el mismo documento", incluso si dos operadores registran **al mismo tiempo**. Consultar "¿existe?" antes de insertar no sirve: entre la consulta y el `INSERT` otro puede colarse (condición de carrera).
- **Dónde está:** `ClienteConfiguracion` declara `ux_clientes_documento`; `CoreBancarioDbContext.GuardarCambiosAsync` traduce el error `23505`. El handler **no** consulta antes.
- **Analogía:** la taquilla de un cine con un solo asiento por número. En vez de preguntar "¿está libre?" y luego sentarte (y que otro se siente en medio), intentas sentarte y el cine te dice "ocupado". Un solo camino, sin trampas.

### 3.4 Concurrencia optimista con `xmin`

- **Qué problema resuelve:** dos operadores leen la misma cuenta: uno la bloquea, el otro intenta cerrarla. Sin protección, el segundo pisaría al primero con datos viejos.
- **Dónde está:** `CuentaConfiguracion` mapea una propiedad sombra `Version` a la columna de sistema `xmin` de PostgreSQL. EF añade `WHERE xmin = <el que leí>` a cada `UPDATE`; si no afecta ninguna fila, alguien cambió la cuenta y EF lanza `DbUpdateConcurrencyException`, que el `DbContext` traduce a `ConflictoDeConcurrenciaException` (HTTP 409).
- **Analogía:** un documento compartido con "número de revisión". Antes de guardar compruebas que sigue en la revisión que leíste; si no, te avisan y vuelves a leer.
- **Por qué no se reintenta solo:** al reintentar, la regla (¿se puede cerrar una cuenta ya bloqueada?) debe evaluarse sobre el estado **nuevo**; lo decide el operador, no el sistema.
- *Esto adelanta un tema del Sprint 4 (saldos). Conviene estudiarlo.*

### 3.5 Puertos y adaptadores (Clean Architecture en acción)

- **Qué problema resuelve:** que los casos de uso no dependan de EF Core, PostgreSQL ni HTTP. Así se pueden probar en memoria (Application.Tests usa repositorios falsos) y cambiar la tecnología sin tocar las reglas.
- **Dónde está:** `IClienteRepositorio`, `ICuentaRepositorio`, `IUnidadDeTrabajo`, `IGeneradorDeNumeroDeCuenta` (puertos, en Application) y sus implementaciones en Infrastructure.
- **Analogía:** el enchufe de la pared. El electrodoméstico (el caso de uso) solo conoce la forma del enchufe (la interfaz); no le importa si detrás hay una central hidroeléctrica o solar.

### Un detalle técnico que vale la pena entender: microsegundos

.NET mide el tiempo en pasos de 100 nanosegundos; PostgreSQL guarda microsegundos. Si el handler devolviera la hora tal cual, la respuesta de "crear" diría `...480269` y un `GET` posterior diría `...48026`. Por eso `RelojExtensiones.AhoraEnMicrosegundos()` trunca la hora **antes** de crear la entidad: lo que se devuelve es exactamente lo que queda guardado.

## 4. Para practicar (Copiar → Modificar → Recrear)

### Ejercicio 1: un value object nuevo

- **Copiar:** abre `Domain/Clientes/Telefono.cs` y su prueba `tests/CoreBancario.Domain.Tests/Clientes/TelefonoTests.cs`.
- **Modificar:** crea (en un archivo aparte, sin tocar nada existente) un `CodigoPostal` que acepte exactamente 6 dígitos ASCII y quite espacios. Escribe primero 3 pruebas (válido, con letras, 5 dígitos).
- **Recrear:** borra tu `CodigoPostal` y reescríbelo sin mirar `Telefono`. Pregúntate: ¿por qué `TryCrear` devuelve `bool` y `Crear` lanza excepción?

### Ejercicio 2: leer el error que la base devuelve

- **Copiar:** mira `CoreBancarioDbContext.GuardarCambiosAsync` y la prueba `F002_RN18_NumeroRepetido_LoRechazaElIndiceUnico` en Infrastructure.Tests.
- **Modificar:** con `docker compose up -d`, intenta insertar a mano en `psql` dos cuentas con el mismo `numero` y mira el mensaje y el `constraint name`. Luego cambia temporalmente el nombre `ux_cuentas_numero` en `CoreBancarioDbContext` y observa qué prueba falla (y por qué).
- **Recrear:** explica con tus palabras por qué el nombre del índice es parte del contrato entre la migración y el `DbContext`.

### Ejercicio 3: un caso de uso nuevo

- **Copiar:** `ObtenerCuentaHandler` (el más pequeño), su consulta y la ruta `GET /cuentas/{id}`.
- **Modificar:** crea `ListarCuentasDeClienteHandler` que devuelva solo la lista de `CuentaDto` de un cliente (sin la ficha). Empieza por una prueba en Application.Tests con los repositorios en memoria.
- **Recrear:** dibuja de memoria el recorrido de tu petición (endpoint → handler → repositorio → base) y marca en qué capa vive cada paso.

## 5. Qué debes revisar tú (autor)

- La **migración** `Persistencia/Migraciones/*_Inicial.cs`: el archivo C# menciona la columna `xmin`, pero el SQL que genera Npgsql la omite (es una columna de sistema). Compruébalo con `dotnet ef migrations script`.
- `RelojExtensiones.cs` (truncar a microsegundos): ¿estás de acuerdo en hacerlo en Application y no en el dominio?
- Que el mensaje de `detail` en el 500 nunca contenga texto interno (RNF-04) y que `BadHttpRequestException` use un texto propio en vez del del framework.
