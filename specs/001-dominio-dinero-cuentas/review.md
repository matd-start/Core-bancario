# Revisión — 001-dominio-dinero-cuentas

## Veredicto: APROBADO

No encontré hallazgos obligatorios. La implementación cumple la spec, el plan y las ADR-0007 y ADR-0008. `dotnet build` termina con 0 advertencias y `dotnet test` da 95/95 en verde. La integridad de las pruebas también está bien: el commit `feat(001)` no toca `tests/`. Quedan algunas mejoras opcionales, casi todas de nombres de pruebas.

## Trazabilidad

La spec no completa ningún RF (sección 3), así que la trazabilidad va por RN, CL y CA. Llamo "indirecta" a una regla que no tiene ninguna prueba con su ID en el nombre, pero sí pruebas de un CA que la spec liga a ella (por ejemplo, "CA-10 (RN-02)") o que la sección 5 del plan le asigna.

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RF | n/a | n/a | La spec no completa ningún RF |
| RN-01 | Sí (`Cuenta.Debitar`, comprobación de saldo antes de `Restar`) | `F001_RN01_DebitarMasQueElSaldo_LanzaSaldoInsuficienteException`, CA13, CA14 | |
| RN-02 | Sí (`Cuenta.Moneda` sin setter, `Abrir`) | Indirecta: F001_CA10_*, F001_CA16_* | Ninguna prueba se llama `RN02_` |
| RN-03 | Sí (`Sumar`, `Restar`, `Acreditar`, `Debitar`) | Indirecta: F001_CA05_*, F001_CA16_*, F001_CL12_* | Ninguna prueba se llama `RN03_` |
| RN-04 | Sí (`Moneda.Precision`, comprobación `EsCero`) | `F001_RN04_MonedaCop_*`, `F001_RN04_MonedaUsd_*`, F001_CA15_* | |
| RN-05 | Sí (lista blanca de estados por operación) | Indirecta: CA17, CA18, CA19, CA27 | Ninguna prueba se llama `RN05_` |
| RN-06 | Sí (`Cerrar`) | Indirecta: CA21, CA22, CA25 | Ninguna prueba se llama `RN06_` |
| RN-10 | Sí (`Movimiento` con `{ get; }` y constructor `internal`) | Indirecta: F001_CA28_* (4 pruebas) | Ninguna prueba se llama `RN10_` |
| RN-14 | Sí (`EsTransicionPermitida` con `switch` sobre la tupla y `_ => false`) | `F001_RN14_BloquearDesbloquearYCerrarConSaldoCero_QuedaCerrada`, CA20, CA23, CA24, CA25 | |
| RN-15 | Sí (`Crear`, `Restar`, `DesdeCalculo`) | `F001_RN15_DesdeCalculoCopConResultadoNegativo_*`, `F001_RN15_DesdeCalculoNegativoQueRedondeaACero_*`, CA01, CA07 | |
| RN-16 | Sí (`Crear` rechaza, `DesdeCalculo` ajusta con ToEven) | `F001_RN16_DesdeCalculoCopEnPuntoMedioExacto_RedondeaAlPar`, `F001_RN16_DesdeCalculoUsdEnPuntoMedioExacto_RedondeaAlPar` | Usa la tabla de ADR-0007 |
| CL-01 | Sí | F001_CA01_* (2 pruebas) | La prueba `CL01_CrearConMonedaNula_*` no prueba CL-01 (ver sugerencias) |
| CL-02 | Sí | Indirecta: F001_CA03_* (10,555 USD; 1.000,50 COP) | |
| CL-03 | Sí, con forma canónica | `F001_CL03_CrearConCerosSobrantes_GuardaLaEscalaDeSuMoneda`, `F001_CL03_CrearUsdConUnDecimal_GuardaDosDecimales` | |
| CL-04 | Sí | Indirecta: F001_CA05_*, F001_CA16_* | |
| CL-05 | Sí | Indirecta: F001_CA07_RestarMasDeLoQueHay_* | |
| CL-06 | Sí | Indirecta: F001_CA15_* | |
| CL-07 | Sí (comparación `>` estricta) | Indirecta: F001_CA13_DebitarTodoElSaldo_DejaSaldoCero | |
| CL-08 | Sí | Indirecta: CA14, RN01 | |
| CL-09 | Sí | Indirecta: CA17, CA18 | |
| CL-10 | Sí | Indirecta: CA19, CA25 | |
| CL-11 | Sí | Indirecta: CA23, CA24 | |
| CL-12 | Sí (orden estado → moneda → monto > 0 → saldo) | `F001_CL12_*` (5 pruebas), CA26, CA27 | |
| CL-13 | Sí (se valida todo antes de modificar) | Indirecta: cada prueba de rechazo comprueba `Saldo` y `Estado` | Ninguna prueba se llama `CL13_`; el plan lo previó así |
| CL-14 | Sí | `F001_CL14_DesdeCalculo*PorEncimaDeLaMitad_*`, `F001_CL14_DesdeCalculo*_GuardaLaEscalaDeSuMoneda`, F001_CA09_* | |
| CA-01 … CA-16 | Sí | `CA01_` … `CA16_` | CA-08 incluye la prueba de mismo hash |
| CA-17 | Sí | `F001_CA17_AcreditarCuentaBloqueada_*` (2 pruebas) | Parte de 1 COP, no de 0 COP como dice la spec (ver desviaciones) |
| CA-18 … CA-28 | Sí | `CA18_` … `CA28_` | CA-28: prueba estructural (reflexión) y de comportamiento |
| RNF-01 | Sí (el `.csproj` de Domain no tiene `PackageReference` ni `ProjectReference`) | `F001_RNF01_EnsambladoDelDominio_SoloReferenciaElFramework` | |
| RNF-02 | Sí | Proceso | Lo comprobé ejecutando `05c98d6`: 90 en rojo y 5 en verde, justo lo que el plan anunció |
| RNF-03 | Sí, con la excepción documentada | Proceso | 2,2 s para Domain.Tests (ver sección RNF) |
| RNF-04 | Sí | `F001_RNF04_MiembrosPublicosDelDominio_NoUsanDoubleNiFloat` | |

**Integridad de las pruebas.** El commit `05c98d6 test(001): pruebas en rojo` existe. El único commit posterior, `0968eed feat(001): implementación`, solo toca `src/`, `tasks.md` y `learning.md`. `git diff 05c98d6 0968eed -- tests/` sale vacío.

**Esqueletos pendientes.** No queda ningún `// SDD: esqueleto creado por test-writer` en `src/`.

**Alcance.** Fuera de `src/` y `tests/` solo cambian documentos que el flujo exige:
- `spec-fase-1.md`: añade RN-14 a RN-16 y anota D-07 → ADR-0007.
- `roadmap.md`: RN-07 pasa a S5.
- `CLAUDE.md`: quita D-07 de la lista de pendientes.
- ADR-0007 y ADR-0008.

Application, Infrastructure y Api no cambian.

**Arquitectura.** Domain no tiene dependencias. Las 8 excepciones de regla heredan de `ReglaDeNegocioException`, y los errores de argumento usan `ArgumentException`/`ArgumentNullException` fuera de la base, como pide ADR-0008. `decimal.Round` siempre lleva `MidpointRounding.ToEven` explícito, como exige ADR-0007. No hay `double`, `float`, `DateTime.Now` ni `!` en `src/`. Sobre la skill `csharp-clean-code`: los métodos son cortos y usan guard clauses, las entidades tienen setters privados y métodos con intención, y los value objects son `sealed record` sin `init`. No hay abstracciones especulativas.

**RNF.**
- RNF-01: verificado por la prueba y por el `.csproj`.
- RNF-02: verificado ejecutando el commit en rojo.
- RNF-03: la suite de Domain tarda 2,2 s, por debajo de los 5 s. Las pruebas no tocan disco ni red. `Cuenta.Abrir` lee el reloj a través de `Guid.CreateVersion7()`, pero ninguna prueba depende de ese valor (riesgo ya documentado en la sección 10 del plan). Las pruebas también usan `Guid.NewGuid()` para `clienteId`, que es aleatorio y no afecta a ninguna aserción.
- RNF-04: verificado por la prueba.

`learning.md` existe.

## Hallazgos obligatorios

Ninguno.

## Sugerencias opcionales

1. **[tests/CoreBancario.Domain.Tests/Monetario/DineroTests.cs:181] Prefijos de ID que no corresponden a lo que se prueba.** Ocho pruebas de argumentos nulos o vacíos llevan un ID que no prueban: `CL01_CrearConMonedaNula`, `CL14_DesdeCalculoConMonedaNula`, `CA06_SumarConNulo`, `CA06_RestarConNulo`, `CA10_AbrirSinNumero`, `CA10_AbrirSinMoneda`, `CA11_AcreditarConMontoNulo` y `CA12_DebitarConMontoNulo`. CL-01 trata del monto negativo, no de la moneda nula, así que estos prefijos dan una trazabilidad falsa. Como ADR-0008 los define como errores de programación, `F001_ADR0008_…` sería un prefijo honesto. Responsable: test-writer.

2. **[tests/CoreBancario.Domain.Tests/Cuentas/CuentaOperacionesTests.cs:275 y 289] CA-17 con otros datos.** La spec dice "Bloqueada con 0 COP → acreditar 10.000 → saldo 10.000", pero la prueba parte de 1 COP y espera 10.001. El comportamiento queda cubierto. Aun así, se puede reproducir el dato literal con `Abrir` + `Bloquear`; `CuentaEstadosTests` ya tiene ese ayudante, `CuentaBloqueadaSinSaldo`. Responsable: test-writer.

3. **Pruebas `RN02_`, `RN03_`, `RN05_`, `RN06_` y `RN10_` explícitas.** Hoy esas reglas se trazan solo a través de sus CA. Basta con renombrar o añadir una prueba por regla para que `grep RN05_` encuentre algo. Responsable: test-writer.

4. **[tests/CoreBancario.Domain.Tests/ArquitecturaDelDominioTests.cs:22] La guarda de RNF-01 es permisiva.** `StartsWith("System")` dejaría pasar paquetes NuGet como `System.Reactive`. Sería más firme comprobar también el `.csproj` sin `PackageReference`, o comparar contra una lista exacta. Responsable: test-writer.

5. **[specs/001-dominio-dinero-cuentas/learning.md:52] Referencia a una prueba que no existe.** El ejercicio "Copiar" cita `RN16_DesdeCalculoEnPuntoMedioExacto_RedondeaAlPar`, pero las pruebas reales son `F001_RN16_DesdeCalculoCopEnPuntoMedioExacto_RedondeaAlPar` y `…Usd…`. Responsable: coder.

6. **[src/CoreBancario.Domain/Monetario/Dinero.cs:82] Forma canónica cerca del máximo de `decimal`.** Con montos cercanos a 7,9 × 10²⁸ USD, sumar `0.00m` no puede conservar la escala 2 y la invariante `Scale == Precision` se rompería. Ese rango queda fuera de alcance (plan, sección 10, riesgo de desbordamiento). Basta con recordarlo cuando S2 fije el máximo de entrada.

7. **[src/CoreBancario.Domain/Cuentas/Movimiento.cs:18] Orden de los ids dentro del mismo milisegundo.** `Guid.CreateVersion7(fechaHora)` no garantiza orden entre movimientos del mismo instante, porque los bits de menor peso son aleatorios. Para el extracto de S4 conviene ordenar por `FechaHora` y una columna de secuencia, no solo por `Id`. Es una nota para S4, no un defecto de hoy.

## Desviaciones entre spec/plan y código

- **CA-17:** el dato inicial de la prueba es 1 COP en vez de 0 COP (sugerencia 2).
- **Nombres de pruebas distintos de la tabla de la sección 5 del plan.** Hay divisiones sin impacto: `CA04_CrearConDecimalesValidosPorValor_SeCrea` pasó a ser `F001_CA04_CrearCopConCerosSobrantes_SeCrea` + `F001_CA04_CrearUsdConDosDecimales_SeCrea`, y `F001_RN16_…` se partió en una teoría por moneda, como pide la sección 7 del propio plan. CA15, CA17 y CA24 también están divididas. La equivalencia sí se pierde en un caso: la tabla del plan menciona `F001_RN01_DebitarMasQueElSaldo_LanzaSaldoInsuficienteException` y existe, pero usa 1 COP frente a 2 COP en lugar del caso de CA-14.
- **Comentarios XML más cortos que en el plan:** la sección 3 del plan documenta `<exception>` por método, y el código solo tiene `<summary>`. El contrato de comportamiento sí coincide.
- No encontré ninguna otra divergencia de comportamiento. El orden de validación, la lista blanca de estados, las transiciones, la forma canónica, `DesdeCalculo` (comprueba el negativo antes de redondear) y la fecha recibida por parámetro coinciden con el plan.

## Resultado de tests

- `dotnet build`: correcto, 0 advertencias y 0 errores.
- `dotnet test`: correcto. 95 en total, 95 en verde, 0 con error y 0 omitidas, en 3,9 s.
  - CoreBancario.Domain.Tests: 95 pruebas en 2,2 s.
  - Application.Tests, Infrastructure.Tests y Api.Tests: sin pruebas; siguen con `--ignore-exit-code 8`, como es de esperar.
- Commit en rojo `05c98d6`, ejecutado desde una exportación temporal fuera del repositorio: 95 en total, 90 con error (`NotImplementedException`) y 5 en verde. Las 5 son guardas estructurales (RNF-01, RNF-04 y las tres de reflexión de CA-28), tal como anunció el plan.

## Preguntas de comprensión para el autor

1. `Dinero` tiene dos fábricas, `Crear`, que rechaza el exceso de decimales, y `DesdeCalculo`, que redondea. ¿Qué error concreto de negocio evitaría esa separación si mañana alguien quisiera "simplificar" con una sola fábrica que siempre redondea? ¿Y por qué `DesdeCalculo` comprueba el signo antes de redondear y no después?
2. `Cuenta.Debitar` comprueba el saldo y lanza `SaldoInsuficienteException` aunque `Dinero.Restar` ya impediría un resultado negativo. ¿Por qué conviene tener esa "segunda defensa" en vez de dejar que salga `MontoNegativoException`? Piensa en qué vería el operador en S2 y en el orden de CL-12.
3. `Acreditar` y `Debitar` devuelven el `Movimiento` en lugar de guardarlo dentro de `Cuenta`. ¿Qué riesgo aceptaste con esa decisión, y en qué sprint y con qué tipo de prueba piensas cubrirlo?

Archivos relevantes:
- C:/Users/miguel/source/repos/core-bancario/specs/001-dominio-dinero-cuentas/spec.md
- C:/Users/miguel/source/repos/core-bancario/specs/001-dominio-dinero-cuentas/plan.md
- C:/Users/miguel/source/repos/core-bancario/specs/001-dominio-dinero-cuentas/learning.md
- C:/Users/miguel/source/repos/core-bancario/src/CoreBancario.Domain/Monetario/Dinero.cs
- C:/Users/miguel/source/repos/core-bancario/src/CoreBancario.Domain/Cuentas/Cuenta.cs
- C:/Users/miguel/source/repos/core-bancario/tests/CoreBancario.Domain.Tests/Monetario/DineroTests.cs
- C:/Users/miguel/source/repos/core-bancario/tests/CoreBancario.Domain.Tests/Cuentas/CuentaOperacionesTests.cs
- C:/Users/miguel/source/repos/core-bancario/tests/CoreBancario.Domain.Tests/ArquitecturaDelDominioTests.cs
