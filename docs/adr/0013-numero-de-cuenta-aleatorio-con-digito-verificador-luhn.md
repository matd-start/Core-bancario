# ADR-0013: Número de cuenta aleatorio con dígito verificador Luhn

- Estado: Aceptada
- Fecha: 2026-10-05
- Feature: 002-backoffice-clientes-cuentas (RN-18)

## Contexto

RN-18: el número de cuenta lo asigna el banco, no cambia nunca, es único, tiene 10 dígitos, **no sigue un orden que permita deducir otros números** y su último dígito es verificador. La spec añade que no empieza por 0 (para que no se pierda al tratarlo como número en una hoja de cálculo). El autor lo eligió para evitar la enumeración (conocer una cuenta no revela las vecinas) y para que el S5 detecte errores de tipeo en la cuenta destino. CL-11 pide que una colisión se resuelva sin que el operador lo note, y CA-09, que 50 aperturas den números distintos, válidos y no consecutivos.

Dos conceptos para leer esta ADR:

- **Dígito verificador:** un dígito calculado a partir de los demás. Si alguien se equivoca al teclear, el cálculo no cuadra y el error se detecta antes de buscar la cuenta. Analogía: la letra del DNI español o el último dígito de una tarjeta de crédito.
- **Generador aleatorio criptográfico (`RandomNumberGenerator`):** produce números que no se pueden predecir aunque se conozcan los anteriores. `Random` no sirve: su secuencia se puede reconstruir.

## Opciones consideradas

**Generación de los 9 primeros dígitos:**

1. **Secuencial (1, 2, 3…).** Simple y sin colisiones, pero predecible: incumple RN-18. Se descarta.
2. **Secuencia + permutación secreta** (cifrado que conserva el formato). No es predecible y no tiene colisiones. En contra: criptografía propia y gestión de una clave secreta, demasiado para la fase 1.
3. **Aleatorio criptográfico + unicidad garantizada por la base.** `RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000)` da un cuerpo de 9 dígitos que no empieza por 0. Un índice único garantiza que nunca haya dos iguales. Antes de insertar se consulta si el número existe y, si existe, se genera otro (hasta 5 intentos).
   - A favor: simple, no predecible y explicable.
   - En contra: las colisiones son posibles (y crecen con el número de cuentas) y hacen falta reintentos.

**Dígito verificador:**

1. **Luhn (mod 10).** El estándar de las tarjetas de pago (ISO/IEC 7812). Detecta todos los errores de un dígito y casi todas las transposiciones de dos dígitos vecinos (no detecta 09 ↔ 90).
   - A favor: es el más conocido, se implementa en pocas líneas y en S5/S6 el frontend puede usarlo para avisar antes de enviar.
   - En contra: la debilidad de 09 ↔ 90 y de algunos "errores gemelos" (22 ↔ 55).
2. **Damm o Verhoeff.** Detectan todos los errores de un dígito y **todas** las transposiciones vecinas. En contra: menos conocidos y basados en tablas, más difíciles de explicar y de replicar en el frontend sin errores.
3. **ISO 7064 mod 97-10** (el del IBAN). Muy robusto, pero usa **dos** dígitos verificadores: no encaja en "su último dígito es verificador". Se descarta.

## Decisión

**Cuerpo aleatorio criptográfico de 9 dígitos (de 1 a 9 el primero) + dígito Luhn**, con unicidad garantizada por el índice único `ux_cuentas_numero`:

- El dominio tiene el value object `NumeroDeCuenta`: `DesdeCuerpo` calcula el verificador y `Crear` valida un número existente. La aleatoriedad está detrás del puerto `IGeneradorDeNumeroDeCuenta` (Application), implementado en Infrastructure con `RandomNumberGenerator`. El puerto existe porque CL-11 necesita forzar una colisión en las pruebas, y porque el dominio no debe depender de una fuente de aleatoriedad (igual que no depende del reloj).
- `AbrirCuentaHandler` repite hasta 5 veces "generar + consultar si existe" y usa el primer número libre.
- Si dos aperturas simultáneas sacaran el mismo número libre (probabilidad de unos 1 en 9×10⁸ por par), el índice rechaza la segunda, que se traduce en un 409 `conflicto-de-concurrencia` (ADR-0011), y el operador reintenta. Nunca hay dos cuentas con el mismo número.

El motivo principal es que es la opción más simple que cumple las tres propiedades de RN-18 (única, no predecible, con verificador). Luhn se elige por ser el algoritmo que cualquier entrevistador y cualquier frontend reconocen, y se acepta su punto ciego (09 ↔ 90).

## Consecuencias

- **Capacidad:** 9×10⁸ números posibles. Con N cuentas, la probabilidad de colisión en un intento es N / 9×10⁸: con un millón de cuentas, ~0,11 %, y cinco colisiones seguidas, ~10⁻¹⁵. Si se agotaran los intentos, sería un 500 (el espacio de números está casi lleno) que obligaría a revisar este diseño mucho antes de llegar a esa escala.
- El número no lleva información (ni sucursal ni tipo de cuenta). Si un requisito futuro lo pidiera, haría falta una ADR nueva.
- En el S5, la cuenta destino se valida con `NumeroDeCuenta` (formato + Luhn) antes de buscarla, y el frontend puede hacer la misma comprobación. Ojo: un verificador correcto no garantiza que la cuenta exista.
- Las pruebas usan vectores calculados a mano (`123456789` → `1234567897`) y un Luhn independiente escrito en la propia prueba como oráculo, para no validar el código consigo mismo.

## Decisiones del autor

_Pendiente._
