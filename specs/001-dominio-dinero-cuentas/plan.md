# Plan — 001 Dominio de dinero y cuentas

Estado: Aprobado (2026-10-01)
Spec: [spec.md](spec.md)

## 1. Resumen del diseño

Todo vive en `CoreBancario.Domain`, sin dependencias. `Moneda` es un `record` sellado con solo dos instancias (`COP`, precisión 0; `USD`, precisión 2). `Dinero` es un `record` sellado con constructor privado, propiedades de solo lectura y dos fábricas: `Crear`, que **rechaza** montos negativos o con más decimales de los permitidos, y `DesdeCalculo`, que **ajusta** el resultado de un cálculo con la regla de punto medio de ADR-0007. En ambos casos el monto se guarda en forma canónica (escala igual a la precisión de la moneda). `Cuenta` es la entidad que protege RN-01, RN-02, RN-05, RN-06 y RN-14: cada método valida en el orden fijo de CL-12 (estado → monto → saldo) **antes** de modificar nada (CL-13). `Acreditar` y `Debitar` devuelven el `Movimiento` inmutable que producen, y la fecha y hora llega como parámetro para que las pruebas no dependan del reloj. Los errores son excepciones que heredan de `ReglaDeNegocioException` (ADR-0008).

## 2. Impacto por capa

| Capa | Qué cambia | Archivos |
|---|---|---|
| Domain | Nuevo: base de errores, `Moneda`, `Dinero`, `Cuenta`, `Movimiento`, enums y excepciones | `src/CoreBancario.Domain/ReglaDeNegocioException.cs`; `src/CoreBancario.Domain/Monetario/*.cs`; `src/CoreBancario.Domain/Cuentas/*.cs` (lista completa en la sección 3) |
| Domain.Tests | Primeras pruebas; se quita `--ignore-exit-code 8` del `.csproj` | `tests/CoreBancario.Domain.Tests/Monetario/*.cs`; `tests/CoreBancario.Domain.Tests/Cuentas/*.cs`; `tests/CoreBancario.Domain.Tests/ArquitecturaDelDominioTests.cs`; `tests/CoreBancario.Domain.Tests/CoreBancario.Domain.Tests.csproj` |
| Application | Sin cambios (fuera de alcance, sección 9 de la spec) | — |
| Infrastructure | Sin cambios | — |
| Api | Sin cambios | — |

## 3. Contratos

### Endpoints

No aplica: esta feature no tiene API (sección 9 de la spec).

### Comandos, consultas y eventos

No aplica. No hay eventos de dominio: `Acreditar` y `Debitar` devuelven el `Movimiento` (ver decisión 8.3) y ningún requisito pide notificar a terceros (YAGNI).

### Firmas públicas

La validación de "monto > 0" (RN-04) vive dentro de `Acreditar` y `Debitar` (decisión del autor, sección 11), así que ambos reciben un `Dinero`.

Estructura de carpetas y espacios de nombres (el espacio de nombres sigue a la carpeta):

```
src/CoreBancario.Domain/
  ReglaDeNegocioException.cs              namespace CoreBancario.Domain
  Monetario/                              namespace CoreBancario.Domain.Monetario
    Moneda.cs
    Dinero.cs
    MontoNegativoException.cs
    PrecisionExcedidaException.cs
    MonedasDistintasException.cs
  Cuentas/                                namespace CoreBancario.Domain.Cuentas
    EstadoCuenta.cs
    TipoMovimiento.cs
    Movimiento.cs
    Cuenta.cs
    MontoNoPositivoException.cs
    SaldoInsuficienteException.cs
    OperacionNoPermitidaException.cs
    TransicionNoPermitidaException.cs
    SaldoDistintoDeCeroException.cs
```

La carpeta se llama `Monetario` y no `Dinero` para que el espacio de nombres no se llame igual que el tipo `Dinero` (eso obliga a escribir nombres completos y confunde al compilador).

```csharp
// ───────────── src/CoreBancario.Domain/ReglaDeNegocioException.cs ─────────────
namespace CoreBancario.Domain;

/// <summary>Base de todo error de dominio: una regla de negocio (RN-xx) rota. Ver ADR-0008.</summary>
public abstract class ReglaDeNegocioException : Exception
{
    protected ReglaDeNegocioException(string message) : base(message) { }
}

// ───────────── src/CoreBancario.Domain/Monetario/Moneda.cs ─────────────
namespace CoreBancario.Domain.Monetario;

/// <summary>COP o USD. Conjunto cerrado: el constructor es privado y solo existen estas dos instancias.</summary>
public sealed record Moneda
{
    public static Moneda COP { get; } = new("COP", 0);
    public static Moneda USD { get; } = new("USD", 2);

    /// <summary>Código ISO 4217.</summary>
    public string Codigo { get; }

    /// <summary>Número de decimales que admite la moneda. COP: 0. USD: 2.</summary>
    public int Precision { get; }

    private Moneda(string codigo, int precision);

    /// <returns>El código, por ejemplo "COP".</returns>
    public override string ToString();
}

// ───────────── src/CoreBancario.Domain/Monetario/Dinero.cs ─────────────
namespace CoreBancario.Domain.Monetario;

/// <summary>
/// Par monto + moneda. Invariantes: Monto >= 0 (RN-15); Monto sin más decimales que la precisión de su
/// moneda (RN-16); forma canónica: Monto.Scale == Moneda.Precision (1000 COP se guarda como 1000, 10,5 USD como 10.50).
/// </summary>
public sealed record Dinero
{
    public decimal Monto { get; }
    public Moneda Moneda { get; }

    /// <summary>true si Monto == 0. Propiedad calculada: no forma parte de la igualdad del record.</summary>
    public bool EsCero => Monto == 0m;

    private Dinero(decimal monto, Moneda moneda);

    /// <summary>Crea un Dinero a partir de un monto pedido por alguien. Nunca redondea.</summary>
    /// <exception cref="ArgumentNullException">moneda es null.</exception>
    /// <exception cref="MontoNegativoException">monto &lt; 0 (RN-15, CL-01).</exception>
    /// <exception cref="PrecisionExcedidaException">el valor tiene más decimales que moneda.Precision (RN-16, CL-02). Se evalúa sobre el valor: 1000.00m COP es válido (CL-03).</exception>
    public static Dinero Crear(decimal monto, Moneda moneda);

    /// <summary>
    /// Crea un Dinero a partir del resultado de un cálculo (en S7, una conversión), ajustándolo a la precisión
    /// de la moneda hacia el valor más cercano; en el punto medio exacto redondea al par, MidpointRounding.ToEven (ADR-0007, RN-16, CL-14).
    /// </summary>
    /// <exception cref="ArgumentNullException">moneda es null.</exception>
    /// <exception cref="MontoNegativoException">resultado &lt; 0, comprobado antes de redondear (RN-15).</exception>
    public static Dinero DesdeCalculo(decimal resultado, Moneda moneda);

    /// <exception cref="ArgumentNullException">otro es null.</exception>
    /// <exception cref="MonedasDistintasException">otro.Moneda != Moneda (RN-03).</exception>
    public Dinero Sumar(Dinero otro);

    /// <summary>this − otro.</summary>
    /// <exception cref="ArgumentNullException">otro es null.</exception>
    /// <exception cref="MonedasDistintasException">otro.Moneda != Moneda (RN-03). Se comprueba primero.</exception>
    /// <exception cref="MontoNegativoException">el resultado sería negativo (RN-15, CL-05).</exception>
    public Dinero Restar(Dinero otro);

    /// <returns>Monto en cultura invariante y código: "1000 COP", "10.50 USD".</returns>
    public override string ToString();
}

// ───────────── src/CoreBancario.Domain/Monetario/*Exception.cs ─────────────
namespace CoreBancario.Domain.Monetario;

/// <summary>RN-15: el monto de un Dinero nunca es negativo.</summary>
public sealed class MontoNegativoException : ReglaDeNegocioException
{
    public MontoNegativoException(decimal monto, Moneda moneda);
}

/// <summary>RN-16: un monto con más decimales de los que admite su moneda se rechaza.</summary>
public sealed class PrecisionExcedidaException : ReglaDeNegocioException
{
    public PrecisionExcedidaException(decimal monto, Moneda moneda);
}

/// <summary>RN-03: no se mezclan monedas sin conversión.</summary>
public sealed class MonedasDistintasException : ReglaDeNegocioException
{
    public MonedasDistintasException(Moneda esperada, Moneda recibida);
}

// ───────────── src/CoreBancario.Domain/Cuentas/EstadoCuenta.cs y TipoMovimiento.cs ─────────────
namespace CoreBancario.Domain.Cuentas;

public enum EstadoCuenta { Activa, Bloqueada, Cerrada }

public enum TipoMovimiento { Debito, Credito }

// ───────────── src/CoreBancario.Domain/Cuentas/Movimiento.cs ─────────────
namespace CoreBancario.Domain.Cuentas;

/// <summary>Registro inmutable de un débito o crédito (RN-10). Solo lo crea Cuenta.</summary>
public sealed class Movimiento
{
    public Guid Id { get; }
    public Guid CuentaId { get; }
    public TipoMovimiento Tipo { get; }
    public Dinero Monto { get; }
    public Dinero SaldoResultante { get; }
    public DateTimeOffset FechaHora { get; }

    /// <summary>Id = Guid.CreateVersion7(fechaHora): ordenado en el tiempo y sin leer el reloj.</summary>
    internal Movimiento(Guid cuentaId, TipoMovimiento tipo, Dinero monto, Dinero saldoResultante, DateTimeOffset fechaHora);
}

// ───────────── src/CoreBancario.Domain/Cuentas/Cuenta.cs ─────────────
namespace CoreBancario.Domain.Cuentas;

public sealed class Cuenta
{
    public Guid Id { get; }
    public string Numero { get; }
    public Guid ClienteId { get; }
    public Moneda Moneda { get; }                       // RN-02: sin setter
    public EstadoCuenta Estado { get; private set; }
    public Dinero Saldo { get; private set; }           // siempre en Moneda; nunca negativo (RN-01)

    private Cuenta(Guid id, string numero, Guid clienteId, Moneda moneda);

    /// <summary>Abre una cuenta Activa con saldo 0 en la moneda indicada (RN-02, CA-10). Id = Guid.CreateVersion7().</summary>
    /// <exception cref="ArgumentException">numero es null, vacío o solo espacios.</exception>
    /// <exception cref="ArgumentNullException">moneda es null.</exception>
    public static Cuenta Abrir(string numero, Guid clienteId, Moneda moneda);

    /// <summary>Suma monto al saldo y devuelve el Movimiento de crédito. Orden de validación en la sección 5.</summary>
    /// <exception cref="ArgumentNullException">monto es null.</exception>
    /// <exception cref="OperacionNoPermitidaException">Estado es Cerrada (RN-05).</exception>
    /// <exception cref="MonedasDistintasException">monto.Moneda != Moneda (RN-03).</exception>
    /// <exception cref="MontoNoPositivoException">monto es cero (RN-04).</exception>
    public Movimiento Acreditar(Dinero monto, DateTimeOffset fechaHora);

    /// <summary>Resta monto del saldo y devuelve el Movimiento de débito. Orden de validación en la sección 5.</summary>
    /// <exception cref="ArgumentNullException">monto es null.</exception>
    /// <exception cref="OperacionNoPermitidaException">Estado no es Activa (RN-05).</exception>
    /// <exception cref="MonedasDistintasException">monto.Moneda != Moneda (RN-03).</exception>
    /// <exception cref="MontoNoPositivoException">monto es cero (RN-04).</exception>
    /// <exception cref="SaldoInsuficienteException">monto > Saldo (RN-01).</exception>
    public Movimiento Debitar(Dinero monto, DateTimeOffset fechaHora);

    /// <summary>Activa → Bloqueada.</summary>
    /// <exception cref="TransicionNoPermitidaException">Estado no es Activa (RN-14).</exception>
    public void Bloquear();

    /// <summary>Bloqueada → Activa.</summary>
    /// <exception cref="TransicionNoPermitidaException">Estado no es Bloqueada (RN-14).</exception>
    public void Desbloquear();

    /// <summary>Activa → Cerrada, solo con saldo cero.</summary>
    /// <exception cref="TransicionNoPermitidaException">Estado no es Activa (RN-14, RN-06). Se comprueba primero (CL-12).</exception>
    /// <exception cref="SaldoDistintoDeCeroException">Saldo distinto de cero (RN-06).</exception>
    public void Cerrar();
}

// ───────────── src/CoreBancario.Domain/Cuentas/*Exception.cs ─────────────
namespace CoreBancario.Domain.Cuentas;

/// <summary>RN-04: todo monto de una operación es mayor que cero.</summary>
public sealed class MontoNoPositivoException : ReglaDeNegocioException
{
    public MontoNoPositivoException();
}

/// <summary>RN-01: el saldo nunca queda negativo.</summary>
public sealed class SaldoInsuficienteException : ReglaDeNegocioException
{
    public SaldoInsuficienteException(Dinero saldo, Dinero monto);
}

/// <summary>RN-05: una cuenta Bloqueada rechaza débitos; una Cerrada rechaza todo.</summary>
public sealed class OperacionNoPermitidaException : ReglaDeNegocioException
{
    public OperacionNoPermitidaException(EstadoCuenta estado, TipoMovimiento operacion);
}

/// <summary>RN-14 (y RN-06, Cerrada es final): transición de estado no permitida, incluida la que repite el estado.</summary>
public sealed class TransicionNoPermitidaException : ReglaDeNegocioException
{
    public TransicionNoPermitidaException(EstadoCuenta desde, EstadoCuenta hacia);
}

/// <summary>RN-06: una cuenta solo se cierra con saldo cero.</summary>
public sealed class SaldoDistintoDeCeroException : ReglaDeNegocioException
{
    public SaldoDistintoDeCeroException(Dinero saldo);
}
```

> **Spec viva (revisión, 2026-10-01).** El código documenta cada tipo y método público con `<summary>`, pero no repite las etiquetas `<exception>` de estas firmas: el contrato de errores vive en este plan y lo verifican las pruebas, que es la fuente que no puede quedar desactualizada en silencio.

Notas para el test-writer sobre las firmas:

- Los mensajes de las excepciones van en español y explican la regla, pero **las pruebas comprueban el tipo**, no el texto.
- `Movimiento` tiene constructor `internal`: las pruebas lo obtienen siempre a través de `Cuenta.Acreditar` o `Cuenta.Debitar`. No hace falta `InternalsVisibleTo`.
- Para dejar una cuenta en un estado concreto basta con la API pública: Bloqueada = `Abrir` + `Bloquear`; Cerrada = `Abrir` + `Cerrar`; con saldo = `Acreditar` antes de bloquear.

## 4. Modelo de datos y persistencia

Sin persistencia en esta feature (sección 9 de la spec). Modelo de dominio:

| Tipo | Clase | Identidad | Mutabilidad |
|---|---|---|---|
| `Moneda` | Value object (`sealed record`) | Por valor (`Codigo`, `Precision`) | Inmutable; solo dos instancias |
| `Dinero` | Value object (`sealed record`) | Por valor (`Monto`, `Moneda`) | Inmutable; las operaciones devuelven uno nuevo |
| `Cuenta` | Entidad (raíz de agregado) | `Id` (`Guid`, UUID v7) | `Estado` y `Saldo` cambian solo por sus métodos |
| `Movimiento` | Entidad, fuera del agregado `Cuenta` | `Id` (`Guid`, UUID v7 con el instante del movimiento) | Inmutable (RN-10) |

**Forma canónica de `Dinero`.** `Crear` y `DesdeCalculo` guardan el monto con `Scale == Moneda.Precision`. Así, `Crear(1000.00m, COP)` y `Crear(1000m, COP)` no solo son iguales (`==` de `decimal` compara valores), sino que se representan igual: `ToString()` da `"1000 COP"` en ambos casos. Esto importa en S4, donde el hash de idempotencia de una solicitud no puede cambiar según cómo se escribió el monto. Pista de implementación, no contrato: `decimal.Round` solo reduce la escala; para fijarla se puede sumar un cero con la escala deseada (`new decimal(0, 0, 0, false, (byte)precision)`), porque la suma conserva la escala mayor. La prueba de `Monto.Scale` dirá si funciona.

**Por qué `Dinero` no usa `init`.** Con propiedades `{ get; init; }`, la expresión `dinero with { Monto = -5m }` crearía una copia sin pasar por `Crear`, saltándose la validación. Con `{ get; }` y constructor privado, la única forma de obtener un `Dinero` es `Crear`, `DesdeCalculo`, `Sumar` o `Restar`, y todas validan. Lo mismo vale para `Moneda`.

**Notas para S2 (no se implementan ahora):**

- `Moneda` se persistirá como su `Codigo` con un value converter de EF Core. Para eso hará falta `Moneda.DesdeCodigo(string)`, que se añade en S2 y no ahora (YAGNI).
- `Dinero` encaja como *complex type* de EF Core (`ComplexProperty`). En `Cuenta.Saldo` solo hace falta guardar el monto, porque la moneda ya está en la cuenta.
- EF Core puede necesitar un constructor privado sin parámetros en `Cuenta` y `Movimiento`. Añadirlo no cambia la API pública.
- `EstadoCuenta` y `TipoMovimiento` conviene guardarlos como texto para que reordenar el enum no cambie el significado de los datos.
- Npgsql solo escribe en `timestamptz` un `DateTimeOffset` con desfase 0: el caso de uso debe pasar `fechaHora` en UTC (por ejemplo, `TimeProvider.GetUtcNow()`).
- El caso de uso debe guardar el `Movimiento` devuelto en la misma transacción que el saldo (spec de producto, modelo de dominio).

## 5. Casos límite y su manejo

**Orden de validación (CL-12) y "sin cambios" (CL-13).** Cada método valida con guard clauses en este orden y solo al final modifica el estado. Si una comprobación lanza, no se ha tocado nada.

| Método | 0. Argumentos (error de programación) | 1. Estado | 2. Monto: moneda | 3. Monto: > 0 | 4. Saldo | 5. Cambio |
|---|---|---|---|---|---|---|
| `Acreditar` | `monto` null → `ArgumentNullException` | Estado ∉ {Activa, Bloqueada} → `OperacionNoPermitidaException` | `MonedasDistintasException` | `monto.EsCero` → `MontoNoPositivoException` | — | `Saldo = Saldo.Sumar(monto)`; devuelve `Movimiento` de crédito |
| `Debitar` | `monto` null → `ArgumentNullException` | Estado ∉ {Activa} → `OperacionNoPermitidaException` | `MonedasDistintasException` | `monto.EsCero` → `MontoNoPositivoException` | `monto.Monto > Saldo.Monto` → `SaldoInsuficienteException` | `Saldo = Saldo.Restar(monto)`; devuelve `Movimiento` de débito |
| `Bloquear` / `Desbloquear` | — | Transición no permitida → `TransicionNoPermitidaException` | — | — | — | `Estado = destino` |
| `Cerrar` | — | Transición no permitida → `TransicionNoPermitidaException` | — | — | `!Saldo.EsCero` → `SaldoDistintoDeCeroException` | `Estado = Cerrada` |

Los estados permitidos se escriben como **lista blanca** ("se permite si está en {…}"), nunca como lista negra ("se rechaza si es Cerrada"): si algún día aparece un cuarto estado, queda denegado por defecto.

**Transiciones permitidas (RN-14).** Solo estas tres: Activa → Bloqueada, Bloqueada → Activa y Activa → Cerrada. Cualquier otro par (origen, destino) se rechaza, incluido el que repite el estado. Mecanismo (decisión del autor, sección 11): un método privado de `Cuenta` con una expresión `switch` sobre la tupla `(origen, destino)`. Tiene un brazo `=> true` por cada transición permitida y `_ => false` para todo lo demás (lista blanca). `Bloquear`, `Desbloquear` y `Cerrar` lo consultan antes de cambiar nada y, si devuelve `false`, lanzan `TransicionNoPermitidaException(Estado, destino)`. No es una firma pública:

```csharp
private static bool EsTransicionPermitida(EstadoCuenta origen, EstadoCuenta destino) => (origen, destino) switch
{
    (EstadoCuenta.Activa, EstadoCuenta.Bloqueada) => true,
    (EstadoCuenta.Bloqueada, EstadoCuenta.Activa) => true,
    (EstadoCuenta.Activa, EstadoCuenta.Cerrada) => true,
    _ => false,
};
```

**Segunda defensa de RN-01.** `Debitar` comprueba el saldo y lanza `SaldoInsuficienteException` **antes** de llamar a `Saldo.Restar`. La `MontoNegativoException` de `Restar` queda como red de seguridad que, con el código correcto, no se alcanza nunca desde `Cuenta` (decisión del autor, sección 11 de la spec).

> **Spec viva (revisión, 2026-10-01).** La columna "Pruebas" recoge los nombres reales tras el Build y los ajustes de la revisión. Varias pruebas del plan original se dividieron en dos (una por moneda o por operación) sin cambiar lo que comprueban. Además: cada regla tiene ahora al menos una prueba con su ID (`RN02_`, `RN03_`, `RN05_`, `RN06_`, `RN10_`), y las pruebas de argumentos nulos o vacíos llevan el prefijo `ADR0008_`, porque son errores de programación y no reglas de negocio.

| ID | Caso | Manejo | Pruebas |
|---|---|---|---|
| CL-01 | Dinero con monto negativo | `Crear` lanza `MontoNegativoException` | `CA01_CrearConMontoNegativo_LanzaMontoNegativoException` (−1 COP); `CA01_CrearUsdConMontoNegativo_LanzaMontoNegativoException` (−0,01 USD) |
| CL-02 | Dinero con más decimales que su moneda | `Crear` lanza `PrecisionExcedidaException`; se evalúa `decimal.Round(monto, precision) != monto` | `CA03_CrearUsdConMasDecimalesQueLaMoneda_LanzaPrecisionExcedidaException` (10,555 USD); `CA03_CrearCopConDecimales_LanzaPrecisionExcedidaException` (1.000,50 COP) |
| CL-03 | Precisión evaluada sobre el valor | 1.000,00 COP y 10,50 USD son válidos; se guardan en forma canónica | `CA04_CrearCopConCerosSobrantes_SeCrea`; `CA04_CrearUsdConDosDecimales_SeCrea`; `CL03_CrearConCerosSobrantes_GuardaLaEscalaDeSuMoneda` (1000.00m COP → `Scale` 0 y `"1000 COP"`); `CL03_CrearUsdConUnDecimal_GuardaDosDecimales` (10.5m → `Scale` 2 y `"10.50 USD"`) |
| CL-04 | Monedas distintas | `Sumar`/`Restar` lanzan `MonedasDistintasException`; `Acreditar`/`Debitar` también (paso 2) | `CA05_SumarMonedasDistintas_…`; `CA05_RestarMonedasDistintas_…`; `CA05_RestarMonedasDistintasQueTambienDaNegativo_…`; `CA16_AcreditarEnOtraMoneda_LanzaMonedasDistintasSinCambios`; `CA16_DebitarEnOtraMoneda_LanzaMonedasDistintasSinCambios`; `RN03_SumarCopYUsd_LanzaMonedasDistintas` |
| CL-05 | Resta que daría negativo | `Restar` lanza `MontoNegativoException` | `CA07_RestarMasDeLoQueHay_LanzaMontoNegativoException`; `CA07_RestarElMismoMonto_DaCero` |
| CL-06 | Acreditar o debitar cero | Paso 3: `MontoNoPositivoException` | `CA15_AcreditarCero_LanzaMontoNoPositivoSinCambios`; `CA15_DebitarCero_LanzaMontoNoPositivoSinCambios` |
| CL-07 | Debitar exactamente el saldo | Se acepta (`>` estricto); saldo 0 | `CA13_DebitarTodoElSaldo_DejaSaldoCero` |
| CL-08 | Debitar más que el saldo | Paso 4: `SaldoInsuficienteException` | `CA14_DebitarMasQueElSaldo_LanzaSaldoInsuficienteSinCambios`; `RN01_DebitarMasQueElSaldo_LanzaSaldoInsuficienteException` |
| CL-09 | Bloqueada: créditos sí, débitos no | Lista blanca de estados por operación | `CA17_AcreditarCuentaBloqueada_AumentaElSaldo` y `CA17_AcreditarCuentaBloqueada_DevuelveMovimientoDeCredito` (desde 0 COP, como dice la spec); `CA18_DebitarCuentaBloqueada_LanzaOperacionNoPermitidaSinCambios`; `RN05_CuentaBloqueada_AceptaCredito`; `RN05_CuentaBloqueada_RechazaDebitoSinCambios` |
| CL-10 | Cerrada rechaza todo | Operaciones: `OperacionNoPermitidaException`; transiciones: `TransicionNoPermitidaException` | `CA19_AcreditarCuentaCerrada_…`; `CA19_DebitarCuentaCerrada_…`; `CA25_BloquearCuentaCerrada_…`; `CA25_DesbloquearCuentaCerrada_…`; `CA25_CerrarCuentaCerrada_…` |
| CL-11 | Transición no permitida o repetida | `TransicionNoPermitidaException` | `CA24_BloquearBloqueada_LanzaTransicionNoPermitidaSinCambios`; `CA24_DesbloquearActiva_LanzaTransicionNoPermitidaSinCambios`; `CA23_CerrarCuentaBloqueada_LanzaTransicionNoPermitidaYSigueBloqueada` |
| CL-12 | Varias reglas rotas: estado → monto (moneda, > 0) → saldo | Orden de la tabla de arriba | `CA26_CerrarBloqueadaConSaldo_LanzaTransicionNoPermitida`; `CA27_DebitarBloqueadaMasQueElSaldo_LanzaOperacionNoPermitida`; `CL12_AcreditarCerradaEnOtraMoneda_LanzaOperacionNoPermitida`; `CL12_DebitarCeroConCuentaBloqueada_LanzaOperacionNoPermitida`; `CL12_DebitarCeroEnOtraMoneda_LanzaMonedasDistintas`; `CL12_DebitarCeroMasQueNingunSaldo_LanzaMontoNoPositivo`; `CL12_DebitarEnOtraMonedaMasQueElSaldo_LanzaMonedasDistintas` |
| CL-13 | Rechazo sin cambios | Validar todo antes de modificar | En cada prueba de rechazo de `Cuenta`, tras `Assert.Throws`, se comprueba que `Saldo` y `Estado` siguen como estaban. Que no hay movimiento lo garantiza la excepción: el método no devuelve nada |
| CL-14 | Ajuste de un resultado de cálculo al valor más cercano | `DesdeCalculo` redondea con `MidpointRounding.ToEven` (ADR-0007), siempre explícito | `CA09_DesdeCalculoUsd_AjustaAlValorMasCercano` (24,2515 USD → 24,25); `CA09_DesdeCalculoCop_AjustaAlValorMasCercano` (42.595,2385 COP → 42.595); `CL14_DesdeCalculoUsdPorEncimaDeLaMitad_SubeAlSiguiente` y `CL14_DesdeCalculoCopPorEncimaDeLaMitad_SubeAlSiguiente` (demuestran que no se trunca); `RN16_DesdeCalculoCopEnPuntoMedioExacto_RedondeaAlPar` y `RN16_DesdeCalculoUsdEnPuntoMedioExacto_RedondeaAlPar` (tabla siguiente); `RN15_DesdeCalculoCopConResultadoNegativo_LanzaMontoNegativoException`; `RN15_DesdeCalculoNegativoQueRedondeaACero_LanzaMontoNegativoException`; `CL14_DesdeCalculoCop_GuardaLaEscalaDeSuMoneda`; `CL14_DesdeCalculoUsd_GuardaLaEscalaDeSuMoneda` |

Otras reglas con prueba propia: `RN02_OperarLaCuenta_NoCambiaSuMoneda`, `RN06_CerrarConSaldoDistintoDeCero_LanzaSaldoDistintoDeCeroSinCambios`, `RN10_MovimientoEmitido_NoCambiaTrasOperacionesPosteriores`, `RN14_BloquearDesbloquearYCerrarConSaldoCero_QuedaCerrada`. Errores de programación (ADR-0008): ocho pruebas `ADR0008_…`.

**Puntos medios para `RN16_DesdeCalculoCopEnPuntoMedioExacto_RedondeaAlPar` y `RN16_DesdeCalculoUsdEnPuntoMedioExacto_RedondeaAlPar`** (ADR-0007: al par, `ToEven`). Las filas incluyen casos donde el último dígito conservado es par, en los que se baja, y casos donde es impar, en los que se sube. Así la prueba falla si alguien cambia a "hacia arriba desde la mitad".

| Entrada | Moneda | Esperado |
|---|---|---|
| 41234.5m (10 USD × 4.123,45) | COP | 41234 |
| 123703.5m (30 USD × 4.123,45) | COP | 123704 |
| 206172.5m (50 USD × 4.123,45) | COP | 206172 |
| 10.125m | USD | 10.12 |
| 10.135m | USD | 10.14 |

## 6. Estrategia para los requisitos no funcionales

| RNF | Cómo se cumple | Cómo se verifica |
|---|---|---|
| RNF-01 | `CoreBancario.Domain.csproj` sigue sin `PackageReference` ni `ProjectReference`; solo se usa la biblioteca base de .NET (`Guid.CreateVersion7`, `decimal`, `DateTimeOffset`) | `RNF01_EnsambladoDelDominio_SoloReferenciaElFramework`: por reflexión, `typeof(Dinero).Assembly.GetReferencedAssemblies()` solo contiene nombres de una lista exacta de ensamblados del framework (tras la revisión; antes aceptaba cualquier nombre que empezara por `System`, lo que dejaría pasar un paquete NuGet como `System.Reactive`). Si el dominio necesita otro ensamblado legítimo del framework, se añade a la lista de forma explícita. Además, revisión del `.csproj` en `/sdd-review` |
| RNF-02 | Flujo de `/sdd-build`: el test-writer escribe todas las pruebas antes que el coder | El commit `test(001): pruebas en rojo` muestra las pruebas fallando; cada RN tiene al menos una prueba (sección 9) |
| RNF-03 | Sin reloj: `fechaHora` llega como parámetro y las pruebas usan un instante fijo (`new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)`); el id del `Movimiento` se deriva de ese instante. Sin disco ni red: las pruebas de arquitectura usan reflexión sobre el ensamblado ya cargado, no leen archivos | Revisión de las pruebas en `/sdd-review` y duración que informa `dotnet test` (< 5 s). Excepción conocida: `Cuenta.Abrir` usa `Guid.CreateVersion7()`, que lee el reloj solo para ordenar la id; ninguna prueba depende de su valor (ver sección 10) |
| RNF-04 | `decimal` en `Dinero`; ningún `double` ni `float` en la API pública | `RNF04_MiembrosPublicosDelDominio_NoUsanDoubleNiFloat`: por reflexión sobre `GetExportedTypes()`, ninguna propiedad, campo, parámetro ni tipo de retorno público es `double` o `float` |

## 7. Estrategia de pruebas

| Nivel | Qué se prueba | Proyecto de tests |
|---|---|---|
| Unitarias (Domain) | `Moneda`, `Dinero`, `Cuenta`, `Movimiento`: todos los CA y CL de la sección 5, más las dos pruebas de arquitectura (RNF-01, RNF-04) | `tests/CoreBancario.Domain.Tests` |
| Integración (Infrastructure) | No aplica en esta feature | — |
| API | No aplica en esta feature | — |

Organización de los archivos de prueba:

| Archivo | Contenido |
|---|---|
| `tests/CoreBancario.Domain.Tests/Monetario/MonedaTests.cs` | `RN04_MonedaCop_TienePrecisionCero`, `RN04_MonedaUsd_TienePrecisionDos` |
| `tests/CoreBancario.Domain.Tests/Monetario/DineroTests.cs` | CA-01…CA-09, CL-01…CL-05, CL-14; pruebas de forma canónica; `CA08_MismoValorEscritoDistinto_SonIgualesConElMismoHash` (1000m y 1000.00m COP) |
| `tests/CoreBancario.Domain.Tests/Cuentas/CuentaOperacionesTests.cs` | CA-10…CA-19, CA-27, CL-06…CL-10, CL-12 y CL-13 de créditos y débitos |
| `tests/CoreBancario.Domain.Tests/Cuentas/CuentaEstadosTests.cs` | CA-20…CA-26, CL-10, CL-11, CL-12 y CL-13 de transiciones |
| `tests/CoreBancario.Domain.Tests/Cuentas/MovimientoTests.cs` | CA-28 y los datos del `Movimiento` (CA-11, CA-12 comprueban tipo, monto y saldo resultante; además `FechaHora` y `CuentaId`) |
| `tests/CoreBancario.Domain.Tests/ArquitecturaDelDominioTests.cs` | RNF-01, RNF-04 |

Convenciones y pautas:

- Nombre `IDRegla_Escenario_Resultado`; el prefijo es el CA, CL, RN o RNF que se prueba.
- Arrange / Act / Assert, un comportamiento por prueba, sin `if` ni bucles.
- `[Theory]` con decimales: `TheoryData<decimal, decimal>` (entrada, esperado) + `[MemberData]`, **una teoría por moneda**. `Moneda` no es serializable para xUnit y, si se pasa como dato, las filas se agrupan en un solo caso.
- Ayudantes privados en cada clase de prueba, sin builders ni librerías: `static Dinero Cop(decimal monto)`, `static Dinero Usd(decimal monto)`, `static Cuenta CuentaActivaConSaldo(decimal pesos)`, `static Cuenta CuentaBloqueadaConSaldo(decimal pesos)`, `static Cuenta CuentaCerrada()` y la constante del instante fijo.
- **CA-28** se prueba de dos formas: (1) estructural, por reflexión: ninguna propiedad pública de `Movimiento` ni de `Dinero` tiene setter público, incluido `init` (`SetMethod` es null o no es público); (2) de comportamiento: `CA28_OperarDespuesDeUnMovimiento_NoCambiaElMovimientoAnterior` (tras un segundo crédito, el `SaldoResultante` del primero no cambia).
- Las pruebas de arquitectura (RNF-01, RNF-04) y la estructural de CA-28 **pasarán desde el principio** con los esqueletos: son guardas contra regresiones, no guían la implementación. Es la excepción esperada a "toda prueba nueva empieza en rojo".
- Al añadir la primera prueba, el test-writer quita la línea `--ignore-exit-code 8` (y su comentario) de `tests/CoreBancario.Domain.Tests/CoreBancario.Domain.Tests.csproj` (ADR-0006).

## 8. Decisiones

ADRs:

- ADR-0007: Regla del punto medio al ajustar Dinero a la precisión de su moneda: al par (`MidpointRounding.ToEven`), por neutralidad (RN-16).
- ADR-0008: Errores de dominio como excepciones con una clase base común (`ReglaDeNegocioException`); los errores de programación usan `ArgumentException` y no heredan de la base. `Result` queda para la validación de entrada en Application.

Decisiones del plan (sin ADR porque son locales a esta feature y fáciles de cambiar):

1. **`Moneda` como `sealed record` con dos instancias, no como `enum`.** La precisión viaja con la moneda (cohesión) y no se puede fabricar una moneda inválida. Con un `enum`, `(Moneda)99` compila y habría que validarlo en cada uso. Coste: en S2 hará falta `DesdeCodigo` para EF Core.
2. **Identificadores `Guid` (UUID v7) generados en el dominio, sin tipos propios (`CuentaId`, `ClienteId`).** El v7 está ordenado por tiempo y se comporta mejor que el aleatorio (v4) en los índices de PostgreSQL, lo que ayuda al extracto de RF-06. `Movimiento` lo deriva de su `fechaHora`, así que no lee el reloj. Los ids con tipo propio evitarían confundir `ClienteId` con `CuentaId`, pero añaden un value converter por id en S2. Con solo dos ids en juego se prefiere KISS: el riesgo de confusión se cubre con nombres de parámetro claros y pruebas.
3. **`Acreditar` y `Debitar` devuelven el `Movimiento`, en lugar de acumularlo dentro de `Cuenta`.** La spec de producto saca los movimientos del agregado para no cargar el historial. Devolverlo es lo más simple y explícito: quien llama lo recibe y lo guarda, y las pruebas lo comprueban sin estado oculto. La alternativa (una lista de "movimientos pendientes" que el repositorio vacía) protege mejor contra el olvido de guardarlo, pero añade estado y una convención de vaciado que ningún requisito de hoy pide. El riesgo se cubre en S4 con una prueba de integración.
4. **La fecha y hora es un parámetro `DateTimeOffset fechaHora`, no un `TimeProvider` inyectado.** Una entidad no debería guardar servicios. Pasar el valor deja el dominio como funciones puras de su entrada; el caso de uso (S2/S4) la obtiene de `TimeProvider`, que sí es una frontera.
5. **Nombres de excepciones por la condición encontrada** (`SaldoInsuficiente`, `MonedasDistintas`, `SaldoDistintoDeCero`, `TransicionNoPermitida`…), cada una ligada a una sola RN en su comentario XML. Así el nombre dice qué pasó y la RN dice qué regla lo prohíbe. Sin propiedades extra: el mensaje basta y ningún requisito pide más (YAGNI).
6. **Forma canónica del monto** (`Scale == Precision`) para que la representación no dependa de cómo se escribió el número (sección 4).
7. **`Dinero.DesdeCalculo` separado de `Crear`.** RN-16 distingue un monto pedido (se rechaza si sobra precisión) del resultado de un cálculo (se ajusta). Dos fábricas con nombres distintos hacen imposible redondear por accidente un monto que alguien pidió.
8. **"Monto > 0" se valida dentro de `Acreditar`/`Debitar`** (decisión del autor). Se descarta un tipo `MontoDeOperacion` porque rechazaría el cero antes de mirar el estado y rompería CL-12.
9. **Transiciones con una expresión `switch` sobre tuplas `(origen, destino)` y `_ => false`** (decisión del autor; sección 5). Se descartan una tabla de pares y el patrón State: más maquinaria para tres transiciones de sí/no, y en el caso de State, una persistencia más difícil en S2.

## 9. Trazabilidad

| ID de la spec | Sección del plan | Tareas |
|---|---|---|
| RF (ninguno) | 1; la feature no completa RF; prepara RF-02, RF-03, RF-04, RF-07 y RF-08 (sección 3 de la spec) | — |
| RN-01 | 3 (`Debitar`), 5 (paso 4, segunda defensa) | T08 |
| RN-02 | 3 (`Cuenta.Moneda` sin setter, `Abrir`), 5 (paso 2) | T06, T07, T08 |
| RN-03 | 3 (`Sumar`, `Restar`, `Acreditar`, `Debitar`), 5 | T03, T07, T08 |
| RN-04 | 3 (`Moneda.Precision`, `MontoNoPositivoException`), 5 (paso 3), 8.8 | T01, T07, T08 |
| RN-05 | 5 (lista blanca de estados por operación) | T07, T08, T09, T10 |
| RN-06 | 3 (`Cerrar`), 5 | T10 |
| RN-10 | 3 (`Movimiento` inmutable, constructor `internal`), 4 | T05, T07 |
| RN-14 | 5 (transiciones permitidas, `switch` con tuplas), 8.9 | T09, T10 |
| RN-15 | 3 (`Crear`, `Restar`, `DesdeCalculo`) | T02, T03, T04 |
| RN-16 | 3 (`Crear`, `DesdeCalculo`), 4 (forma canónica), 5 (tabla de puntos medios); ADR-0007 | T02, T04 |
| RNF-01 | 6 | T11 |
| RNF-02 | 6, 7 | Todas (flujo de `/sdd-build`) |
| RNF-03 | 6, 8.4 | T05, T07, T08, T11 |
| RNF-04 | 6 | T11 |
| CL-01 | 5 | T02 |
| CL-02 | 5 | T02 |
| CL-03 | 4, 5 | T02 |
| CL-04 | 5 | T03, T07, T08 |
| CL-05 | 5 | T03 |
| CL-06 | 5 | T07, T08 |
| CL-07 | 5 | T08 |
| CL-08 | 5 | T08 |
| CL-09 | 5 | T09 |
| CL-10 | 5 | T10 |
| CL-11 | 5 | T09, T10 |
| CL-12 | 5 (orden de validación) | T07, T08, T09, T10 |
| CL-13 | 5 | T07, T08, T09, T10 |
| CL-14 | 5; ADR-0007 | T04 |
| CA-01 | 5 (CL-01) | T02 |
| CA-02 | 3 (`Crear`, `EsCero`) | T02 |
| CA-03 | 5 (CL-02) | T02 |
| CA-04 | 5 (CL-03) | T02 |
| CA-05 | 5 (CL-04) | T03 |
| CA-06 | 3 (`Sumar`, `Restar`) | T03 |
| CA-07 | 5 (CL-05) | T03 |
| CA-08 | 3 (`sealed record`), 4 (forma canónica) | T02 |
| CA-09 | 5 (CL-14) | T04 |
| CA-10 | 3 (`Abrir`) | T06 |
| CA-11 | 3 (`Acreditar`) | T07 |
| CA-12 | 3 (`Debitar`) | T08 |
| CA-13 | 5 (CL-07) | T08 |
| CA-14 | 5 (CL-08, CL-13) | T08 |
| CA-15 | 5 (CL-06) | T07, T08 |
| CA-16 | 5 (CL-04) | T07, T08 |
| CA-17 | 5 (CL-09) | T09 |
| CA-18 | 5 (CL-09) | T09 |
| CA-19 | 5 (CL-10) | T10 |
| CA-20 | 5 (transiciones) | T09 |
| CA-21 | 3 (`Cerrar`) | T10 |
| CA-22 | 5 (`Cerrar`, paso 4) | T10 |
| CA-23 | 5 (CL-11) | T10 |
| CA-24 | 5 (CL-11) | T09 |
| CA-25 | 5 (CL-10) | T10 |
| CA-26 | 5 (CL-12) | T10 |
| CA-27 | 5 (CL-12) | T09 |
| CA-28 | 3 (`Movimiento`), 7 | T05, T07 |
| D-07 | ADR-0007 | T04 |

## 10. Riesgos y preguntas resueltas

Preguntas resueltas por el autor (detalle en la sección 11):

1. **D-07, punto medio:** al par (`MidpointRounding.ToEven`). Ver ADR-0007.
2. **Dónde se valida "monto de operación > 0" (RN-04):** dentro de `Acreditar`/`Debitar`. Se descartó un tipo `MontoDeOperacion` porque rechazaría el cero antes de mirar el estado y rompería CL-12.
3. **Mecanismo de transiciones (RN-14):** expresión `switch` con tuplas `(origen, destino)` y `_ => false` (lista blanca). Ver sección 5.

No quedan preguntas abiertas.

Riesgos:

- **`Guid.CreateVersion7()` en `Cuenta.Abrir` lee el reloj del sistema.** Ninguna prueba depende de su valor, así que el determinismo de RNF-03 se mantiene, pero una lectura literal de "ni reloj del sistema" lo objetaría. Si el revisor o el autor lo prefieren, `Abrir` puede recibir el `Guid id` desde el caso de uso (cambio de firma: `Abrir(Guid id, string numero, Guid clienteId, Moneda moneda)`).
- **Desbordamiento de `decimal`.** `Sumar` con montos cercanos a 7,9 × 10²⁸ lanza `OverflowException` (no es un error de dominio). Los límites de monto quedan fuera de alcance (sección 9 de la spec); en S2 la validación de entrada de la Api pondrá un máximo razonable.
- **Olvidar guardar el `Movimiento` devuelto** dejaría un saldo sin su movimiento. El dominio no puede impedirlo con la opción elegida (decisión 8.3); lo cubre una prueba de integración en S4.
- **Forma canónica cerca del máximo de `decimal` (S2).** Con montos cercanos a 7,9 × 10²⁸, sumar el cero con escala no puede conservar `Scale == Precision`. Cuando S2 fije el monto máximo de entrada en la Api, ese límite queda muy por debajo y el riesgo desaparece (revisión, sugerencia 6).
- **Orden de los movimientos en el extracto (S4).** `Guid.CreateVersion7(fechaHora)` no garantiza orden entre movimientos del mismo milisegundo (sus bits bajos son aleatorios). El extracto debe ordenar por `FechaHora` y una columna de secuencia, no solo por `Id` (revisión, sugerencia 7).
- **Forma canónica e hash de idempotencia (S4).** El hash debe calcularse sobre `Dinero` canónico (`ToString()` o `Monto` + `Moneda.Codigo`), nunca sobre el texto crudo de la solicitud.

## 11. Decisiones del autor

- **D-07, al par (`ToEven`).** Ver ADR-0007, que recoge el porqué con sus palabras.
- **"Monto > 0" dentro de `Acreditar`/`Debitar`.** Un tipo `MontoDeOperacion` rechazaría el cero antes de mirar el estado y rompería CL-12. La regla sigue perteneciendo a la operación.
- **Transiciones con `switch` y tuplas.** Con sus palabras: "Switch por simplicidad: poco código, fácil de depurar y todo está en un mismo sitio; en esta parte del Sprint 1 hay pocas acciones, pocos estados y reglas simples."
- **ADR-0008 aceptada en su contenido:** excepciones con una clase base común en el dominio y `Result` reservado para la validación de entrada en Application. Ver la ADR, que recoge el porqué con sus palabras.
