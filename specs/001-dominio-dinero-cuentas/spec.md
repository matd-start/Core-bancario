# 001 — Dominio de dinero y cuentas

Estado: Aprobado (2026-09-29)
Fecha: 2026-09-29

## 1. Contexto y objetivo

Todo lo que viene después (backoffice, ventanilla, transferencias, multimoneda) mueve dinero entre cuentas. Esta feature construye el núcleo de dominio que lo hace seguro por construcción: `Dinero` no mezcla monedas ni queda negativo, `Cuenta` nunca queda con saldo negativo ni en un estado inválido, y cada débito o crédito deja un `Movimiento` inmutable. Es dominio puro, sin base de datos ni API: las reglas se demuestran con pruebas unitarias.

## 2. Actores

Ningún actor humano usa esta feature directamente; la consumen los casos de uso de sprints posteriores.

- Operador (a través de S2 y S4): abre, bloquea, desbloquea y cierra cuentas; registra depósitos y retiros, que acreditan y debitan.
- Cliente (a través de S5): sus transferencias debitan y acreditan cuentas.

## 3. Requisitos funcionales

Esta feature no completa ningún RF de la spec de producto: construye las capacidades de dominio que usarán RF-02 y RF-03 (S2), RF-04 (S4), RF-07 (S5) y RF-08 (S7). Su trazabilidad es por reglas de negocio (sección 5). Las capacidades son:

- Crear `Dinero` en COP o USD, sumarlo, restarlo y ajustar el resultado de un cálculo a la precisión de su moneda.
- Abrir una `Cuenta` en una moneda, con saldo cero y estado Activa.
- Acreditar y debitar una `Cuenta`; cada operación aceptada produce un `Movimiento`.
- Bloquear, desbloquear y cerrar una `Cuenta`.

## 4. Datos

| Entidad | Campo | Tipo | Obligatorio | Reglas |
|---|---|---|---|---|
| Moneda (VO) | Código | COP · USD | Sí | Solo estas dos en fase 1. |
| Moneda (VO) | Precisión | Número de decimales | Sí | COP: 0 (pesos enteros). USD: 2. |
| Dinero (VO) | Monto | Decimal | Sí | ≥ 0 (RN-15). No más decimales que la precisión de su moneda (RN-16). |
| Dinero (VO) | Moneda | Moneda | Sí | Solo opera con Dinero de la misma moneda (RN-03). |
| Cuenta (entidad) | Id | Identificador | Sí | Identidad de la cuenta; no cambia. |
| Cuenta | Número | Texto | Sí | Único en el banco; la unicidad se garantiza al persistir (S2). |
| Cuenta | ClienteId | Identificador | Sí | Referencia al titular por id, no por objeto. |
| Cuenta | Moneda | Moneda | Sí | Fijada al abrir; no cambia (RN-02). |
| Cuenta | Estado | Activa · Bloqueada · Cerrada | Sí | Nace Activa; cambia solo por RN-14; Cerrada es final (RN-06). |
| Cuenta | Saldo | Dinero | Sí | Misma moneda que la cuenta; nace en 0; nunca negativo (RN-01). |
| Movimiento (entidad) | Id | Identificador | Sí | Identidad del movimiento. |
| Movimiento | CuentaId | Identificador | Sí | Cuenta afectada. |
| Movimiento | Tipo | Débito · Crédito | Sí | |
| Movimiento | Monto | Dinero | Sí | > 0 (RN-04); moneda de la cuenta. |
| Movimiento | Saldo resultante | Dinero | Sí | Saldo de la cuenta justo después de aplicar el movimiento. |
| Movimiento | Fecha y hora | Instante | Sí | Momento en que se registró. |

Un `Movimiento` no se modifica ni se borra después de creado (RN-10).

## 5. Reglas de negocio

IDs globales de `docs/producto/spec-fase-1.md`. RN-14, RN-15 y RN-16 se añadieron allí a partir de esta entrevista.

- RN-01: El saldo de una cuenta nunca es negativo. No hay sobregiro en fase 1.
- RN-02: Cada cuenta tiene una sola moneda, fijada al abrirla y que no cambia.
- RN-03: Solo se suma o resta Dinero de la misma moneda; mezclar monedas sin conversión es un error de dominio.
- RN-04: Todo monto de una operación es mayor que cero. COP se maneja en pesos enteros y USD con 2 decimales.
- RN-05: Una cuenta Bloqueada rechaza débitos y acepta créditos. Una cuenta Cerrada rechaza todo.
- RN-06: Una cuenta solo se cierra con saldo cero. Cerrada es un estado final.
- RN-10: Los movimientos no se editan ni se borran. (El reverso queda fuera de alcance; ver sección 9.)
- RN-14: Una cuenta solo cambia de estado por Activa → Bloqueada, Bloqueada → Activa y Activa → Cerrada. Cualquier otra transición se rechaza, incluida la que repite el estado actual; una cuenta Bloqueada se desbloquea antes de cerrarla.
- RN-15: El monto de un Dinero nunca es negativo; cero es válido.
- RN-16: Un monto con más decimales de los que admite su moneda se rechaza. Solo el resultado de un cálculo se ajusta a la precisión de su moneda, siempre hacia el valor más cercano, sin favorecer al banco ni al cliente; el punto medio exacto lo fija el ADR de D-07.

## 6. Requisitos no funcionales

- RNF-01: El proyecto `CoreBancario.Domain` no referencia ningún paquete NuGet ni otro proyecto de la solución.
- RNF-02: Cada regla de la sección 5 tiene al menos una prueba unitaria que falla si la regla se rompe, y se vio en rojo antes de implementarla.
- RNF-03 (propuesto): Las pruebas de dominio no hacen E/S (ni disco, ni red, ni reloj del sistema) y la suite de `CoreBancario.Domain.Tests` termina en menos de 5 s en local.
- RNF-04 (propuesto): Ningún miembro público del dominio recibe ni devuelve montos como `double` o `float`; solo `decimal` dentro de `Dinero`.

## 7. Errores y casos límite

- CL-01: Si se crea un Dinero con monto negativo, se rechaza (RN-15).
- CL-02: Si se crea un Dinero con más decimales de los que admite su moneda (10,555 USD; 1.000,50 COP), se rechaza (RN-16).
- CL-03: La precisión se evalúa sobre el valor, no sobre cómo se escribió: 1.000,00 COP y 10,50 USD son válidos (RN-16).
- CL-04: Si se suman o restan Dinero de monedas distintas, o se acredita o debita a una cuenta un monto en otra moneda, se rechaza (RN-03).
- CL-05: Si una resta de Dinero daría negativo, se rechaza (RN-15).
- CL-06: Si se acredita o debita un monto de cero, se rechaza (RN-04).
- CL-07: Si se debita exactamente el saldo, se acepta y el saldo queda en cero.
- CL-08: Si se debita más que el saldo, se rechaza por saldo insuficiente (RN-01).
- CL-09: Una cuenta Bloqueada acepta créditos y rechaza débitos (RN-05).
- CL-10: Una cuenta Cerrada rechaza créditos, débitos y cualquier cambio de estado (RN-05, RN-06, RN-14).
- CL-11: Si se pide una transición no permitida o que repite el estado actual (bloquear una Bloqueada, desbloquear una Activa, cerrar una Bloqueada), se rechaza (RN-14).
- CL-12: Cuando una operación rompe varias reglas, se informa una sola, en este orden: primero el estado de la cuenta, luego la validez del monto (moneda y mayor que cero) y por último el saldo. Ejemplo: cerrar una Bloqueada con saldo distinto de cero informa el error de estado.
- CL-13: Una operación rechazada no deja cambios: saldo y estado quedan como estaban y no se produce ningún movimiento.
- CL-14: Al ajustar el resultado de un cálculo a la precisión de su moneda, se va al valor más cercano: 24,2515 USD → 24,25 USD; 42.595,2385 COP → 42.595 COP (RN-16).

## 8. Criterios de aceptación

**Dinero y Moneda**

- CA-01 (RN-15): Dado un monto de −1 COP, cuando se crea un Dinero, entonces se rechaza.
- CA-02 (RN-15): Dado un monto de 0 COP, cuando se crea un Dinero, entonces se crea con monto cero.
- CA-03 (RN-16): Dados 10,555 USD o 1.000,50 COP, cuando se crea un Dinero, entonces se rechaza.
- CA-04 (RN-16): Dados 1.000,00 COP o 10,50 USD, cuando se crea un Dinero, entonces se crea.
- CA-05 (RN-03): Dados 100 COP y 1,00 USD, cuando se suman o se restan, entonces se rechaza por monedas distintas.
- CA-06 (RN-03): Dados 100 COP y 50 COP, cuando se suman, entonces el resultado es 150 COP; cuando se restan, 50 COP.
- CA-07 (RN-15): Dados 50 COP y 80 COP, cuando se restan (50 − 80), entonces se rechaza.
- CA-08: Dos Dinero con el mismo monto y la misma moneda son iguales; con distinta moneda no lo son.
- CA-09 (RN-16): Dado el resultado de un cálculo de 24,2515 USD, cuando se ajusta a la precisión de su moneda, entonces queda en 24,25 USD; dado 42.595,2385 COP, queda en 42.595 COP.

**Cuenta: apertura, créditos y débitos**

- CA-10 (RN-02): Dada una moneda, cuando se abre una cuenta, entonces nace Activa, con saldo cero en esa moneda.
- CA-11 (RN-04, RN-10): Dada una cuenta Activa con 50.000 COP, cuando se acreditan 20.000 COP, entonces el saldo es 70.000 COP y se produce un Movimiento de crédito por 20.000 COP con saldo resultante 70.000 COP.
- CA-12 (RN-04, RN-10): Dada una cuenta Activa con 50.000 COP, cuando se debitan 20.000 COP, entonces el saldo es 30.000 COP y se produce un Movimiento de débito por 20.000 COP con saldo resultante 30.000 COP.
- CA-13 (RN-01): Dada una cuenta Activa con 50.000 COP, cuando se debitan 50.000 COP, entonces el saldo es 0 COP.
- CA-14 (RN-01): Dada una cuenta Activa con 50.000 COP, cuando se debitan 80.000 COP, entonces se rechaza por saldo insuficiente, el saldo sigue en 50.000 COP y no hay movimiento.
- CA-15 (RN-04): Dada una cuenta Activa, cuando se acredita o debita 0 COP, entonces se rechaza sin cambios.
- CA-16 (RN-03): Dada una cuenta en COP, cuando se acredita o debita 10,00 USD, entonces se rechaza sin cambios.

**Cuenta: estados**

- CA-17 (RN-05): Dada una cuenta Bloqueada con 0 COP, cuando se acreditan 10.000 COP, entonces el saldo es 10.000 COP y se produce el movimiento.
- CA-18 (RN-05): Dada una cuenta Bloqueada con 50.000 COP, cuando se debitan 10.000 COP, entonces se rechaza sin cambios.
- CA-19 (RN-05): Dada una cuenta Cerrada, cuando se acredita o debita cualquier monto, entonces se rechaza sin cambios.
- CA-20 (RN-14): Dada una cuenta Activa, cuando se bloquea, entonces queda Bloqueada; y dada una Bloqueada, cuando se desbloquea, queda Activa.
- CA-21 (RN-06): Dada una cuenta Activa con saldo cero, cuando se cierra, entonces queda Cerrada.
- CA-22 (RN-06): Dada una cuenta Activa con 1 COP, cuando se cierra, entonces se rechaza y sigue Activa.
- CA-23 (RN-14): Dada una cuenta Bloqueada con saldo cero, cuando se cierra, entonces se rechaza y sigue Bloqueada.
- CA-24 (RN-14): Dada una cuenta Bloqueada, cuando se bloquea; o dada una Activa, cuando se desbloquea; entonces se rechaza sin cambios.
- CA-25 (RN-06, RN-14): Dada una cuenta Cerrada, cuando se bloquea, desbloquea o cierra, entonces se rechaza y sigue Cerrada.
- CA-26 (RN-14, CL-12): Dada una cuenta Bloqueada con 10.000 COP, cuando se cierra, entonces se informa el error de estado, no el de saldo.
- CA-27 (CL-12): Dada una cuenta Bloqueada con 50.000 COP, cuando se debitan 80.000 COP, entonces se informa el error de estado, no el de saldo insuficiente.

**Movimiento**

- CA-28 (RN-10): Dado un Movimiento creado, entonces ninguno de sus datos se puede modificar después de su creación.

## 9. Fuera de alcance

- Persistencia, API, casos de uso y autorización (S2 en adelante).
- Entidad `Cliente` y generación y unicidad del número de cuenta (S2).
- RN-07, origen distinto de destino: pasa a S5 con `Transferencia`.
- Reverso de movimientos: ningún RF de fase 1 lo pide.
- Motivo e historial de cambios de estado de la cuenta.
- Idempotencia y concurrencia (S4 y S5).
- Conversión entre monedas y tasas de cambio (S7); aquí solo existe la capacidad de ajustar un resultado a la precisión de su moneda.
- Registro contable de la fracción que se pierde al redondear (ledger, fase 2).
- Sobregiro, límites de monto por operación y otras monedas además de COP y USD.

## 10. Preguntas para Design

- D-07 (ADR): regla del punto medio exacto (al par frente a hacia arriba desde la mitad). La dirección ya es neutral por RN-16. El autor debe defender la elección con sus palabras antes de cerrar la ADR.
- ¿Dónde se valida que un monto de operación sea mayor que cero (RN-04): dentro de `Debitar` y `Acreditar`, o en un tipo propio (por ejemplo `MontoDeOperacion`)?
- Nombres de las excepciones de dominio: ¿por el estado (`SaldoDistintoDeCeroException`) o por la regla rota? ¿Una clase base común?
- ¿Cómo entrega `Cuenta` el `Movimiento` que produce: lo devuelve la operación o lo acumula para que otro lo guarde? La spec de producto dice que los movimientos viven fuera de `Cuenta`.
- ¿De dónde sale la fecha y hora del `Movimiento` para que las pruebas no dependan del reloj del sistema (RNF-03)?
- Representación de `Moneda` (enumeración más precisión, o clase) y tipo de los identificadores.
- ¿Las transiciones de estado se modelan con una tabla o condicionales, o con el patrón State?

## 11. Decisiones del autor

- Incluir `Movimiento` en esta feature: RN-10 se prueba ya y S4 reutiliza el modelo.
- Mover RN-07 a S5: YAGNI; sin `Transferencia` habría que inventar una estructura provisional solo para probarla.
- `Dinero` nunca negativo (RN-15): la invariante vive en el propio tipo y `Cuenta` añade una segunda defensa con el error de saldo insuficiente.
- Rechazar montos con más decimales de los que admite su moneda (RN-16): nadie mueve dinero que no pidió; el redondeo es solo para resultados de cálculos.
- Una cuenta Bloqueada no se cierra sin desbloquearla antes (RN-14): el bloqueo es un control (embargo, sospecha de fraude) y quitarlo exige un paso explícito.
- Una transición repetida es un error explícito (RN-14): avisa al operador de que el estado no era el que creía.
- Sin motivo ni historial de cambios de estado en fase 1: ningún RF lo pide; la trazabilidad llega con logs (S8) y ledger (fase 2).
- Reverso fuera de alcance: ningún RF de fase 1 lo ejercita.
- Fracción de redondeo neutral, al valor más cercano (RN-16): favorecer al banco es un sesgo sistemático contra el cliente (*salami slicing*) y favorecer al cliente es explotable.
- Si una operación rompe varias reglas, se informa primero el estado (CL-12): orden fijo, predecible y fácil de probar.
