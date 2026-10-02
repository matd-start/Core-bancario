# Aprendizaje — 001 Dominio de dinero y cuentas

## 1. Mapa del código

Todo vive en la capa **Domain** (`src/CoreBancario.Domain`), que no depende de nada externo.

| Archivo | Qué hace |
|---|---|
| `ReglaDeNegocioException.cs` | Clase base de todos los errores de negocio (ADR-0008). |
| `Monetario/Moneda.cs` | COP (0 decimales) y USD (2 decimales). Solo existen esas dos instancias. |
| `Monetario/Dinero.cs` | Monto + moneda. Nunca negativo, nunca con más decimales de los permitidos. |
| `Monetario/*Exception.cs` | `MontoNegativo`, `PrecisionExcedida`, `MonedasDistintas`. |
| `Cuentas/Cuenta.cs` | Entidad principal: saldo y estado, con todas sus reglas. |
| `Cuentas/Movimiento.cs` | Registro inmutable de un crédito o débito. |
| `Cuentas/EstadoCuenta.cs`, `TipoMovimiento.cs` | Enumeraciones. |
| `Cuentas/*Exception.cs` | `MontoNoPositivo`, `SaldoInsuficiente`, `OperacionNoPermitida`, `TransicionNoPermitida`, `SaldoDistintoDeCero`. |

## 2. Recorrido de una petición (todavía sin API)

Aún no hay endpoint; esto es lo que haría un futuro caso de uso de "depositar 50 USD":

1. Llama a `Dinero.Crear(50m, Moneda.USD)`: rechaza negativos y más de 2 decimales, y guarda `50.00`.
2. Llama a `cuenta.Acreditar(dinero, fechaHora)`.
3. `Cuenta` valida en orden: estado, moneda, monto mayor que cero. Si algo falla lanza una excepción y **no ha tocado nada**.
4. Si todo pasa: `Saldo = Saldo.Sumar(monto)` (`Dinero.Sumar` devuelve un `Dinero` nuevo).
5. Se crea y devuelve un `Movimiento` con el saldo resultante; su `Id` es `Guid.CreateVersion7(fechaHora)`.
6. Quien llamó (en el futuro, Application/Infrastructure) guarda cuenta y movimiento en la misma transacción.

## 3. Conceptos clave

**Value object (`Dinero`, `Moneda`).** Resuelve que un número suelto no dice su moneda ni sus reglas. Se define por su valor (dos `Dinero` de 10 USD son iguales) y es inmutable. Analogía: un billete; da igual cuál, importa lo que vale. Está en `Dinero.cs`. Tiene constructor privado y propiedades `{ get; }` (sin `init`) para que `with { Monto = -5 }` no se salte la validación.

**Forma canónica.** `1000m` y `1000.00m` son iguales para `decimal`, pero se escriben distinto, y eso rompería un futuro hash de idempotencia. `AFormaCanonica` en `Dinero.cs` fuerza `Scale == Precision` (primero `Round`, que solo reduce la escala, y luego suma un cero con la escala deseada, que la amplía). Analogía: escribir siempre las fechas como AAAA-MM-DD.

**`Crear` frente a `DesdeCalculo`.** `Crear` es para un monto que alguien pidió: si trae demasiados decimales, se **rechaza** (nunca se redondea en silencio). `DesdeCalculo` es para resultados de cuentas (por ejemplo una conversión): se **ajusta** con redondeo al par (`MidpointRounding.ToEven`, ADR-0007). Dos nombres distintos hacen imposible redondear por accidente un monto del usuario. Además `DesdeCalculo` comprueba el negativo antes de redondear, para que un -0,001 no se vuelva un 0 válido.

**Entidad que protege sus invariantes (`Cuenta`).** No hay setters públicos: el saldo y el estado solo cambian con `Acreditar`, `Debitar`, `Bloquear`, `Desbloquear` y `Cerrar`. Cada método valida **todo primero** (guard clauses, orden fijo de CL-12: estado, moneda, monto > 0, saldo) y modifica **al final**, así un rechazo nunca deja cambios a medias (CL-13). Los estados se escriben como **lista blanca** ("se permite si es Activa o Bloqueada"), de modo que un estado nuevo quede denegado por defecto. Analogía: un cajero que revisa todos los requisitos antes de abrir la caja.

**Máquina de estados con `switch` sobre tupla.** `EsTransicionPermitida` lista las 3 transiciones válidas y `_ => false` para el resto. `Bloquear`, `Desbloquear` y `Cerrar` la consultan.

**UUID v7 con `fechaHora` (`Movimiento`).** `Guid.CreateVersion7(fechaHora)` crea un id ordenable por tiempo (bueno para índices en PostgreSQL) sin leer el reloj, porque la fecha llega como parámetro. Así las pruebas son deterministas.

## Qué conviene estudiar

- Value objects y records en C# (igualdad por valor, `with`).
- `decimal` en .NET: escala (`Scale`), `MidpointRounding` y por qué existe el redondeo al par.
- Invariantes y agregados (Domain-Driven Design, nivel básico).
- UUID v7 frente a v4 como clave primaria.

## 4. Para practicar (Copiar, Modificar, Recrear)

1. **Copiar.** Sigue con el depurador la prueba `RN16_DesdeCalculoEnPuntoMedioExacto_RedondeaAlPar` hasta `AFormaCanonica` y anota la escala del `decimal` en cada paso.
2. **Modificar.** En una rama de prueba, cambia `ToEven` por `AwayFromZero` en `Dinero.cs`. Ejecuta `dotnet test`: ¿qué pruebas fallan y por qué? Revierte el cambio.
3. **Recrear.** Borra el cuerpo de `Cerrar` y vuelve a escribirlo mirando solo las pruebas de `CuentaEstadosTests.cs`. Después compara con la versión original: ¿pusiste la transición antes que el saldo?
