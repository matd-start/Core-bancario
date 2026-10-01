# ADR-0007: Regla del punto medio al ajustar Dinero a la precisión de su moneda

- Estado: Aceptada
- Fecha: 2026-10-01
- Feature: 001-dominio-dinero-cuentas (resuelve D-07 de la spec de producto)

## Contexto

RN-16 ya fija casi todo el redondeo: un monto con más decimales de los que admite su moneda se **rechaza**, y solo el **resultado de un cálculo** (en fase 1, la conversión de RF-08) se ajusta a la precisión de su moneda (COP: 0 decimales; USD: 2), **hacia el valor más cercano**, sin favorecer al banco ni al cliente. Como `Dinero` nunca es negativo (RN-15), no hay que decidir qué pasa con los negativos.

Queda un solo caso abierto: el **punto medio exacto**, cuando el resultado está justo a mitad de camino entre dos valores válidos (41.234,5 COP está igual de cerca de 41.234 que de 41.235). Ahí "el más cercano" no decide y hace falta una regla de desempate.

Datos que pesan en la decisión (el autor los comprobó con experimentos):

- Las dos reglas solo dan resultados distintos en un punto medio cuyo último dígito conservado es **par**: 41.234,5 → 41.234 o 41.235; en cambio 123.703,5 → 123.704 con las dos.
- Con montos al azar, el punto medio aparece en ~1 de cada 2.000 conversiones. Con montos **redondos**, que son los que la gente transfiere, aparece mucho más: a la tasa 4.123,45 COP/USD, 10, 30, 50, 70… USD dan siempre ,5 COP: **1 de cada 20** montos enteros de USD, y en la mitad de ellos las reglas difieren.

| USD × 4.123,45 | Resultado exacto | Al par (`ToEven`) | Hacia arriba desde la mitad (`AwayFromZero`) |
|---|---|---|---|
| 10 | 41.234,5 COP | 41.234 | 41.235 |
| 30 | 123.703,5 COP | 123.704 | 123.704 |
| 50 | 206.172,5 COP | 206.172 | 206.173 |
| (USD) | 10,125 USD | 10,12 | 10,13 |

- RF-08 exige que la vista previa del frontend use la **misma fórmula** que el backend. Lo que redondea en cada pieza del sistema, por defecto:
  - .NET: `Math.Round(decimal, int)` sin modo usa **al par** (`MidpointRounding.ToEven`).
  - JavaScript: `Math.round` y `Intl.NumberFormat` (opción `roundingMode`, valor por defecto `"halfExpand"`) redondean **hacia arriba desde la mitad**. `Intl.NumberFormat` admite `roundingMode: "halfEven"` para redondear al par.
  - PostgreSQL: `round(numeric)` desempata **alejándose de cero** (hacia arriba con montos positivos).

## Opciones consideradas

1. **Al par, o redondeo bancario (`MidpointRounding.ToEven`).** En el punto medio va al vecino cuyo último dígito es par: a veces sube y a veces baja, así que en promedio el error es cero.
   - A favor: es la única de las dos que cumple también en el punto medio el "sin favorecer a nadie" de RN-16; es el estándar IEEE 754 y el valor por defecto de .NET.
   - En contra: es menos intuitiva ("¿por qué 41.234,5 da 41.234?"); el frontend debe configurarla de forma explícita (`roundingMode: "halfEven"`) y probarla, porque los valores por defecto de JavaScript hacen lo contrario.
2. **Hacia arriba desde la mitad (`MidpointRounding.AwayFromZero`).** En el punto medio siempre sube.
   - A favor: es la regla que se enseña en el colegio y la que usan por defecto JavaScript y PostgreSQL, así que la vista previa coincide sin configurar nada.
   - En contra: en cada punto medio el que recibe el dinero gana media unidad (0,5 COP o 0,005 USD). Es poco, pero siempre en la misma dirección, y con montos redondos pasa en 1 de cada 20 conversiones: un sesgo pequeño y sistemático a favor del cliente, justo lo que RN-16 llama explotable.

## Decisión

Opción 1: **al par (`MidpointRounding.ToEven`)**. El motivo principal es que es la única de las dos reglas que mantiene en el punto medio la neutralidad que exige RN-16. Hacia arriba desde la mitad favorece siempre al que recibe, y con montos redondos el punto medio es frecuente. Las dos opciones cuestan lo mismo en el backend; el coste de "al par" está en el frontend y se controla con una configuración y una prueba.

Se descarta "hacia arriba desde la mitad" (`AwayFromZero`): coincide sin configurar con JavaScript y PostgreSQL, pero introduce un sesgo sistemático a favor del cliente en cada punto medio.

## Consecuencias

- El redondeo vive en **un solo lugar**: `Dinero.DesdeCalculo`, que pasa siempre `MidpointRounding.ToEven` de forma explícita. Nunca se llama a `Math.Round` sin modo: aunque su valor por defecto coincida hoy con esta decisión, la regla no debe depender de un valor implícito.
- No se redondea en SQL: PostgreSQL desempata alejándose de cero y podría contradecir esta ADR.
- El frontend no calcula montos con números binarios (`number` con decimales), porque fallan justo en los puntos medios (por ejemplo, 1,005 no existe en binario). Calcula con enteros en la unidad mínima o con una librería decimal, como ya pide el RNF de dinero de la spec de producto.
- Las pruebas de `Dinero.DesdeCalculo` incluyen los puntos medios de la tabla anterior, con los valores de la columna "al par". En S7 esa misma tabla se reutiliza como **vectores de prueba compartidos** entre backend y frontend para demostrar RF-08.
- El frontend usa `roundingMode: "halfEven"` en `Intl.NumberFormat` (o su equivalente en la librería decimal), porque los valores por defecto de JavaScript redondean hacia arriba desde la mitad. Una prueba del frontend lo comprueba con 41.234,5 → 41.234.

## Decisiones del autor

> Elegí redondeo al par porque evita un sesgo sistemático cuando existen muchos redondeos en el punto medio. Si siempre redondeamos hacia arriba, cada caso x,5 favorece la misma dirección. Con ToEven unas veces sube y otras baja, compensando el error acumulado a largo plazo. El costo que acepto es que el frontend debe configurar el redondeo al par (`roundingMode: "halfEven"`) y probarlo, porque JavaScript redondea hacia arriba por defecto.
