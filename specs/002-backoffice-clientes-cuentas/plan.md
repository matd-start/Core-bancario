# Plan — 002 Backoffice: clientes y cuentas

Estado: Aprobado (2026-10-05)
Spec: [spec.md](spec.md)

## 1. Resumen del diseño

El dominio crece en dos frentes. **`Cliente`** es una entidad nueva con value objects que concentran las reglas de formato y la normalización: `Documento`, `NombreDePersona`, `Correo` y `Telefono`. **`Cuenta`** pasa a recibir un `NumeroDeCuenta` (10 dígitos con verificador Luhn) y las fechas de apertura y cierre. Application tiene un handler por caso de uso. Cada handler valida la entrada acumulando **todos** los errores (usa el `TryCrear` de los value objects) y devuelve un `Resultado<T>` (ADR-0010, pendiente de confirmar). Después llama al dominio y guarda con un único `SaveChanges`, así que cada operación es atómica. Infrastructure persiste con EF Core + Npgsql, nombres en snake_case y una migración inicial. Hay tres protecciones en la base:

- un índice único por documento, cuya violación (`23505`) se traduce al mismo error de duplicado;
- un índice único por número de cuenta;
- `xmin` como token de concurrencia **por fila** en `cuentas`, cuyo conflicto se traduce a un 409 sin reintento automático (ADR-0012).

La Api son Minimal APIs delgadas. Un `IExceptionHandler` traduce cada error a `ProblemDetails` con un `codigo` estable (ADR-0011). Las pruebas de integración corren contra PostgreSQL en Testcontainers, con un contenedor por proyecto de tests y las migraciones reales. La forma de despachar los casos de uso (D-06, ADR-0009): **handlers llamados directamente**, decidido por el autor. La sección 8.1 dice qué cambiaría con un mediador.

## 2. Impacto por capa

| Capa | Qué cambia | Archivos |
|---|---|---|
| Domain | Nuevo `Clientes/` (`TipoDocumento`, `Documento`, `NombreDePersona`, `Correo`, `Telefono`, `Cliente`) y `Cuentas/NumeroDeCuenta`. Cambian `Cuenta` (número tipado, fechas, firmas de `Abrir` y `Cerrar`), `Moneda` (`DesdeCodigo`, `TryDesdeCodigo`) y `Dinero` (el constructor privado aplica la forma canónica) | `src/CoreBancario.Domain/Clientes/*.cs`, `src/CoreBancario.Domain/Cuentas/NumeroDeCuenta.cs`, `src/CoreBancario.Domain/Cuentas/Cuenta.cs`, `src/CoreBancario.Domain/Monetario/Moneda.cs`, `src/CoreBancario.Domain/Monetario/Dinero.cs` |
| Domain.Tests | Pruebas nuevas de los value objects, `Cliente` y `NumeroDeCuenta`. **Se adaptan las pruebas de la 001** que usan `Cuenta.Abrir(string…)` y `Cerrar()` (lista en la sección 7) | `tests/CoreBancario.Domain.Tests/Clientes/*.cs`, `tests/CoreBancario.Domain.Tests/Cuentas/*.cs`, `tests/CoreBancario.Domain.Tests/Monetario/MonedaTests.cs` |
| Application | Nuevo: `Resultado<T>`, excepciones de aplicación, puertos (`IClienteRepositorio`, `ICuentaRepositorio`, `IGeneradorDeNumeroDeCuenta`, `IUnidadDeTrabajo`), DTOs, seis handlers y `AddAplicacion` | `src/CoreBancario.Application/Comun/*.cs`, `src/CoreBancario.Application/Clientes/*.cs`, `src/CoreBancario.Application/Cuentas/*.cs`, `src/CoreBancario.Application/DependencyInjection.cs`, `.csproj` |
| Application.Tests | Primeras pruebas, con fakes en memoria; se quita `--ignore-exit-code 8` | `tests/CoreBancario.Application.Tests/**` |
| Infrastructure | Nuevo: `CoreBancarioDbContext` (implementa `IUnidadDeTrabajo` y traduce errores de la base), configuraciones de EF, repositorios, generador aleatorio de números, migración inicial, fábrica de diseño y `AddInfraestructura` | `src/CoreBancario.Infrastructure/Persistencia/**`, `src/CoreBancario.Infrastructure/Cuentas/GeneradorAleatorioDeNumeroDeCuenta.cs`, `src/CoreBancario.Infrastructure/DependencyInjection.cs`, `.csproj`, `.config/dotnet-tools.json` |
| Infrastructure.Tests | Primeras pruebas, con Testcontainers; se quita `--ignore-exit-code 8` | `tests/CoreBancario.Infrastructure.Tests/**` |
| Api | Endpoints de clientes y cuentas, catálogo de errores, `IExceptionHandler`, configuración en `Program.cs`, `public partial class Program` | `src/CoreBancario.Api/Program.cs`, `src/CoreBancario.Api/Errores/*.cs`, `src/CoreBancario.Api/Clientes/*.cs`, `src/CoreBancario.Api/Cuentas/*.cs`, `.csproj` |
| Api.Tests | Primeras pruebas, con `WebApplicationFactory` y Testcontainers; se quita `--ignore-exit-code 8` | `tests/CoreBancario.Api.Tests/**` |

## 3. Contratos

### Endpoints

Todas las rutas usan el **id** (`Guid`), como piden RF-05 y CA-17. El número de cuenta todavía no se usa como clave de búsqueda; lo necesitará el S5 para la cuenta destino (YAGNI). Los cambios de estado son acciones con nombre (`POST …/bloquear`), igual que los métodos del dominio, en lugar de un `PATCH` con el estado deseado: así la intención queda explícita y RN-14 se evalúa en un solo lugar.

| Método | Ruta | Request | Respuestas | Cubre |
|---|---|---|---|---|
| POST | `/clientes` | `RegistrarClienteRequest` | 201 `ClienteDto` (+ `Location: /clientes/{id}`) · 400 `datos-invalidos` · 409 `documento-duplicado` | RF-01, CA-01…CA-05, CL-01…CL-08 |
| GET | `/clientes/{clienteId:guid}` | — | 200 `FichaClienteDto` · 404 `no-encontrado` | RF-12, CL-18, CL-19 |
| GET | `/clientes/por-documento?tipoDocumento=CC&numeroDocumento=1.234.567` | query | 200 `FichaClienteDto` · 400 `datos-invalidos` · 404 `no-encontrado` | RF-12, CA-15, CA-16, CL-17, CL-18 |
| POST | `/clientes/{clienteId:guid}/cuentas` | `AbrirCuentaRequest` | 201 `CuentaDto` (+ `Location: /cuentas/{id}`) · 400 `datos-invalidos` · 404 `no-encontrado` | RF-02, CA-06…CA-09, CL-09…CL-12 |
| GET | `/cuentas/{cuentaId:guid}` | — | 200 `CuentaDto` · 404 `no-encontrado` | RF-05, CA-17, CL-18 |
| POST | `/cuentas/{cuentaId:guid}/bloquear` | — | 200 `CuentaDto` · 404 · 409 `conflicto-de-concurrencia` · 422 `transicion-no-permitida` | RF-03, CA-10, CA-11, CA-14, CL-13, CL-15, CL-16 |
| POST | `/cuentas/{cuentaId:guid}/desbloquear` | — | 200 `CuentaDto` · 404 · 409 · 422 `transicion-no-permitida` | RF-03, CA-10, CL-13, CL-15, CL-16 |
| POST | `/cuentas/{cuentaId:guid}/cerrar` | — | 200 `CuentaDto` · 404 · 409 · 422 `transicion-no-permitida` o `saldo-distinto-de-cero` | RF-03, CA-11…CA-14, CL-13…CL-16 |

Cualquier operación puede responder 500 `error-inesperado` sin detalles (RNF-04).

**Forma de los cuerpos (JSON, camelCase, el valor por defecto de ASP.NET Core):**

```json
// RegistrarClienteRequest
{ "tipoDocumento": "CC", "numeroDocumento": "1.234.567", "nombres": "María José",
  "apellidos": "Núñez", "correo": "maria@example.com", "telefono": "+57 300 123 4567" }

// ClienteDto
{ "id": "0199…", "tipoDocumento": "CC", "numeroDocumento": "1234567", "nombres": "María José",
  "apellidos": "Núñez", "correo": "maria@example.com", "telefono": "+573001234567",
  "fechaRegistro": "2026-10-05T14:03:11.123456+00:00" }

// AbrirCuentaRequest
{ "moneda": "USD" }

// CuentaDto
{ "id": "0199…", "numero": "1234567897", "clienteId": "0199…", "moneda": "USD", "estado": "Activa",
  "saldo": { "monto": "0.00", "moneda": "USD" },
  "fechaApertura": "2026-10-05T14:05:00+00:00", "fechaCierre": null }

// FichaClienteDto
{ "cliente": { …ClienteDto… }, "cuentas": [ …CuentaDto… ] }

// Error (ProblemDetails, RFC 9457) — ejemplo 409
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Conflicto", "status": 409,
  "detail": "Ya existe un cliente con ese tipo y número de documento.", "instance": "/clientes",
  "codigo": "documento-duplicado", "traceId": "00-…" }

// Datos inválidos (ValidationProblemDetails) — CA-04
{ "title": "Datos inválidos", "status": 400, "codigo": "datos-invalidos",
  "errors": { "numeroDocumento": ["…"], "correo": ["…"], "telefono": ["…"] } }
```

- **El monto viaja como texto** (`"0.00"`), en cultura invariante y en la forma canónica de `Dinero`. Lo pide el RNF de dinero de la spec de producto: el frontend no debe convertir montos a `number` (binario).
- `tipoDocumento`, `moneda` y `estado` viajan como texto: `CC`/`CE`/`PA`, `COP`/`USD` y `Activa`/`Bloqueada`/`Cerrada`.
- Los campos de `RegistrarClienteRequest` y `AbrirCuentaRequest` son **`string?`**, incluidos `tipoDocumento` y `moneda`. Así el model binding nunca rechaza el cuerpo por un campo ausente o un enum desconocido (eso cortaría la acumulación de errores de CA-04) y toda la validación ocurre en un solo lugar (ADR-0010).
- Las claves de `errors` son exactamente `tipoDocumento`, `numeroDocumento`, `nombres`, `apellidos`, `correo`, `telefono` (registrar y buscar) y `moneda` (abrir cuenta). Las pruebas comprueban estas claves, **no** los mensajes.

### Comandos, consultas y eventos

No hay eventos de dominio: ningún requisito pide notificar a terceros (YAGNI).

| Tipo | Nombre | Datos | Productor → consumidor | Cubre |
|---|---|---|---|---|
| Comando | `RegistrarClienteComando` | tipo, número, nombres, apellidos, correo, teléfono (todos `string?`) | Endpoint → `RegistrarClienteHandler` | RF-01 |
| Consulta | `ObtenerClienteConsulta` | `Guid ClienteId` | Endpoint → `ObtenerClienteHandler` | RF-12, CL-18, CL-19 |
| Consulta | `BuscarClientePorDocumentoConsulta` | `string? TipoDocumento`, `string? NumeroDocumento` | Endpoint → `BuscarClientePorDocumentoHandler` | RF-12, CL-17 |
| Comando | `AbrirCuentaComando` | `Guid ClienteId`, `string? Moneda` | Endpoint → `AbrirCuentaHandler` | RF-02 |
| Comando | `CambiarEstadoDeCuentaComando` | `Guid CuentaId`, `AccionDeEstado Accion` | Endpoint → `CambiarEstadoDeCuentaHandler` | RF-03 |
| Consulta | `ObtenerCuentaConsulta` | `Guid CuentaId` | Endpoint → `ObtenerCuentaHandler` | RF-05 |

### Firmas públicas

Las pruebas comprueban **tipos de excepción y claves de error**, no textos. Los mensajes van en español.

Estructura de carpetas y espacios de nombres (el espacio de nombres sigue a la carpeta):

```
src/CoreBancario.Domain/
  Monetario/Moneda.cs, Dinero.cs                       (cambian)
  Cuentas/Cuenta.cs                                    (cambia)
  Cuentas/NumeroDeCuenta.cs                            (nuevo)
  Clientes/                                            namespace CoreBancario.Domain.Clientes
    TipoDocumento.cs, Documento.cs, NombreDePersona.cs, Correo.cs, Telefono.cs, Cliente.cs
src/CoreBancario.Application/
  DependencyInjection.cs                               namespace CoreBancario.Application
  Comun/        Resultado.cs, IUnidadDeTrabajo.cs, RecursoNoEncontradoException.cs,
                ConflictoDeConcurrenciaException.cs, DineroDto.cs,
                RelojExtensiones.cs (internal; añadido en el Build)
  Clientes/     RegistrarClienteComando.cs, RegistrarClienteHandler.cs, ObtenerClienteConsulta.cs,
                ObtenerClienteHandler.cs, BuscarClientePorDocumentoConsulta.cs,
                BuscarClientePorDocumentoHandler.cs, ClienteDto.cs, FichaClienteDto.cs,
                IClienteRepositorio.cs, DocumentoDuplicadoException.cs,
                TiposDeDocumentoAceptados.cs, FichaClienteDtoFabrica.cs (internal; añadidos en el Build)
  Cuentas/      AbrirCuentaComando.cs, AbrirCuentaHandler.cs, CambiarEstadoDeCuentaComando.cs,
                AccionDeEstado.cs, CambiarEstadoDeCuentaHandler.cs, ObtenerCuentaConsulta.cs,
                ObtenerCuentaHandler.cs, CuentaDto.cs, ICuentaRepositorio.cs, IGeneradorDeNumeroDeCuenta.cs
src/CoreBancario.Infrastructure/
  DependencyInjection.cs                               namespace CoreBancario.Infrastructure
  Persistencia/ CoreBancarioDbContext.cs, ConfiguracionDeBaseDeDatos.cs, CoreBancarioDbContextFactory.cs
  Persistencia/Configuraciones/ ClienteConfiguracion.cs, CuentaConfiguracion.cs
  Persistencia/Repositorios/    ClienteRepositorio.cs, CuentaRepositorio.cs
  Persistencia/Migraciones/     (generadas por dotnet ef)
  Cuentas/      GeneradorAleatorioDeNumeroDeCuenta.cs
src/CoreBancario.Api/
  Program.cs
  Errores/      CodigosDeError.cs, ErrorHttp.cs, CatalogoDeErrores.cs, ManejadorDeErrores.cs, RespuestasDeError.cs
  Clientes/     EndpointsDeClientes.cs, RegistrarClienteRequest.cs
  Cuentas/      EndpointsDeCuentas.cs, AbrirCuentaRequest.cs
```

#### Domain

```csharp
// ───────────── src/CoreBancario.Domain/Monetario/Moneda.cs (se añade) ─────────────
namespace CoreBancario.Domain.Monetario;

public sealed record Moneda
{
    // … COP, USD, Codigo, Precision y ToString() sin cambios …

    /// <summary>Devuelve COP o USD según el código exacto ("COP", "USD"; sensible a mayúsculas). Lo usa la persistencia.</summary>
    /// <exception cref="ArgumentException">codigo es null, vacío o no es "COP" ni "USD" (error de programación, ADR-0008).</exception>
    public static Moneda DesdeCodigo(string codigo);

    /// <summary>Versión sin excepciones para validar la entrada (CL-10). "cop", "EUR", "" y null devuelven false.</summary>
    public static bool TryDesdeCodigo(string? codigo, [NotNullWhen(true)] out Moneda? moneda);
}

// ───────────── src/CoreBancario.Domain/Monetario/Dinero.cs (cambia solo por dentro) ─────────────
// El constructor privado Dinero(decimal monto, Moneda moneda) aplica AFormaCanonica. EF Core materializa
// el complex type Saldo a través de ese constructor (sección 4), así que un monto leído de numeric(19,2)
// (por ejemplo 50000.00 COP) recupera su forma canónica (50000). La API pública no cambia.

// ───────────── src/CoreBancario.Domain/Cuentas/NumeroDeCuenta.cs ─────────────
namespace CoreBancario.Domain.Cuentas;

/// <summary>
/// RN-18. Valor: 10 dígitos ASCII; el primero no es 0; el último es el dígito verificador Luhn de los 9 primeros.
/// La generación aleatoria no vive aquí: la hace IGeneradorDeNumeroDeCuenta (Application) con DesdeCuerpo (ADR-0013).
/// </summary>
public sealed record NumeroDeCuenta
{
    public string Valor { get; }

    private NumeroDeCuenta(string valor);

    /// <summary>Añade a cuerpo (los 9 primeros dígitos) su dígito verificador Luhn. "123456789" → "1234567897".</summary>
    /// <exception cref="ArgumentException">cuerpo no tiene exactamente 9 dígitos ASCII o empieza por 0.</exception>
    public static NumeroDeCuenta DesdeCuerpo(string cuerpo);

    /// <summary>Reconstruye un número ya asignado (persistencia, pruebas) comprobando todas sus reglas.</summary>
    /// <exception cref="ArgumentException">valor no tiene 10 dígitos ASCII, empieza por 0 o su verificador no es correcto.</exception>
    public static NumeroDeCuenta Crear(string valor);

    /// <returns>Valor.</returns>
    public override string ToString();
}

// ───────────── src/CoreBancario.Domain/Cuentas/Cuenta.cs (cambia) ─────────────
namespace CoreBancario.Domain.Cuentas;

public sealed class Cuenta
{
    public Guid Id { get; }
    public NumeroDeCuenta Numero { get; }                // antes: string
    public Guid ClienteId { get; }
    public Moneda Moneda { get; }
    public EstadoCuenta Estado { get; private set; }
    public Dinero Saldo { get; private set; }
    public DateTimeOffset FechaApertura { get; }         // nuevo; no cambia
    public DateTimeOffset? FechaCierre { get; private set; }  // nuevo; solo con Estado == Cerrada

    /// <summary>EF Core enlaza este constructor por nombre de parámetro: los nombres deben seguir igual que las propiedades.</summary>
    private Cuenta(Guid id, NumeroDeCuenta numero, Guid clienteId, Moneda moneda, DateTimeOffset fechaApertura);

    /// <summary>
    /// Abre una cuenta Activa con saldo 0 en la moneda indicada (RN-02), sin fecha de cierre.
    /// Id = Guid.CreateVersion7(fechaApertura): ordenado en el tiempo y sin leer el reloj (resuelve el riesgo de la 001).
    /// </summary>
    /// <exception cref="ArgumentNullException">numero o moneda es null.</exception>
    public static Cuenta Abrir(NumeroDeCuenta numero, Guid clienteId, Moneda moneda, DateTimeOffset fechaApertura);

    // Acreditar, Debitar, Bloquear y Desbloquear: sin cambios.

    /// <summary>Activa → Cerrada, solo con saldo cero; fija FechaCierre = fechaCierre. Orden: transición → saldo → cambio (CL-12/CL-13 de la 001).</summary>
    /// <exception cref="TransicionNoPermitidaException">Estado no es Activa (RN-14, RN-06). FechaCierre sigue null.</exception>
    /// <exception cref="SaldoDistintoDeCeroException">Saldo distinto de cero (RN-06). FechaCierre sigue null.</exception>
    public void Cerrar(DateTimeOffset fechaCierre);
}

// ───────────── src/CoreBancario.Domain/Clientes/TipoDocumento.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>CC: cédula de ciudadanía. CE: cédula de extranjería. PA: pasaporte (RN-17).</summary>
public enum TipoDocumento { CC, CE, PA }

// ───────────── src/CoreBancario.Domain/Clientes/Documento.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>
/// RN-17. Tipo + número normalizado. Normalizar: quitar espacios en blanco (char.IsWhiteSpace), '.' y '-', y pasar
/// a mayúsculas invariantes. Formato tras normalizar: CC y CE, solo dígitos ASCII, de 3 a 10, sin empezar por '0';
/// PA, letras ASCII y dígitos ASCII, de 5 a 15. Igualdad de record: (Tipo, Numero) ya normalizado.
/// </summary>
public sealed record Documento
{
    public TipoDocumento Tipo { get; }
    public string Numero { get; }

    private Documento(TipoDocumento tipo, string numero);

    /// <summary>Normaliza y valida. false si numero es null o no cumple el formato de su tipo, o si tipo no está definido.</summary>
    public static bool TryCrear(TipoDocumento tipo, string? numero, [NotNullWhen(true)] out Documento? documento);

    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false (error de programación: quien llama debió validar).</exception>
    public static Documento Crear(TipoDocumento tipo, string numero);

    /// <returns>"CC 1234567".</returns>
    public override string ToString();
}

// ───────────── src/CoreBancario.Domain/Clientes/NombreDePersona.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>
/// Nombres o apellidos. Se quitan espacios al inicio y al final y se normaliza a Unicode NFC (string.Normalize()).
/// Válido: de 1 a 100 caracteres; solo letras (char.IsLetter, incluye tildes y ñ), espacios, apóstrofo (') y guion (-);
/// con al menos una letra (CL-06).
/// </summary>
public sealed record NombreDePersona
{
    public string Valor { get; }
    private NombreDePersona(string valor);
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out NombreDePersona? nombre);
    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static NombreDePersona Crear(string valor);
    public override string ToString();   // Valor
}

// ───────────── src/CoreBancario.Domain/Clientes/Correo.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>
/// Se quitan espacios al inicio y al final. Válido: como máximo 254 caracteres; exactamente una '@'; parte local
/// y dominio no vacíos; ningún espacio en blanco ni carácter de control dentro. No es único (sección 9 de la spec).
/// </summary>
public sealed record Correo
{
    public string Valor { get; }
    private Correo(string valor);
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Correo? correo);
    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static Correo Crear(string valor);
    public override string ToString();   // Valor
}

// ───────────── src/CoreBancario.Domain/Clientes/Telefono.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>
/// E.164. Normalizar: quitar espacios en blanco, '-', '(' y ')'. Válido tras normalizar: '+' seguido de 8 a 15
/// dígitos ASCII (CL-07). "+57 (300) 123-4567" → "+573001234567".
/// </summary>
public sealed record Telefono
{
    public string Valor { get; }
    private Telefono(string valor);
    public static bool TryCrear(string? valor, [NotNullWhen(true)] out Telefono? telefono);
    /// <exception cref="ArgumentException">Mismas condiciones en las que TryCrear devuelve false.</exception>
    public static Telefono Crear(string valor);
    public override string ToString();   // Valor
}

// ───────────── src/CoreBancario.Domain/Clientes/Cliente.cs ─────────────
namespace CoreBancario.Domain.Clientes;

/// <summary>Persona natural titular de cuentas. Inmutable en la fase 1: editar datos está fuera de alcance.</summary>
public sealed class Cliente
{
    public Guid Id { get; }
    public Documento Documento { get; }
    public NombreDePersona Nombres { get; }
    public NombreDePersona Apellidos { get; }
    public Correo Correo { get; }
    public Telefono Telefono { get; }
    public DateTimeOffset FechaRegistro { get; }

    /// <summary>
    /// Solo para EF Core: Documento es una navegación owned y EF no la puede pasar por constructor.
    /// Lleva #pragma warning disable/restore CS8618 acotado a este constructor, con un comentario que lo explica.
    /// </summary>
    private Cliente();

    /// <summary>Id = Guid.CreateVersion7(fechaRegistro). La unicidad del documento (RN-17) la garantiza la base (sección 4).</summary>
    /// <exception cref="ArgumentNullException">Algún value object es null.</exception>
    public static Cliente Registrar(Documento documento, NombreDePersona nombres, NombreDePersona apellidos,
        Correo correo, Telefono telefono, DateTimeOffset fechaRegistro);
}
```

#### Application

```csharp
// ───────────── src/CoreBancario.Application/Comun/Resultado.cs ─────────────
namespace CoreBancario.Application.Comun;

/// <summary>
/// Éxito con un valor, o datos inválidos con TODOS los errores por campo (RNF-03, CL-04; ADR-0008 y ADR-0010).
/// Solo representa "datos inválidos"; los demás errores siguen siendo excepciones.
/// </summary>
public sealed class Resultado<T>
{
    public bool EsExito { get; }

    /// <exception cref="InvalidOperationException">Se lee con EsExito == false.</exception>
    public T Valor { get; }

    /// <summary>Campo (clave camelCase del contrato) → mensajes. Vacío si EsExito.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    public static Resultado<T> Exito(T valor);

    /// <exception cref="ArgumentException">errores está vacío.</exception>
    public static Resultado<T> Invalido(IReadOnlyDictionary<string, string[]> errores);
}

// ───────────── src/CoreBancario.Application/Comun/IUnidadDeTrabajo.cs ─────────────
namespace CoreBancario.Application.Comun;

public interface IUnidadDeTrabajo
{
    /// <summary>Guarda todos los cambios pendientes en una sola transacción (RNF-06).</summary>
    /// <exception cref="Clientes.DocumentoDuplicadoException">Violación del índice único del documento (CL-01, CL-03).</exception>
    /// <exception cref="ConflictoDeConcurrenciaException">La fila cambió desde que se leyó (xmin, CL-15) o choque improbable del número de cuenta (CL-11).</exception>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

// ───────────── src/CoreBancario.Application/Comun/*Exception.cs ─────────────
namespace CoreBancario.Application.Comun;

/// <summary>404 (RNF-03): el cliente o la cuenta no existen (CL-09, CL-16, CL-18).</summary>
public sealed class RecursoNoEncontradoException : Exception
{
    public RecursoNoEncontradoException(string mensaje);
}

/// <summary>409 (RNF-03): otra operación modificó la misma fila (CL-15). Quien llama debe volver a leer y reintentar.</summary>
public sealed class ConflictoDeConcurrenciaException : Exception
{
    public ConflictoDeConcurrenciaException(Exception? causa = null);
}

// ───────────── src/CoreBancario.Application/Clientes/DocumentoDuplicadoException.cs ─────────────
namespace CoreBancario.Application.Clientes;

/// <summary>409 (RNF-03): ya existe un cliente con ese tipo y número normalizado (RN-17, CL-01, CL-03, CL-08).</summary>
public sealed class DocumentoDuplicadoException : Exception
{
    public DocumentoDuplicadoException(Exception? causa = null);
}

// ───────────── DTOs ─────────────
namespace CoreBancario.Application.Comun;
/// <summary>Monto en texto, cultura invariante y forma canónica: "0" COP, "0.00" USD.</summary>
public sealed record DineroDto(string Monto, string Moneda)
{
    public static DineroDto Desde(Dinero dinero);
}

namespace CoreBancario.Application.Clientes;
public sealed record ClienteDto(Guid Id, string TipoDocumento, string NumeroDocumento, string Nombres,
    string Apellidos, string Correo, string Telefono, DateTimeOffset FechaRegistro)
{
    public static ClienteDto Desde(Cliente cliente);
}
/// <summary>Cuentas ordenadas por FechaApertura y después por Id. Lista vacía si no tiene (CL-19).</summary>
public sealed record FichaClienteDto(ClienteDto Cliente, IReadOnlyList<CuentaDto> Cuentas);

namespace CoreBancario.Application.Cuentas;
public sealed record CuentaDto(Guid Id, string Numero, Guid ClienteId, string Moneda, string Estado,
    DineroDto Saldo, DateTimeOffset FechaApertura, DateTimeOffset? FechaCierre)
{
    public static CuentaDto Desde(Cuenta cuenta);
}

// ───────────── Puertos ─────────────
namespace CoreBancario.Application.Clientes;
public interface IClienteRepositorio
{
    void Agregar(Cliente cliente);
    Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken cancellationToken);
    Task<Cliente?> ObtenerPorDocumentoAsync(Documento documento, CancellationToken cancellationToken);
}

namespace CoreBancario.Application.Cuentas;
public interface ICuentaRepositorio
{
    void Agregar(Cuenta cuenta);
    /// <summary>Con seguimiento de cambios: lo usa también el cambio de estado.</summary>
    Task<Cuenta?> ObtenerAsync(Guid cuentaId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken);
    Task<bool> ExisteNumeroAsync(NumeroDeCuenta numero, CancellationToken cancellationToken);
}

/// <summary>Fuente de números de cuenta no predecibles (RN-18). Interfaz por CL-11: las pruebas fuerzan una colisión.</summary>
public interface IGeneradorDeNumeroDeCuenta
{
    NumeroDeCuenta Generar();
}

// ───────────── Comandos y consultas ─────────────
namespace CoreBancario.Application.Clientes;
public sealed record RegistrarClienteComando(string? TipoDocumento, string? NumeroDocumento, string? Nombres,
    string? Apellidos, string? Correo, string? Telefono);
public sealed record ObtenerClienteConsulta(Guid ClienteId);
public sealed record BuscarClientePorDocumentoConsulta(string? TipoDocumento, string? NumeroDocumento);

namespace CoreBancario.Application.Cuentas;
public sealed record AbrirCuentaComando(Guid ClienteId, string? Moneda);
public enum AccionDeEstado { Bloquear, Desbloquear, Cerrar }
public sealed record CambiarEstadoDeCuentaComando(Guid CuentaId, AccionDeEstado Accion);
public sealed record ObtenerCuentaConsulta(Guid CuentaId);

// ───────────── Handlers (uno por caso de uso; clases concretas, sin interfaz: ADR-0009) ─────────────
// Cambio del Build: los handlers que escriben usan reloj.AhoraEnMicrosegundos() (Application/Comun/RelojExtensiones.cs)
// en vez de reloj.GetUtcNow(). .NET mide en ticks de 100 ns y timestamptz guarda microsegundos; sin truncar, la
// respuesta del POST no coincidía con lo que devuelve el GET (CA01 y CA17 comparan exacto). Decisión del autor (2026-10-06).
namespace CoreBancario.Application.Clientes;

public sealed class RegistrarClienteHandler(IClienteRepositorio clientes, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>
    /// 1) Valida TODOS los campos y acumula errores (sección 5, "Validación de registro"); si hay alguno, devuelve
    ///    Invalido sin tocar repositorio ni unidad de trabajo. 2) Cliente.Registrar(…, reloj.AhoraEnMicrosegundos()).
    /// 3) Agregar + GuardarCambiosAsync. Sin consulta previa de existencia: el duplicado lo detecta el índice único.
    /// </summary>
    /// <exception cref="DocumentoDuplicadoException">CL-01, CL-03, CL-08.</exception>
    public Task<Resultado<ClienteDto>> EjecutarAsync(RegistrarClienteComando comando, CancellationToken cancellationToken);
}

public sealed class ObtenerClienteHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas)
{
    /// <exception cref="Comun.RecursoNoEncontradoException">No existe el cliente (CL-18).</exception>
    public Task<FichaClienteDto> EjecutarAsync(ObtenerClienteConsulta consulta, CancellationToken cancellationToken);
}

public sealed class BuscarClientePorDocumentoHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas)
{
    /// <summary>
    /// Invalido si tipoDocumento falta o no es CC/CE/PA, o si numeroDocumento falta o queda vacío al normalizar.
    /// Si el número no cumple el formato de su tipo → RecursoNoEncontradoException (no puede existir un cliente así).
    /// </summary>
    /// <exception cref="Comun.RecursoNoEncontradoException">Documento no registrado (CL-18).</exception>
    public Task<Resultado<FichaClienteDto>> EjecutarAsync(BuscarClientePorDocumentoConsulta consulta, CancellationToken cancellationToken);
}

namespace CoreBancario.Application.Cuentas;

public sealed class AbrirCuentaHandler(IClienteRepositorio clientes, ICuentaRepositorio cuentas,
    IGeneradorDeNumeroDeCuenta generador, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    public const int MaximoDeIntentos = 5;

    /// <summary>
    /// 1) Moneda: Moneda.TryDesdeCodigo; si falla → Invalido con la clave "moneda" (CL-10), sin tocar la base.
    /// 2) Cliente: ObtenerAsync; si no existe → RecursoNoEncontradoException (CL-09).
    /// 3) Número: hasta MaximoDeIntentos veces, generador.Generar() y ExisteNumeroAsync; el primero libre se usa (CL-11).
    /// 4) Cuenta.Abrir(numero, clienteId, moneda, reloj.AhoraEnMicrosegundos()) + Agregar + GuardarCambiosAsync.
    /// </summary>
    /// <exception cref="Comun.RecursoNoEncontradoException">CL-09.</exception>
    /// <exception cref="InvalidOperationException">Los MaximoDeIntentos números generados ya existían (espacio agotado: 500).</exception>
    public Task<Resultado<CuentaDto>> EjecutarAsync(AbrirCuentaComando comando, CancellationToken cancellationToken);
}

public sealed class CambiarEstadoDeCuentaHandler(ICuentaRepositorio cuentas, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>ObtenerAsync → Bloquear() | Desbloquear() | Cerrar(reloj.AhoraEnMicrosegundos()) → GuardarCambiosAsync. Sin reintento automático (ADR-0012).</summary>
    /// <exception cref="Comun.RecursoNoEncontradoException">CL-16.</exception>
    /// <exception cref="TransicionNoPermitidaException">RN-14, CL-13.</exception>
    /// <exception cref="SaldoDistintoDeCeroException">RN-06, CL-14.</exception>
    /// <exception cref="Comun.ConflictoDeConcurrenciaException">CL-15.</exception>
    public Task<CuentaDto> EjecutarAsync(CambiarEstadoDeCuentaComando comando, CancellationToken cancellationToken);
}

public sealed class ObtenerCuentaHandler(ICuentaRepositorio cuentas)
{
    /// <exception cref="Comun.RecursoNoEncontradoException">CL-18.</exception>
    public Task<CuentaDto> EjecutarAsync(ObtenerCuentaConsulta consulta, CancellationToken cancellationToken);
}

// ───────────── src/CoreBancario.Application/DependencyInjection.cs ─────────────
namespace CoreBancario.Application;
public static class DependencyInjection
{
    /// <summary>Registra los seis handlers como Scoped y TryAddSingleton(TimeProvider.System).</summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection services);
}
```

#### Infrastructure

```csharp
// ───────────── src/CoreBancario.Infrastructure/Persistencia/CoreBancarioDbContext.cs ─────────────
namespace CoreBancario.Infrastructure.Persistencia;

public sealed class CoreBancarioDbContext(DbContextOptions<CoreBancarioDbContext> options) : DbContext(options), IUnidadDeTrabajo
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    /// <summary>
    /// SaveChangesAsync con traducción de errores (sección 5):
    /// DbUpdateConcurrencyException → ConflictoDeConcurrenciaException;
    /// DbUpdateException con PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } y
    ///   ConstraintName "ux_clientes_documento" → DocumentoDuplicadoException;
    ///   ConstraintName "ux_cuentas_numero"     → ConflictoDeConcurrenciaException;
    /// cualquier otra se propaga sin tocar (será un 500).
    /// </summary>
    public Task GuardarCambiosAsync(CancellationToken cancellationToken);
}

// ───────────── src/CoreBancario.Infrastructure/Persistencia/ConfiguracionDeBaseDeDatos.cs ─────────────
namespace CoreBancario.Infrastructure.Persistencia;

public static class ConfiguracionDeBaseDeDatos
{
    /// <summary>Nombre de la cadena en configuración: ConnectionStrings:CoreBancario.</summary>
    public const string NombreCadenaDeConexion = "CoreBancario";

    /// <summary>UseNpgsql(cadena) + UseSnakeCaseNamingConvention(). Lo usan la DI, la fábrica de diseño y las pruebas (una sola configuración).</summary>
    public static DbContextOptionsBuilder UsarPostgres(this DbContextOptionsBuilder opciones, string cadenaDeConexion);
}

/// <summary>Para `dotnet ef`: lee la variable de entorno ConnectionStrings__CoreBancario o usa una cadena de relleno (migrations add no se conecta).</summary>
public sealed class CoreBancarioDbContextFactory : IDesignTimeDbContextFactory<CoreBancarioDbContext>
{
    public CoreBancarioDbContext CreateDbContext(string[] args);
}

// ───────────── Repositorios y generador (públicos para las pruebas de integración) ─────────────
namespace CoreBancario.Infrastructure.Persistencia.Repositorios;
public sealed class ClienteRepositorio(CoreBancarioDbContext db) : IClienteRepositorio { /* … */ }
public sealed class CuentaRepositorio(CoreBancarioDbContext db) : ICuentaRepositorio { /* … */ }

namespace CoreBancario.Infrastructure.Cuentas;
/// <summary>RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000) → cuerpo de 9 dígitos sin 0 inicial → NumeroDeCuenta.DesdeCuerpo.</summary>
public sealed class GeneradorAleatorioDeNumeroDeCuenta : IGeneradorDeNumeroDeCuenta
{
    public NumeroDeCuenta Generar();
}

// ───────────── src/CoreBancario.Infrastructure/DependencyInjection.cs ─────────────
namespace CoreBancario.Infrastructure;
public static class DependencyInjection
{
    /// <summary>
    /// AddDbContext con UsarPostgres; la cadena se lee de configuracion DENTRO del callback de opciones (lazy), para
    /// que WebApplicationFactory pueda sobrescribirla. Si falta → InvalidOperationException con mensaje claro.
    /// Registra IUnidadDeTrabajo (el mismo DbContext con scope), los dos repositorios (Scoped) y el generador (Singleton).
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuracion);
}
```

#### Api

```csharp
// ───────────── src/CoreBancario.Api/Errores/CodigosDeError.cs ─────────────
namespace CoreBancario.Api.Errores;

/// <summary>Identificadores estables del contrato con core-bancario-web (RNF-03, ADR-0011). Nunca se renombran.</summary>
public static class CodigosDeError
{
    public const string DatosInvalidos = "datos-invalidos";                 // 400
    public const string NoEncontrado = "no-encontrado";                     // 404
    public const string DocumentoDuplicado = "documento-duplicado";         // 409
    public const string ConflictoDeConcurrencia = "conflicto-de-concurrencia"; // 409
    public const string TransicionNoPermitida = "transicion-no-permitida";  // 422
    public const string SaldoDistintoDeCero = "saldo-distinto-de-cero";     // 422
    public const string OperacionNoPermitida = "operacion-no-permitida";    // 422
    public const string SaldoInsuficiente = "saldo-insuficiente";           // 422
    public const string MontoNoPositivo = "monto-no-positivo";              // 422
    public const string MonedasDistintas = "monedas-distintas";             // 422
    public const string MontoNegativo = "monto-negativo";                   // 422
    public const string PrecisionExcedida = "precision-excedida";           // 422
    public const string ReglaDeNegocio = "regla-de-negocio";                // 422, respaldo: una prueba exige que no se use
    public const string ErrorInesperado = "error-inesperado";               // 500
}

// ───────────── src/CoreBancario.Api/Errores/ErrorHttp.cs ─────────────
namespace CoreBancario.Api.Errores;
public sealed record ErrorHttp(int Estado, string Codigo, string Titulo);

// ───────────── src/CoreBancario.Api/Errores/CatalogoDeErrores.cs ─────────────
namespace CoreBancario.Api.Errores;

public static class CatalogoDeErrores
{
    /// <summary>Tabla de la sección 5 ("Catálogo de errores"). Una excepción no listada → 500 ErrorInesperado.</summary>
    public static ErrorHttp Clasificar(Exception excepcion);
}

// ───────────── src/CoreBancario.Api/Errores/ManejadorDeErrores.cs ─────────────
namespace CoreBancario.Api.Errores;

/// <summary>
/// Escribe ProblemDetails con IProblemDetailsService: status, title (del catálogo), detail (el mensaje de la
/// excepción SOLO si no es 500), instance (ruta) y la extensión "codigo". Siempre devuelve true.
/// Registra en log con nivel Error las de 500 (desde .NET 10 el middleware ya no registra en log las excepciones
/// que un IExceptionHandler maneja) y con nivel Information las demás.
/// </summary>
public sealed class ManejadorDeErrores(IProblemDetailsService problemDetailsService, ILogger<ManejadorDeErrores> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken);
}

// ───────────── src/CoreBancario.Api/Errores/RespuestasDeError.cs ─────────────
namespace CoreBancario.Api.Errores;
public static class RespuestasDeError
{
    /// <summary>TypedResults.ValidationProblem(errores, title: "Datos inválidos", extensions: { ["codigo"] = "datos-invalidos" }).</summary>
    public static ValidationProblem DatosInvalidos(IReadOnlyDictionary<string, string[]> errores);
}

// ───────────── Requests y endpoints ─────────────
namespace CoreBancario.Api.Clientes;
public sealed record RegistrarClienteRequest(string? TipoDocumento, string? NumeroDocumento, string? Nombres,
    string? Apellidos, string? Correo, string? Telefono);
public static class EndpointsDeClientes
{
    /// <summary>Registra las tres rutas de /clientes (sección 3) con WithName, WithSummary y metadatos de respuesta (RNF-05).</summary>
    public static IEndpointRouteBuilder MapEndpointsDeClientes(this IEndpointRouteBuilder app);
}

namespace CoreBancario.Api.Cuentas;
public sealed record AbrirCuentaRequest(string? Moneda);
public static class EndpointsDeCuentas
{
    /// <summary>Registra POST /clientes/{clienteId}/cuentas y las rutas de /cuentas (sección 3).</summary>
    public static IEndpointRouteBuilder MapEndpointsDeCuentas(this IEndpointRouteBuilder app);
}

// ───────────── src/CoreBancario.Api/Program.cs (al final) ─────────────
public partial class Program;   // para WebApplicationFactory<Program>
```

**Nombres de operación (`WithName`, son el `operationId` del OpenAPI):** `RegistrarCliente`, `ObtenerCliente`, `BuscarClientePorDocumento`, `AbrirCuenta`, `ObtenerCuenta`, `BloquearCuenta`, `DesbloquearCuenta`, `CerrarCuenta`.

**Forma de un endpoint (con la opción recomendada de D-06; ilustrativo, no es contrato):**

```csharp
app.MapPost("/clientes", async Task<Results<Created<ClienteDto>, ValidationProblem>> (
        RegistrarClienteRequest request, RegistrarClienteHandler handler, CancellationToken ct) =>
    {
        var resultado = await handler.EjecutarAsync(new RegistrarClienteComando(request.TipoDocumento, …), ct);
        return resultado.EsExito
            ? TypedResults.Created($"/clientes/{resultado.Valor.Id}", resultado.Valor)
            : RespuestasDeError.DatosInvalidos(resultado.Errores);
    })
    .WithName("RegistrarCliente")
    .ProducesProblem(StatusCodes.Status409Conflict);
```

Notas para el test-writer sobre las firmas:

- Los value objects se prueban con `TryCrear` (devuelve `false`, sin excepción) y con `Crear` (lanza `ArgumentException`). `Crear` es un **error de programación** (ADR-0008): quien llama debió validar antes. Por eso no hereda de `ReglaDeNegocioException`.
- Vectores Luhn verificados a mano: `123456789` → `1234567897`; `100000000` → `1000000008`; `987654321` → `9876543217`. Inválidos: `1234567890` (verificador incorrecto), `2134567897` (transposición de los dos primeros dígitos), `0234567899` (Luhn correcto pero empieza por 0), `123456789` (9 dígitos), `12345678a7` (no dígito).
- Para comprobar el verificador de los números que devuelve la API (CA-06, CA-09), conviene un **oráculo independiente** en la prueba (un Luhn escrito en la prueba), no `NumeroDeCuenta.Crear`, para no probar el código consigo mismo.
- `Cliente` y `Cuenta` no tienen setters públicos: el estado solo se prepara con la API pública (`Registrar`, `Abrir`, `Acreditar`, `Bloquear`, `Cerrar`).

## 4. Modelo de datos y persistencia

### Modelo de dominio

| Tipo | Clase | Identidad | Mutabilidad |
|---|---|---|---|
| `Cliente` | Entidad (raíz de agregado) | `Id` (UUID v7 de `FechaRegistro`) | Inmutable en fase 1 |
| `Documento` | Value object (`sealed record`) | (`Tipo`, `Numero` normalizado) | Inmutable |
| `NombreDePersona`, `Correo`, `Telefono` | Value objects (`sealed record`) | `Valor` | Inmutables |
| `Cuenta` | Entidad (raíz de agregado) | `Id` (UUID v7 de `FechaApertura`) | `Estado`, `Saldo` y `FechaCierre` cambian solo por sus métodos |
| `NumeroDeCuenta` | Value object (`sealed record`) | `Valor` | Inmutable |

`Cuenta` referencia al cliente por `ClienteId`, no por objeto (spec de producto, "Modelo de dominio").

**Dónde vive cada regla (sin duplicarla):** el formato y la normalización viven **solo** en los value objects del dominio. Application **no** repite reglas: llama a `TryCrear` de cada uno y traduce un `false` en un mensaje para su campo. Api no valida nada; solo traduce. El detalle y las alternativas descartadas (incluida `AddValidation()` de .NET 10) están en ADR-0010.

### Tablas (PostgreSQL, snake_case con `EFCore.NamingConventions`)

**`clientes`**

| Columna | Tipo | Nulo | Origen |
|---|---|---|---|
| `id` | `uuid` PK | No | `Cliente.Id` |
| `tipo_documento` | `varchar(2)` | No | `Documento.Tipo`, como texto (`HasConversion<string>()`) |
| `numero_documento` | `varchar(15)` | No | `Documento.Numero` (ya normalizado) |
| `nombres` | `varchar(100)` | No | conversor `NombreDePersona` ↔ `string` |
| `apellidos` | `varchar(100)` | No | ídem |
| `correo` | `varchar(254)` | No | conversor `Correo` ↔ `string` |
| `telefono` | `varchar(16)` | No | conversor `Telefono` ↔ `string` |
| `fecha_registro` | `timestamptz` | No | `Cliente.FechaRegistro` |

- Índice único **`ux_clientes_documento`** sobre (`tipo_documento`, `numero_documento`): RN-17, CL-01, CL-02 y CL-03.
- `Documento` se mapea como **owned type** (`OwnsOne`, en la misma tabla, `Navigation(...).IsRequired()`) y **no** como complex type: EF Core 10 no admite índices sobre propiedades de complex types (llega en EF Core 11), y el índice único es justo lo que protege RN-17. El índice se declara en el builder del owned (`HasIndex(d => new { d.Tipo, d.Numero }).IsUnique().HasDatabaseName("ux_clientes_documento")`).
- Los conversores de lectura usan `Crear` de cada value object (por ejemplo `v => Correo.Crear(v)`). Los datos guardados ya pasaron esas reglas.

**`cuentas`**

| Columna | Tipo | Nulo | Origen |
|---|---|---|---|
| `id` | `uuid` PK | No | `Cuenta.Id` |
| `numero` | `varchar(10)` | No | conversor `NumeroDeCuenta` ↔ `string` |
| `cliente_id` | `uuid` FK → `clientes(id)` `ON DELETE RESTRICT` | No | `Cuenta.ClienteId`, sin navegación (`HasOne<Cliente>().WithMany().HasForeignKey(c => c.ClienteId)`) |
| `moneda` | `varchar(3)` | No | conversor `Moneda` ↔ `Codigo` (`Moneda.DesdeCodigo`) |
| `estado` | `varchar(10)` | No | `EstadoCuenta` como texto |
| `saldo` | `numeric(19,2)` | No | `Saldo.Monto` (complex type `Dinero`) |
| `saldo_moneda` | `varchar(3)` | No | `Saldo.Moneda` (complex type `Dinero`) |
| `fecha_apertura` | `timestamptz` | No | `Cuenta.FechaApertura` |
| `fecha_cierre` | `timestamptz` | Sí | `Cuenta.FechaCierre` |
| `xmin` | `xid` (columna de sistema; **no** la crea la migración) | — | propiedad sombra `uint "Version"` con `IsRowVersion()`, `HasColumnName("xmin")`, `HasColumnType("xid")` |

- Índice único **`ux_cuentas_numero`** (RN-18, CL-11) e índice no único sobre `cliente_id` (lo crea EF por la FK; lo usa la ficha del cliente).
- CHECK, como defensa en profundidad (si el dominio tuviera un error, la base lo rechaza en vez de guardarlo):
  - `ck_cuentas_saldo_no_negativo`: `saldo >= 0` (RN-01).
  - `ck_cuentas_saldo_en_moneda_de_la_cuenta`: `saldo_moneda = moneda` (RN-02).
  - `ck_cuentas_fecha_cierre_solo_si_cerrada`: `(estado = 'Cerrada') = (fecha_cierre IS NOT NULL)` (sección 4 de la spec).

**Decisiones de mapeo y su porqué:**

1. **Moneda duplicada (`moneda` y `saldo_moneda`) con CHECK de igualdad.** `Dinero` es un complex type de dos propiedades. EF Core no deja mapear dos propiedades de la misma entidad a una sola columna, y guardar solo el monto exigiría que `Cuenta` reconstruyera su `Saldo` a mano (código de infraestructura dentro del dominio). Duplicar 3 caracteres por fila y que la base garantice que coinciden es lo más simple y deja cada fila autodescriptiva.
2. **`numeric(19,2)` + forma canónica en el constructor de `Dinero`.** Con escala fija, PostgreSQL devuelve `50000.00` para una cuenta COP. EF Core materializa los complex types **por su constructor** (`Dinero` no tiene otro). Si ese constructor privado aplica `AFormaCanonica`, el monto leído vuelve con `Scale == Precision` y `DineroDto` muestra `"50000"`, no `"50000.00"`. La escala 2 cubre las dos monedas y `19` acota el máximo muy por debajo del límite de `decimal` (cierra el riesgo "forma canónica cerca del máximo" de la 001). Una prueba de integración comprueba el recorrido de ida y vuelta (sección 7). Si fallara (porque EF no usara el constructor), la alternativa es `numeric` sin escala, que conserva la escala escrita.
3. **snake_case con `EFCore.NamingConventions`.** Las consultas Dapper del S4 escriben SQL a mano: `cliente_id` se escribe sin comillas y `"ClienteId"` no. Los nombres de índices y CHECK se fijan a mano con `HasDatabaseName` y nombres explícitos, porque Infrastructure los usa para traducir errores (`ConstraintName`).
4. **`xmin` como propiedad sombra.** La versión es un detalle de persistencia, así que el dominio no la ve. Detalle y motivo en ADR-0012.
5. **Constructores para EF.** `Cuenta` usa su constructor privado (parámetros escalares con el mismo nombre que las propiedades) y EF asigna después `Estado`, `Saldo` y `FechaCierre`. `Cliente` necesita un constructor privado sin parámetros, porque una navegación owned no se puede pasar por constructor. Es el único `#pragma warning disable CS8618` del dominio, acotado y comentado.

### Migraciones

- Una migración, `Inicial`, en `src/CoreBancario.Infrastructure/Persistencia/Migraciones/`, generada con `dotnet ef migrations add Inicial --project src/CoreBancario.Infrastructure --output-dir Persistencia/Migraciones` (herramienta local `dotnet-ef` en `.config/dotnet-tools.json`, con la misma versión que EF Core).
- El SQL de la migración **no** crea una columna `xmin` ni `version` (el C# generado sí menciona `xmin`; Npgsql la omite al generar el SQL). Se revisa con `dotnet ef migrations script` (sección 10, riesgo 2).
- Después de generarla: `dotnet format`, porque el CI ejecuta `dotnet format --verify-no-changes`.
- **Cómo se aplican (RNF-02):** las pruebas llaman a `Database.MigrateAsync()` sobre un contenedor vacío. La Api las aplica al arrancar **solo en `Development`**. En producción se decidirá con el despliegue (D-11), porque migrar al arrancar con varias instancias es mala práctica. Desde EF Core 9, `Migrate` falla si el modelo tiene cambios sin migración: es una guarda gratuita.

### Paquetes (verificados el 2026-10-05; todos los `Microsoft.*` con el mismo parche)

| Proyecto | Paquete | Versión |
|---|---|---|
| Application | `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.12 |
| Infrastructure | `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 |
| Infrastructure | `EFCore.NamingConventions` | 10.0.1 |
| Infrastructure | `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) | 10.0.12 |
| Infrastructure | `Microsoft.Extensions.Configuration.Abstractions` (si no llega de forma transitiva) | 10.0.12 |
| Herramienta local | `dotnet-ef` | 10.0.12 |
| Infrastructure.Tests, Api.Tests | `Testcontainers.PostgreSql` | 4.15.0 |
| Api.Tests | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 |

Domain sigue **sin paquetes**. Ningún value object nuevo usa `Regex`: se valida recorriendo caracteres (`char.IsAsciiDigit`, `char.IsAsciiLetterOrDigit`, `char.IsLetter`, `char.IsWhiteSpace`), así que la prueba `RNF01_EnsambladoDelDominio_SoloReferenciaElFramework` de la 001 sigue en verde sin ampliar la lista de ensamblados. Si el coder necesitara otro ensamblado del framework, lo añade a la lista de forma explícita y lo justifica en el commit.

### Configuración

- Cadena de conexión: `ConnectionStrings:CoreBancario`. No va en el repositorio (gitleaks). En desarrollo se define con `dotnet user-secrets` (la Api lleva `UserSecretsId`) o con la variable `ConnectionStrings__CoreBancario`. Se documenta en el README y en un comentario de `.env.example`.
- Las pruebas la inyectan con `WebApplicationFactory` (`UseSetting`) o crean el `DbContext` directamente con `UsarPostgres`.

## 5. Casos límite y su manejo

### Validación de registro (CL-04…CL-07, CA-04)

`RegistrarClienteHandler` evalúa **los seis campos siempre** y acumula un error por campo inválido. No se detiene en el primero.

| Clave | Regla | Mensaje sugerido (no es contrato) |
|---|---|---|
| `tipoDocumento` | null/vacío → obligatorio. Debe ser exactamente `CC`, `CE` o `PA`. Se compara con un `switch` explícito, **no** con `Enum.TryParse`, que acepta `"1"` o `"CC,PA"` | "El tipo de documento debe ser CC, CE o PA." |
| `numeroDocumento` | null/vacío → obligatorio. Si el tipo es válido: `Documento.TryCrear`. Si el tipo es inválido, el número **no** se evalúa (su formato depende del tipo) y solo se informa el error del tipo | "El número no cumple el formato de {tipo}: …" |
| `nombres`, `apellidos` | `NombreDePersona.TryCrear` | "Debe tener de 1 a 100 letras, espacios, apóstrofos o guiones." |
| `correo` | `Correo.TryCrear` | "Debe tener la forma usuario@dominio (máximo 254 caracteres)." |
| `telefono` | `Telefono.TryCrear` | "Debe estar en formato internacional: + y de 8 a 15 dígitos con el código de país." |

### Casos de la spec

| ID | Caso | Manejo | Test esperado |
|---|---|---|---|
| CL-01 | Mismo tipo y número normalizado | `Documento` normaliza; el INSERT choca con `ux_clientes_documento` (`23505`) → `DocumentoDuplicadoException` → 409 `documento-duplicado` | Domain: `CL01_NumeroEscritoDistinto_SonElMismoDocumento`. Api: `CA02_RegistrarMismoDocumentoConPuntos_RespondeDuplicadoYQuedaUnSoloCliente` |
| CL-02 | Mismo número con otro tipo | El índice incluye `tipo_documento` | Domain: `CL02_MismoNumeroConOtroTipo_SonDocumentosDistintos`. Api: `CA03_RegistrarMismoNumeroComoPasaporte_SeAcepta`. Infra: `CL02_MismoNumeroConOtroTipo_SeGuardanLosDos` |
| CL-03 | Registros simultáneos | **Sin consulta previa**: el índice decide y quien pierde recibe la misma excepción que en CL-01. Como el camino concurrente es el mismo que el normal, la prueba es determinista | Infra: `CA05_DosContextosRegistranElMismoDocumento_ElSegundoLanzaDocumentoDuplicado`. Api: `CA05_DosRegistrosSimultaneos_UnoCreaYOtroRespondeDuplicado` |
| CL-04 | Varios campos inválidos | Acumulación (tabla anterior); `Resultado.Invalido` → 400 con todas las claves; no se llama al repositorio | App: `CA04_DocumentoCorreoYTelefonoInvalidos_DevuelveLosTresErroresSinGuardar`, `CL04_TodosLosCamposAusentes_DevuelveUnErrorPorCampo`. Api: `CA04_RegistroConTresCamposInvalidos_Responde400ConLosTresErrores` |
| CL-05 | Formato de documento por tipo | `Documento.TryCrear` | Domain: `CL05_CedulaConLetras_NoEsValida`, `CL05_CedulaQueEmpiezaPorCero_NoEsValida`, `CL05_CedulaFueraDeRango_NoEsValida` ("12", "12345678901"), `CL05_PasaporteConSimbolos_NoEsValido` ("AB/12345"), `CL05_PasaporteFueraDeRango_NoEsValido` ("A123", 16 caracteres), `CL05_CedulaDeExtranjeriaConLetras_NoEsValida`. App: `CL05_TipoDocumentoDesconocido_DevuelveErrorDeTipo` ("XX", "1", "cc") |
| CL-06 | Nombres o apellidos vacíos o con solo espacios | `NombreDePersona.TryCrear` | Domain: `CL06_VacioOSoloEspacios_NoEsValido`, `CL06_ConTildesEnieApostrofoYGuion_EsValido` ("María José", "Núñez", "O'Neil", "Pérez-Gómez"), `CL06_ConDigitosOSimbolos_NoEsValido`, `CL06_MasDeCienCaracteres_NoEsValido`, `CL06_QuitaEspaciosAlInicioYAlFinal`, `CL06_SoloApostrofosYGuiones_NoEsValido` |
| CL-07 | Teléfono sin código de país | `Telefono.TryCrear` exige `+` | Domain: `CL07_SinCodigoDePais_NoEsValido` ("3001234567"), `CL07_FueraDeRango_NoEsValido` ("+1234567", "+" y 16 dígitos), `CA01_TelefonoConEspaciosGuionesYParentesis_SeNormaliza` |
| CL-08 | Reintento de un registro que sí se guardó | Igual que CL-01; la búsqueda por documento lo encuentra | Api: `CL08_ReintentarRegistroGuardado_RespondeDuplicadoYSeEncuentraPorDocumento` |
| CL-09 | Abrir cuenta a un cliente inexistente | Moneda válida → `ObtenerAsync` null → `RecursoNoEncontradoException` → 404; no se llama a `Agregar`. La FK es la segunda defensa | App: `CL09_ClienteInexistente_LanzaNoEncontradoSinGuardar`. Api: `CA08_AbrirCuentaClienteInexistente_Responde404YNoCreaCuenta` |
| CL-10 | Moneda distinta de COP/USD | `Moneda.TryDesdeCodigo` → `Invalido` con la clave `moneda` → 400. La moneda se valida **antes** que el cliente: con moneda inválida y cliente inexistente responde 400 | Domain: `CL10_TryDesdeCodigoDesconocido_DevuelveFalse` ("EUR", "cop", "", null), `CL10_DesdeCodigoValido_DevuelveLaMoneda`. App: `CL10_MonedaDesconocida_DevuelveErrorDeMonedaSinGuardar`. Api: `CL10_AbrirCuentaEnEur_Responde400` |
| CL-11 | Número generado ya existente | Bucle de hasta 5 intentos `Generar` + `ExisteNumeroAsync`. Si dos aperturas simultáneas sacaran el mismo número libre (probabilidad ~1 en 9×10⁸ por par), el índice lo rechaza → 409 `conflicto-de-concurrencia` y el operador reintenta. Nunca hay dos cuentas con el mismo número | App: `CL11_NumeroRepetido_GeneraOtroSinError`, `CL11_CincoNumerosRepetidos_LanzaInvalidOperation`. Api: `CL11_GeneradorDevuelveUnNumeroExistente_AbreConOtroNumero` (generador falso con la secuencia A, A, B). Infra: `RN18_NumeroRepetido_LoRechazaElIndiceUnico` |
| CL-12 | Reintento de una apertura que sí se guardó | Límite aceptado: se abre una segunda cuenta (sin idempotencia hasta el S4) | Api: `CL12_ReintentarApertura_AbreUnaSegundaCuenta` (documenta el límite) |
| CL-13 | Transición no permitida | `Cuenta` lanza `TransicionNoPermitidaException` → 422 `transicion-no-permitida`; no se guarda nada | Api: `CA11_CerrarOBloquearCuentaBloqueada_Responde422YSigueBloqueada`, `CA12_CambiarEstadoDeCuentaCerrada_Responde422` |
| CL-14 | Cerrar con saldo | `SaldoDistintoDeCeroException` → 422 `saldo-distinto-de-cero`; `FechaCierre` sigue null | Domain: `CA13_CerrarConSaldo_NoFijaFechaDeCierre`. Api: `CA13_CerrarCuentaConSaldo_Responde422YSigueActivaSinFechaDeCierre` |
| CL-15 | Cambios de estado simultáneos | `xmin` por fila: el `UPDATE` lleva `WHERE xmin = @leido`; 0 filas → `DbUpdateConcurrencyException` → `ConflictoDeConcurrenciaException` → 409. **Sin reintento automático** (ADR-0012): al reintentar, el operador vuelve a enviar y RN-14 se evalúa sobre el estado nuevo | Infra: `CA14_BloquearYCerrarIntercalados_ElSegundoRecibeConflicto`, `CL15_ReintentoTrasConflicto_EvaluaLaReglaSobreElEstadoNuevo`, `CL15_CerrarConVersionViejaTrasCambioDeSaldo_LanzaConflicto` (write skew del S4). Api: `RNF03_ConflictoDeConcurrencia_SeClasificaComo409` |
| CL-16 | Cambiar el estado de una cuenta inexistente | `RecursoNoEncontradoException` → 404 | Api: `CL16_BloquearCuentaInexistente_Responde404` |
| CL-17 | Búsqueda con el número sin normalizar | `BuscarClientePorDocumentoHandler` usa `Documento.TryCrear` (misma normalización que el registro) | Api: `CA15_BuscarConPuntos_DevuelveLaFichaConSusCuentas` |
| CL-18 | Documento, cliente o cuenta inexistentes | `RecursoNoEncontradoException` → 404 `no-encontrado`. Un número con formato inválido también responde 404; un tipo inválido o un número ausente responden 400 | Api: `CA16_BuscarDocumentoNoRegistrado_Responde404`, `CL18_ObtenerClienteInexistente_Responde404`, `CA17_ObtenerCuentaInexistente_Responde404`, `CL18_BuscarConTipoInvalido_Responde400` |
| CL-19 | Cliente sin cuentas | `ListarPorClienteAsync` devuelve una lista vacía → `"cuentas": []` | Api: `CA16_ClienteSinCuentas_DevuelveListaVacia` |

### Catálogo de errores (ADR-0011)

| Origen | HTTP | `codigo` | `title` |
|---|---|---|---|
| `Resultado.Invalido` (vía `RespuestasDeError.DatosInvalidos`, con `errors`) | 400 | `datos-invalidos` | Datos inválidos |
| `BadHttpRequestException` (JSON mal formado; se fuerza con `RouteHandlerOptions.ThrowOnBadRequest = true`) | 400 | `datos-invalidos` | Datos inválidos |
| `RecursoNoEncontradoException` | 404 | `no-encontrado` | Recurso no encontrado |
| `DocumentoDuplicadoException` | 409 | `documento-duplicado` | Conflicto |
| `ConflictoDeConcurrenciaException` | 409 | `conflicto-de-concurrencia` | Conflicto |
| `TransicionNoPermitidaException` | 422 | `transicion-no-permitida` | Regla de negocio rota |
| `SaldoDistintoDeCeroException` | 422 | `saldo-distinto-de-cero` | Regla de negocio rota |
| `OperacionNoPermitidaException`, `SaldoInsuficienteException`, `MontoNoPositivoException`, `MonedasDistintasException`, `MontoNegativoException`, `PrecisionExcedidaException` | 422 | uno por tipo (`CodigosDeError`) | Regla de negocio rota |
| Otra `ReglaDeNegocioException` (respaldo) | 422 | `regla-de-negocio` | Regla de negocio rota |
| Cualquier otra excepción | 500 | `error-inesperado` | Error inesperado (sin `detail`) |

Un id que no es un GUID no coincide con ninguna ruta (`{id:guid}`) y responde 404 sin cuerpo. Es un límite aceptado: el frontend nunca construye esas rutas a mano.

## 6. Estrategia para los requisitos no funcionales

| RNF | Cómo se cumple | Cómo se verifica |
|---|---|---|
| RNF-01 | Cada CA tiene al menos una prueba en Api.Tests o Infrastructure.Tests contra PostgreSQL en Testcontainers (sección 7). El test-writer las escribe antes que el coder | Tabla de la sección 7. Commit `test(002): pruebas en rojo`, que el revisor ejecuta |
| RNF-02 | Esquema solo por la migración `Inicial`. Las pruebas usan `MigrateAsync()` sobre un contenedor vacío, nunca `EnsureCreated`. La Api migra al arrancar en `Development` | Infra: `RNF02_BaseVacia_QuedaConTodasLasMigracionesAplicadas` (`GetPendingMigrationsAsync` vacío y `GetAppliedMigrationsAsync` contiene `Inicial`), `RNF02_Modelo_NoTieneCambiosSinMigracion` (`Database.HasPendingModelChanges()` es false) |
| RNF-03 | Cuatro categorías + 500 con `codigo` estable (sección 5, ADR-0011). Los datos inválidos se acumulan (ADR-0010) | Api: `RNF03_CatalogoDeErrores_ClasificaCadaExcepcion` (teoría con cada tipo → estado y código), `RNF03_CadaReglaDeNegocioDelDominio_TieneCodigoPropio` (por reflexión: ninguna subclase concreta de `ReglaDeNegocioException` cae en `regla-de-negocio`), `RNF03_JsonMalFormado_Responde400DatosInvalidos`, y cada prueba de CA comprueba estado y `codigo` |
| RNF-04 | `ManejadorDeErrores` solo pone `detail` en errores conocidos (mensajes propios, en español) y en el 500 no incluye mensaje, traza ni tipo. Las excepciones de Npgsql siempre se traducen o caen en el 500 genérico | Api: `RNF04_ErrorInesperado_Responde500SinDetallesInternos`: sustituye `TimeProvider` por uno que lanza `InvalidOperationException("SELECT * FROM clientes …")` y comprueba 500, `codigo = error-inesperado` y que el cuerpo no contiene `SELECT`, `Exception`, `   at ` ni el mensaje |
| RNF-05 | `WithName`, `WithSummary`, `TypedResults` con `Results<…>` y `.ProducesProblem(…)`/`.ProducesValidationProblem()` por cada código de la tabla de endpoints | Api: `RNF05_ContratoOpenApi_DescribeCadaOperacionYSusErrores`: lee `/openapi/v1.json` y comprueba, por `operationId`, los códigos de respuesta exactos de la sección 3 |
| RNF-06 | Cada comando hace un solo `GuardarCambiosAsync`, que es una transacción. La validación ocurre antes de `Agregar`. Las reglas del dominio se comprueban antes de modificar nada (001, CL-13) | CA-04 (App: no se llama al repositorio), CA-05 y CA-08 (no queda nada), CA-11 y CA-13 (estado sin cambios tras un 422), CA-14 (el estado guardado es el de la operación aplicada) |

## 7. Estrategia de pruebas

| Nivel | Qué se prueba | Proyecto de tests |
|---|---|---|
| Unitarias (Domain) | Value objects (normalización y formato), `NumeroDeCuenta` (Luhn), `Cliente.Registrar`, los cambios de `Cuenta` (fechas, `Abrir`, `Cerrar`), `Moneda.DesdeCodigo`/`TryDesdeCodigo`. Se adaptan las pruebas de la 001 | `tests/CoreBancario.Domain.Tests` |
| Unitarias (Application) | Acumulación de errores (CA-04, CL-04, CL-05, CL-10), reintento del número (CL-11) y "no se guarda nada" (RNF-06), con fakes en memoria | `tests/CoreBancario.Application.Tests` |
| Integración (Infrastructure) | Migraciones (RNF-02), índice único y traducción de `23505` (CA-05, CL-02, RN-18), `xmin` y write skew (CA-14, CL-15), forma canónica al recargar, CHECK de la base | `tests/CoreBancario.Infrastructure.Tests` |
| API (integración de punta a punta) | Todos los CA por HTTP contra PostgreSQL real, catálogo de errores (RNF-03), RNF-04 y RNF-05 | `tests/CoreBancario.Api.Tests` |

### Dónde va cada CA (RNF-01: al menos una prueba de integración por CA)

| CA | Prueba(s) de integración | Proyecto |
|---|---|---|
| CA-01 | `CA01_RegistrarClienteValido_Responde201ConIdFechaYDatosNormalizados` (y un GET posterior lo devuelve igual) | Api |
| CA-02 | `CA02_RegistrarMismoDocumentoConPuntos_RespondeDuplicadoYQuedaUnSoloCliente` | Api |
| CA-03 | `CA03_RegistrarMismoNumeroComoPasaporte_SeAcepta` | Api |
| CA-04 | `CA04_RegistroConTresCamposInvalidos_Responde400ConLosTresErrores` (exactamente las claves `numeroDocumento`, `correo`, `telefono`) | Api |
| CA-05 | `CA05_DosRegistrosSimultaneos_UnoCreaYOtroRespondeDuplicado` (`Task.WhenAll`; estados {201, 409}; un solo cliente) y `CA05_DosContextosRegistranElMismoDocumento_ElSegundoLanzaDocumentoDuplicado` | Api, Infra |
| CA-06 | `CA06_AbrirCuentaCop_QuedaActivaConSaldoCeroFechaDeAperturaYNumeroValido`; `CA06_SaldoCopRecargado_ConservaFormaCanonica` | Api, Infra |
| CA-07 | `CA07_AbrirCuentaUsd_QuedaEnUsdConSaldoCero` (`"0.00"`); `CA07_SaldoUsdRecargado_ConservaDosDecimales` | Api, Infra |
| CA-08 | `CA08_AbrirCuentaClienteInexistente_Responde404YNoCreaCuenta` | Api |
| CA-09 | `CA09_AbrirCincuentaCuentas_NumerosDistintosValidosYNoConsecutivos` (en el orden de creación, ningún cuerpo es el anterior + 1) | Api |
| CA-10 | `CA10_BloquearYDesbloquear_CambiaElEstadoYSePersiste` | Api |
| CA-11 | `CA11_CerrarOBloquearCuentaBloqueada_Responde422YSigueBloqueada` | Api |
| CA-12 | `CA12_CerrarCuentaActivaSinSaldo_QuedaCerradaConFechaDeCierre`; `CA12_CambiarEstadoDeCuentaCerrada_Responde422` | Api |
| CA-13 | `CA13_CerrarCuentaConSaldo_Responde422YSigueActivaSinFechaDeCierre` | Api |
| CA-14 | `CA14_BloquearYCerrarIntercalados_ElSegundoRecibeConflicto` (determinista: dos `DbContext` leen la misma cuenta; el primero guarda `Bloquear`; el segundo intenta guardar `Cerrar` → `ConflictoDeConcurrenciaException`; al recargar, Bloqueada sin fecha de cierre) | Infra (+ Api: `RNF03_ConflictoDeConcurrencia_SeClasificaComo409`) |
| CA-15 | `CA15_BuscarConPuntos_DevuelveLaFichaConSusCuentas` | Api |
| CA-16 | `CA16_BuscarDocumentoNoRegistrado_Responde404`; `CA16_ClienteSinCuentas_DevuelveListaVacia` | Api |
| CA-17 | `CA17_ObtenerCuentaExistente_DevuelveNumeroMonedaEstadoSaldoYFechas`; `CA17_ObtenerCuentaInexistente_Responde404` | Api |

**Por qué CA-14 se prueba intercalando y no con dos peticiones HTTP en paralelo.** Con dos peticiones reales, si no llegan a solaparse, la segunda ve el estado nuevo y responde 422 en vez de 409, así que la prueba fallaría a veces (prueba *flaky*). Intercalar dos `DbContext` reproduce exactamente el solapamiento que importa (ambos leen la misma versión) y siempre da el mismo resultado. La traducción a HTTP 409 se prueba aparte, en el catálogo.

### Infraestructura de pruebas (la escribe el test-writer)

- **Un contenedor por proyecto de tests**, compartido por todas sus pruebas: una clase `PostgresFixture : IAsyncLifetime` con `new PostgreSqlBuilder("postgres:17").Build()` (la misma imagen que `docker-compose.yml`). En `InitializeAsync` arranca el contenedor y aplica `MigrateAsync()`. Se registra con `[assembly: AssemblyFixture(typeof(PostgresFixture))]` de xUnit v3 y las clases la reciben por constructor. Infrastructure.Tests expone `CoreBancarioDbContext CrearContexto()` (con `UsarPostgres`). Api.Tests añade `ApiFixture`, que además crea una `WebApplicationFactory<Program>` con `UseSetting("ConnectionStrings:CoreBancario", …)`. Duplicar esta clase pequeña en los dos proyectos es aceptable (DRY: se abstrae a la tercera vez).
- **Aislamiento por datos únicos, sin limpiar la base.** Cada prueba crea sus propios clientes con un número de documento aleatorio (CC de 7 a 10 dígitos que no empieza por 0) y trabaja solo con sus ids. Así las pruebas pueden correr en paralelo (xUnit v3 paraleliza clases) y no hacen falta Respawn ni una base por prueba. Consecuencia: **los datos literales de la spec (CC 1234567) se sustituyen por números únicos con las mismas transformaciones** (`"1.234.567"` pasa a ser el número generado con puntos). Es una desviación consciente que el revisor debe aceptar.
- **CA-13 sin RF-04:** la prueba prepara la cuenta con el dominio y la guarda directamente: `Cuenta.Abrir(...)`, `cuenta.Acreditar(Dinero.Crear(1000m, Moneda.COP), instante)` (el `Movimiento` se descarta porque en el S2 no hay tabla de movimientos), `db.Cuentas.Add(cuenta)`, `SaveChangesAsync()`. Usa un scope de `factory.Services`. El cliente se crea antes por HTTP (FK).
- **CL-11 y RNF-04** sustituyen servicios con `factory.WithWebHostBuilder(b => b.ConfigureTestServices(…))`: un `IGeneradorDeNumeroDeCuenta` con una secuencia fija, o un `TimeProvider` que lanza.
- **Fechas:** `timestamptz` guarda microsegundos y .NET maneja ticks de 100 ns. Las pruebas que comparan fechas leídas de la base usan una tolerancia o un `TimeProvider` falso con instantes redondos.
- **Fakes de Application.Tests** (en el propio proyecto, sin librerías): `ClienteRepositorioEnMemoria`, `CuentaRepositorioEnMemoria`, `UnidadDeTrabajoEspia` (cuenta las llamadas), `GeneradorDeSecuencia` y `RelojFijo : TimeProvider`.
- **Paquetes de tests:** `Testcontainers.PostgreSql` 4.15.0 (Infrastructure.Tests y Api.Tests) y `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 (Api.Tests). Docker es obligatorio en local (Docker Desktop en Windows); el runner de Ubuntu del CI ya lo trae.
- **Al añadir la primera prueba**, el test-writer quita `--ignore-exit-code 8` (y su comentario) de `CoreBancario.Application.Tests.csproj`, `CoreBancario.Infrastructure.Tests.csproj` y `CoreBancario.Api.Tests.csproj` (ADR-0006).

### Pruebas de la 001 que cambian (las adapta el test-writer en el commit en rojo)

- Todas las llamadas `Cuenta.Abrir("001-000X", clienteId, moneda)` pasan a `Cuenta.Abrir(NumeroDeCuenta.Crear("1234567897"), clienteId, moneda, Instante)`, en `CuentaEstadosTests.cs`, `CuentaOperacionesTests.cs` y `MovimientoTests.cs`. Conviene un ayudante privado por clase.
- Todas las llamadas `cuenta.Cerrar()` pasan a `cuenta.Cerrar(Instante)`.
- `CA10_AbrirCuenta_ConservaNumeroYCliente`: compara `cuenta.Numero` con el `NumeroDeCuenta` usado.
- `ADR0008_AbrirSinNumero_LanzaArgumentException` (teoría con `null`, `""` y `" "`) pasa a ser `ADR0008_AbrirSinNumero_LanzaArgumentNullException` (solo `null`): un `NumeroDeCuenta` vacío ya no puede existir.
- Nuevas en Domain: `CA06_Abrir_FijaFechaDeAperturaYSinFechaDeCierre`, `CA06_Abrir_IdEsVersion7DeLaFechaDeApertura`, `CA12_Cerrar_FijaLaFechaDeCierre`, `CA11_CerrarBloqueada_NoFijaFechaDeCierre`, `CA13_CerrarConSaldo_NoFijaFechaDeCierre`.

Convenciones (las de la 001): nombre `IDRegla_Escenario_Resultado`, Arrange/Act/Assert y un comportamiento por prueba. Las pruebas de API comprueban **estado HTTP y `codigo`**, no el texto.

## 8. Decisiones

ADRs nuevas (todas en `Propuesta`):

- **ADR-0009: Despacho de comandos y consultas (D-06).** Handlers llamados directamente o un mediador. **Lo decide el autor**; se recomienda llamarlos directamente.
- **ADR-0010: Validación de entrada: reglas en value objects y acumulación en Application.** Complementa a ADR-0008. Se recomienda `Resultado<T>`; el autor confirma el canal (pregunta 2).
- **ADR-0011: Contrato de errores HTTP: ProblemDetails con un código estable.** Concreta la consecuencia de ADR-0008: 400/404/409/422/500 y `codigo`.
- **ADR-0012: Concurrencia optimista por fila con `xmin` en `cuentas`.** 409 sin reintento automático en los cambios de estado. Relación con D-02 (S4).
- **ADR-0013: Número de cuenta aleatorio con dígito verificador Luhn.** RN-18.

Decisiones del plan sin ADR (locales a esta feature y fáciles de cambiar):

1. **Value objects con `TryCrear` y `Crear`.** `TryCrear` es el idioma estándar de .NET para interpretar una entrada (`int.TryParse`): no usa excepciones ni un tipo `Resultado` en el dominio, así que respeta ADR-0008. `Crear` lanza `ArgumentException` porque llegar con datos inválidos es un error de programación.
2. **Un `NombreDePersona` para nombres y apellidos.** Las reglas son las mismas (DRY de conocimiento, no de código).
3. **Unicidad del documento solo con el índice, sin consulta previa.** Consultar antes no evita la carrera de CL-03 y crea dos caminos para el mismo error. Con solo el índice hay un camino (insertar y traducir `23505`) y CA-05 es determinista.
4. **Número de cuenta con consulta previa y hasta 5 intentos.** Reintentar después de un fallo del índice obligaría a desenganchar la entidad del `DbContext` a través del puerto. La consulta previa es más simple y el índice sigue siendo la garantía final (ADR-0013).
5. **Ids UUID v7 derivados del instante (`Guid.CreateVersion7(fecha)`) en `Cliente` y `Cuenta`.** Cierra el riesgo de la 001 ("`Abrir` lee el reloj") sin cambiar de tipo de id.
6. **`TimeProvider` inyectado en los handlers y la fecha pasada como parámetro al dominio**, como decidió la 001 (decisión 8.4).
7. **Lecturas con los mismos repositorios de EF Core.** Las consultas del S2 son por clave, así que Dapper no está justificado todavía (ADR-0004: "solo cuando una consulta concreta lo justifica"). La ficha son dos consultas simples.
8. **Un único `CuentaDto`** para el detalle y para la lista de la ficha: es un superconjunto de lo que pide CA-15 y evita un segundo DTO casi igual.
9. **Montos como texto en el JSON** (sección 3), según el RNF de dinero de la spec de producto.
10. **Un solo handler para los tres cambios de estado** (`AccionDeEstado`). Los tres comparten el flujo leer → método del dominio → guardar; tres clases casi idénticas no aportan nada.
11. **Requests de Api separados de los comandos de Application.** El OpenAPI es el contrato con `core-bancario-web` (ADR-0005). Renombrar un tipo interno no debe cambiar ese contrato, y `AbrirCuentaRequest` no lleva el `clienteId` (va en la ruta).
12. **Sin `AddValidation()` ni filtros de endpoint en el S2** (ADR-0010): ninguna regla queda en la capa HTTP. Los filtros se añadirán cuando un requisito transversal lo pida (por ejemplo, autorización en el S3).
13. **Migrar al arrancar solo en `Development`.**

### 8.1 Qué cambia según la respuesta de D-06 (ADR-0009)

El plan está escrito con la recomendación (**handlers llamados directamente**). Los handlers son clases concretas con un único método `EjecutarAsync(TComando, CancellationToken)`, sin dependencias de ninguna librería. Si el autor elige un mediador, el cambio es mecánico y no toca Domain, Infrastructure ni la lógica de los handlers:

| Pieza | Handlers directos (recomendado) | Con mediador (MediatR o `Mediator`) |
|---|---|---|
| Comandos y consultas | `record` simples | Implementan `IRequest<TRespuesta>` |
| Handlers | Clase concreta con `EjecutarAsync` | Implementan `IRequestHandler<TRequest, TRespuesta>`; el método se llama `Handle` |
| Registro (`AddAplicacion`) | `AddScoped<RegistrarClienteHandler>()`, etc. | `AddMediatR(…)` o `AddMediator()` y, en MediatR 13+, la clave de licencia |
| Endpoints | Reciben el handler concreto por parámetro | Reciben `ISender`/`IMediator` y llaman a `Send` |
| Paquetes | Ninguno | Uno en Application |
| Pruebas | Application.Tests llama a `EjecutarAsync` | Llama a `Handle`; Api, Infrastructure y Domain no cambian |
| Tareas | T06…T11, T18, T19 | Las mismas, con estas diferencias |

## 9. Trazabilidad

| ID de la spec | Sección del plan | Tareas |
|---|---|---|
| RF-01 | 3 (POST /clientes, `RegistrarClienteHandler`), 5 | T04, T05, T07, T14, T15, T18 |
| RF-02 | 3 (POST /clientes/{id}/cuentas, `AbrirCuentaHandler`), 4, 5 | T02, T03, T09, T15, T16, T19 |
| RF-03 | 3 (POST /cuentas/{id}/bloquear, desbloquear y cerrar), 5 | T03, T10, T14, T19 |
| RF-05 (parcial) | 3 (GET /cuentas/{id}) | T10, T19 |
| RF-12 | 3 (GET /clientes/{id}, GET /clientes/por-documento) | T08, T15, T18 |
| RN-01 | 4 (CHECK `ck_cuentas_saldo_no_negativo`); dominio de la 001 sin cambios | T12, T13 |
| RN-02 | 4 (CHECK `ck_cuentas_saldo_en_moneda_de_la_cuenta`, conversor de `Moneda`) | T01, T12, T13 |
| RN-06 | 3 (`Cuenta.Cerrar(fecha)`), 5 (CL-14) | T03, T10 |
| RN-14 | 5 (CL-13); dominio de la 001 | T03, T10 |
| RN-17 | 3 (`Documento`), 4 (`ux_clientes_documento`), 5 (CL-01…CL-03) | T04, T12, T13, T14 |
| RN-18 | 3 (`NumeroDeCuenta`, `IGeneradorDeNumeroDeCuenta`), 4 (`ux_cuentas_numero`), 5 (CL-11); ADR-0013 | T02, T09, T12, T13, T16 |
| RNF-01 | 7 | Todas (flujo de `/sdd-build`) |
| RNF-02 | 4 (Migraciones), 6 | T13, T20 |
| RNF-03 | 3, 5 (catálogo), 6; ADR-0010, ADR-0011 | T06, T07, T17 |
| RNF-04 | 5 (catálogo), 6 | T17, T20 |
| RNF-05 | 3, 6 | T18, T19, T20 |
| RNF-06 | 1, 6 | T07, T09, T10, T14 |
| CL-01 | 5 | T04, T14 |
| CL-02 | 5 | T04, T12 |
| CL-03 | 5 | T14 |
| CL-04 | 5 (validación de registro) | T07 |
| CL-05 | 5 | T04, T07 |
| CL-06 | 5 | T04, T07 |
| CL-07 | 5 | T04, T07 |
| CL-08 | 5 | T14, T18 |
| CL-09 | 5 | T09 |
| CL-10 | 5 | T01, T09 |
| CL-11 | 5; ADR-0013 | T09, T14, T16 |
| CL-12 | 5 (límite aceptado) | T09 |
| CL-13 | 5 | T10, T17 |
| CL-14 | 5 | T03, T10, T17 |
| CL-15 | 5; ADR-0012 | T12, T14, T17 |
| CL-16 | 5 | T10 |
| CL-17 | 5 | T04, T08 |
| CL-18 | 5 | T08, T10 |
| CL-19 | 5 | T08, T15 |
| CA-01 | 5, 7 | T04, T05, T07, T18 |
| CA-02 | 5 (CL-01), 7 | T14, T18 |
| CA-03 | 5 (CL-02), 7 | T12, T18 |
| CA-04 | 5 (validación de registro), 7 | T07, T17, T18 |
| CA-05 | 5 (CL-03), 7 | T14, T18 |
| CA-06 | 3 (`CuentaDto`), 4 (forma canónica), 7 | T01, T02, T03, T09, T19 |
| CA-07 | 4 (forma canónica), 7 | T01, T09, T19 |
| CA-08 | 5 (CL-09), 7 | T09, T19 |
| CA-09 | 7; ADR-0013 | T02, T16, T19 |
| CA-10 | 3, 7 | T10, T19 |
| CA-11 | 5 (CL-13), 7 | T10, T17, T19 |
| CA-12 | 3 (`Cerrar(fecha)`), 7 | T03, T10, T19 |
| CA-13 | 5 (CL-14), 7 (preparación sin RF-04) | T03, T10, T19 |
| CA-14 | 5 (CL-15), 7; ADR-0012 | T12, T14, T17 |
| CA-15 | 5 (CL-17), 7 | T08, T18 |
| CA-16 | 5 (CL-18, CL-19), 7 | T08, T18 |
| CA-17 | 3, 7 | T10, T19 |
| D-06 | 8.1; ADR-0009 | T06, T11, T18, T19 (y T21 para anotar la resolución) |

## 10. Riesgos y preguntas abiertas

### Preguntas abiertas para el autor (resueltas el 2026-10-05)

El autor eligió las dos recomendaciones: **handlers llamados directamente** (ADR-0009) y **`Resultado<T>`** (ADR-0010). Sus motivos están en la sección "Decisiones del autor" de cada ADR. El plan no cambia.

1. **D-06: handlers llamados directamente o mediador** (ADR-0009). Recomendación: llamarlos directamente. El plan está escrito así y la sección 8.1 dice qué cambia con un mediador.
2. **Canal de los datos inválidos: `Resultado<T>` o una excepción con la lista de errores** (ADR-0010). Recomendación: `Resultado<T>`, porque es coherente con ADR-0008 y con lo que el autor escribió en ella. Lo que no cambia con ninguna de las dos: las reglas viven en los value objects y los errores se acumulan en Application. Si elige la excepción: `Resultado<T>` desaparece; los handlers devuelven el DTO; se añade `DatosInvalidosException(IReadOnlyDictionary<string,string[]> errores)` en `Application/Comun`; `ManejadorDeErrores` la traduce a `ValidationProblemDetails`; `RespuestasDeError` desaparece. Afecta a T06, T07, T08, T09, T17, T18 y T19.

### Riesgos

1. **`HasIndex` dentro de `OwnsOne`.** Se usa para el índice de `Documento`, porque EF Core 10 no admite índices sobre complex types. Si al generar la migración no aparece `ux_clientes_documento`, la alternativa es crearlo con `migrationBuilder.Sql(...)` en la migración. CA-02 y CA-05 lo detectarían.
2. **`xmin` en la migración.** Npgsql reconoce `xmin` como columna de sistema y no debe crearla. Hay que revisar el código generado: si aparece `xmin` o `version`, se corrige la configuración, porque la migración fallaría contra PostgreSQL. **Resultado del Build:** el C# de la migración sí menciona `xmin` (`type: "xid", rowVersion: true`), pero el SQL generado no la crea, porque Npgsql omite las columnas de sistema. Comprobado con `dotnet ef migrations script` y con las pruebas `RNF02_*`, que aplican la migración sobre una base vacía. Lo que se revisa es el SQL, no el C#.
3. **Materialización de `Dinero` por constructor.** El diseño de `numeric(19,2)` depende de que EF Core use el constructor privado del complex type. Las pruebas `CA06_SaldoCopRecargado_ConservaFormaCanonica` y `CA07_…` lo verifican; si fallaran, se cambia a `numeric` sin escala (sección 4).
4. **Redacción de la spec de producto.** El RNF de concurrencia dice "reintento acotado y luego 409". ADR-0012 lo concreta: en los cambios de estado no hay reintento automático (lo exige CA-14), y el reintento acotado queda como opción para las operaciones de dinero del S4 (ADR de D-02). Conviene que la spec de producto lo aclare cuando se escriba esa ADR.
5. **Tiempo del sprint (1 semana).** Es la primera feature con EF Core, Testcontainers y Minimal APIs a la vez. Si el tiempo aprieta, lo primero que se puede recortar sin incumplir la spec son las pruebas de los CHECK por SQL y `CL12_ReintentarApertura_AbreUnaSegundaCuenta`, que solo documenta un límite aceptado.
6. **Docker obligatorio para `dotnet test`.** En Windows hace falta Docker Desktop en marcha; sin él, las pruebas de Infrastructure y Api fallan al arrancar el contenedor (no se omiten).
7. **`dotnet format` y `-warnaserror` en el CI** con código generado (migraciones) y con el `#pragma` de `Cliente`. Hay que ejecutar `dotnet format` después de generar la migración.
8. **Correo según la letra de la spec.** `usuario@dominio` no exige punto en el dominio, así que `a@b` se acepta. Si se quisiera exigir un punto, sería un cambio de la spec, no del plan.

Para estudiar antes o durante el Build (además de lo que pide el roadmap del S2):

- **Owned types frente a complex types en EF Core:** por qué aquí se usan los dos.
- **`xmin` y concurrencia optimista:** se adelanta un tema del S4.
- **El patrón "Try" de .NET (`TryParse`)** frente a `Result` y frente a excepciones.
- **`ProblemDetails` (RFC 9457).**
