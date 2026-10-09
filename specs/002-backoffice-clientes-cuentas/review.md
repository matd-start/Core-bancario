# Revisión — 002-backoffice-clientes-cuentas

## Veredicto: APROBADO

La implementación cumple la spec, el plan y las ADR 0009–0013. Hay 395 pruebas en verde contra PostgreSQL real y el commit `feat` no toca ninguna prueba. No encontré nada que afecte a la corrección, a un requisito o a una ADR. Las observaciones de abajo son de nombres de pruebas y de mantener viva la documentación.

## Trazabilidad

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RF-01 | Sí (`RegistrarClienteHandler`, POST /clientes) | Vía CA-01…CA-05 | Ningún test lleva "RF01" en el nombre; el plan nombra por CA |
| RF-02 | Sí (`AbrirCuentaHandler`) | Vía CA-06…CA-09 | Ídem |
| RF-03 | Sí (`CambiarEstadoDeCuentaHandler`, 3 rutas POST) | Vía CA-10…CA-14 | Ídem |
| RF-05 (parcial) | Sí (GET /cuentas/{id}) | Vía CA-17 | Ídem |
| RF-12 | Sí (GET /clientes/{id}, /clientes/por-documento) | Vía CA-15, CA-16 | Ídem |
| RN-01 | Sí (dominio 001 + CHECK) | `F002_RN01_SaldoNegativoPorSql_LoRechazaElCheck` | |
| RN-02 | Sí (CHECK `saldo_moneda = moneda`) | `F002_RN02_SaldoEnOtraMonedaPorSql_LoRechazaElCheck` | |
| RN-06 | Sí (`Cuenta.Cerrar(fecha)`) | `F002_CA13_*` (Api, App, Domain) | `RN06_` es de la 001 |
| RN-14 | Sí (dominio 001) | `F002_CA11_*`, `F002_CA12_*` | `RN14_` es de la 001 |
| RN-17 | Sí (`Documento` + `ux_clientes_documento`) | `F002_RN17_ToString…`, `F002_CL01_*`, `F002_CL02_*`, `F002_CA02_*`, `F002_CA05_*` | |
| RN-18 | Sí (`NumeroDeCuenta` Luhn, generador criptográfico, `ux_cuentas_numero`) | `F002_RN18_*` (9), `F002_CA09_*` | |
| RNF-01…06 | Ver sección de resultados | `F002_RNF02_*`, `F002_RNF03_*`, `F002_RNF04_*`, `F002_RNF05_*` | RNF-01 y RNF-06 no tienen un test con su ID; ver nota abajo |
| CL-01 | Sí | `F002_CL01_NumeroEscritoDistinto…`, `F002_CL01_LaBaseRechaza…`, `F002_CL01_RegistrarSinConsultaPrevia…` | |
| CL-02 | Sí | `F002_CL02_*` (Domain, App, Infra) | |
| CL-03 | Sí (solo el índice, sin consulta previa) | `F002_CA05_DosContextos…`, `F002_CA05_DosRegistrosSimultaneos…` | Los `F001_CL03_*` que hay son de la 001 (Dinero) |
| CL-04 | Sí | `F002_CL04_TodosLosCamposAusentes…`, `F002_CA04_*` | |
| CL-05 | Sí | `F002_CL05_*` (12) | |
| CL-06 | Sí | `F002_CL06_*` (10) | |
| CL-07 | Sí | `F002_CL07_*` (6) | |
| CL-08 | Sí | `F002_CL08_ReintentarRegistroGuardado…` | |
| CL-09 | Sí | `F002_CL09_*` (App + FK en Infra), `F002_CA08_*` | |
| CL-10 | Sí | `F002_CL10_*` (8) | |
| CL-11 | Sí | `F002_CL11_*` (App + Api con generador falso) | |
| CL-12 | Sí (límite aceptado) | `F002_CL12_ReintentarApertura_AbreUnaSegundaCuenta` | |
| CL-13 | Sí | `F002_CA11_*`, `F002_CA12_CambiarEstadoDeCuentaCerrada…` | No hay `CL13_` |
| CL-14 | Sí | `F002_CA13_*` | Los `F001_CL14_*` que hay son de la 001 (Dinero) |
| CL-15 | Sí (xmin, sin reintento) | `F002_CL15_*` (3), `F002_CA14_*` | |
| CL-16 | Sí | `F002_CL16_*` (App, Api) | |
| CL-17 | Sí | `F002_CL17_*`, `F002_CA15_BuscarConPuntos…` | |
| CL-18 | Sí | `F002_CL18_*` (9) | |
| CL-19 | Sí | `F002_CL19_*` | |
| CA-01…CA-13, CA-15…CA-17 | Sí | Al menos un test de Api (integración) por CA | |
| CA-14 | Sí | `F002_CA14_BloquearYCerrarIntercalados…` y `F002_CA14_TrasElConflicto…` (Infra) + `F002_RNF03_ConflictoDeConcurrencia_SeClasificaComo409` (Api) | Prueba intercalada a propósito (plan, sección 7) |

**Nota sobre los nombres:** todas las reglas están probadas. Lo que falla es solo que el ID aparezca en el nombre:

- **Los RF** se cubren a través de los CA que los citan. El plan aprobado no puso IDs de RF en los nombres de las pruebas.
- **CL-03, CL-13, CL-14 y RNF-06** se cubren con pruebas de CA o con pruebas "…SinGuardar".

No lo marco como obligatorio porque no deja nada sin probar. Va en Sugerencias.

## Hallazgos obligatorios

Ninguno.

Comprobaciones hechas:
- **Pruebas en rojo:** el commit `875831b test(002): pruebas en rojo` existe y es anterior al de implementación.
- **Integridad de las pruebas:** `git show --stat f8c91a3 -- tests/` está vacío, así que el commit `feat` no cambió ninguna prueba.
- **Esqueletos:** no queda ningún `// SDD: esqueleto creado por test-writer` en `src/`. En `875831b` había esqueletos en 32 archivos y la migración no existía, que es justo el estado en rojo que pedía tasks.md.
- **Alcance:** todos los archivos de producción del diff están en las tareas T01–T21. La excepción son tres ayudantes `internal` nuevos (ver Desviaciones).
- **Arquitectura:**
  - Domain sigue sin `PackageReference` y sin `Regex`.
  - Application solo depende de `DependencyInjection.Abstractions`.
  - Infrastructure traduce `DbUpdateConcurrencyException` y el error `23505` según el nombre del índice.
  - Los endpoints son delgados y llaman al handler directamente (ADR-0009).
  - Los datos inválidos se devuelven con `Resultado<T>` y las reglas viven en los `TryCrear` de los value objects (ADR-0010).
  - Los errores salen como ProblemDetails con un `codigo` estable (ADR-0011).
  - `xmin` es una propiedad sombra y un conflicto devuelve 409 sin reintento (ADR-0012).
  - El número de cuenta usa `RandomNumberGenerator` + Luhn y se prueba hasta 5 veces (ADR-0013).
- **Invariantes de T21:**
  - No hay `!` para silenciar nulos; el único `#pragma` es el de `Cliente`, acotado y comentado.
  - Todos los `decimal.Round` llevan `MidpointRounding`.
  - `dotnet format --verify-no-changes` no reporta cambios y `dotnet build -c Release -warnaserror` da 0 advertencias.
- **learning.md:** existe (`C:\Users\miguel\source\repos\core-bancario\specs\002-backoffice-clientes-cuentas\learning.md`).

## Sugerencias opcionales

1. **Validación del número vacío distinta en registrar y en buscar.**
   - `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Application\Clientes\RegistrarClienteHandler.cs:49` usa `string.IsNullOrWhiteSpace`.
   - `BuscarClientePorDocumentoHandler` usa `TiposDeDocumentoAceptados.EstaVacioAlNormalizar`.
   - Consecuencia: con `"..."` el registro responde "no cumple el formato" y la búsqueda responde "obligatorio". Las dos respuestas son 400 con la clave `numeroDocumento`, así que el contrato no se rompe.
   - Corrección: usar el mismo ayudante en los dos sitios (es la misma regla, DRY de conocimiento).
2. **Choques de IDs con la 001.** Los CL y CA son locales a cada spec, así que `CL03_*` y `CL14_*` del proyecto Domain.Tests (Dinero, de la 001) parecen cubrir CL-03 y CL-14 de la 002 cuando no tienen nada que ver.
   - En features futuras conviene que el plan elija una de dos: añadir un prefijo de feature a los nombres de las pruebas, o declarar que la trazabilidad se lee por CA + carpeta.
   - Aplicar la misma decisión a los RF: ponerlos en los nombres, o dejar escrito en la convención que se trazan a través de sus CA.
3. **OpenAPI solo en Development.** `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Api\Program.cs:28-31` expone `/openapi/v1.json` solo en `Development`. RNF-05 se cumple (la prueba corre en Development), pero `core-bancario-web` necesitará el contrato fuera de ese entorno. Conviene decidirlo con D-11: por ejemplo, generar `openapi.json` al compilar con `Microsoft.Extensions.ApiDescription.Server` y versionarlo.
4. **El 500 no aparece en el OpenAPI.** La sección 3 dice que cualquier operación puede responder `500 error-inesperado`, pero no hay `ProducesProblem(500)`, así que el frontend no verá ese esquema en el contrato. Para la 002 es aceptable porque la prueba RNF-05 fija los códigos de la tabla de endpoints.

## Desviaciones entre spec/plan y código

1. **La hora se trunca a microsegundos.** El plan dice `reloj.GetUtcNow()` en los tres handlers que escriben. El código usa `RelojExtensiones.AhoraEnMicrosegundos()` (`C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Application\Comun\RelojExtensiones.cs`), para que la respuesta del POST coincida con lo que guarda `timestamptz`. Está justificado y explicado en learning.md. Hay que actualizar las firmas de la sección 3 del plan.
2. **La migración menciona `xmin`.** `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Infrastructure\Persistencia\Migraciones\20261006185225_Inicial.cs:43` declara `xmin = table.Column<uint>(type: "xid", rowVersion: true, …)`, y T13 y el riesgo 2 del plan decían que la migración "no crea la columna xmin". En la práctica Npgsql omite las columnas de sistema al generar el SQL. Las pruebas `F002_RNF02_*` aplican esa migración sobre un PostgreSQL vacío y pasan, así que la base no intenta crear `xmin`. Conviene corregir el texto del riesgo 2 para que diga "el C# la menciona; el SQL generado no la crea".
3. **Tres ayudantes `internal` que no estaban en el plan:** `FichaClienteDtoFabrica` (DRY entre las dos consultas de ficha), `TiposDeDocumentoAceptados` (el `switch` explícito que pedía T07, compartido con la búsqueda) y `RelojExtensiones`. No cambian ningún contrato público; basta con añadirlos a la estructura de carpetas de la sección 3.
4. **Lugar de los cambios de documentación.** T21 pedía tocar `CLAUDE.md` y `spec-fase-1.md` (D-06 → ADR-0009) y esos cambios llegaron en el commit `docs(002): plan y ADRs aprobados`, no en el de implementación. El contenido es correcto; solo cambia el commit en que entró.
5. **Datos únicos en las pruebas de integración:** sustituyen CC 1234567 por números aleatorios con las mismas transformaciones. El plan (sección 7) lo declara como desviación consciente, y la acepto.

## Resultado de tests

`dotnet build`: correcto, 0 advertencias, 0 errores. `dotnet build -c Release -warnaserror`: 0 advertencias.

`dotnet test` (Docker en marcha):

```
total: 395 · error: 0 · correcto: 395 · omitido: 0 · duración: 1m 01s
Domain.Tests 6,6 s · Application.Tests 5,3 s · Infrastructure.Tests 57,2 s · Api.Tests 58,5 s
```

Estado de cada RNF:
- **RNF-01:** cada CA tiene al menos una prueba de integración: Api para todos menos CA-14, que va en Infrastructure (como acordó el plan). Que se vieran en rojo lo confirma la estructura de `875831b` (esqueletos y sin migración). **No volví a ejecutar ese commit**, así que no lo comprobé ejecutándolo.
- **RNF-02:** verificado. El esquema se crea solo con la migración y las pruebas `F002_RNF02_BaseVacia…` y `F002_RNF02_Modelo_NoTieneCambiosSinMigracion` pasan.
- **RNF-03:** verificado por el catálogo, por la reflexión sobre las subclases de `ReglaDeNegocioException`, por la prueba de JSON mal formado y por el `codigo` que comprueba cada prueba de CA.
- **RNF-04:** verificado por `F002_RNF04_ErrorInesperado_Responde500SinDetallesInternos`.
- **RNF-05:** verificado por `F002_RNF05_ContratoOpenApi…` en Development. Fuera de Development no se puede comprobar (ver sugerencia 3).
- **RNF-06:** cada comando hace un solo `GuardarCambiosAsync`, y las pruebas "…SinGuardar", CA-08, CA-11 y CA-13 confirman que no queda nada a medias.

## Preguntas de comprensión para el autor

1. Para el documento, la unicidad se confía **solo** al índice único, sin consultar antes. Para el número de cuenta se **consulta antes** (hasta 5 veces) y además existe el índice. ¿Por qué los dos casos se resuelven distinto, si los dos son "no puede haber dos iguales"? ¿Qué pasaría en CL-03 si el registro consultara antes de insertar?
2. ADR-0012 elige `xmin` (protección de **toda la fila**) en lugar de un token sobre la columna `estado`. Explica con un ejemplo del S4 (un depósito que llega mientras alguien cierra la cuenta) qué *write skew* evita esto y qué parte de ese problema queda pendiente para D-02.
3. Un dato inválido devuelve `Resultado<T>`, pero una transición no permitida lanza una excepción, y en el dominio los value objects usan `TryCrear` en lugar de `Resultado`. ¿Qué criterio separa "dato que no llega a ser objeto" de "regla de negocio rota", y por qué eso permite informar los tres errores de CA-04 a la vez?
