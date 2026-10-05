# 002 — Backoffice: clientes y cuentas

Estado: Aprobado (2026-10-05)
Fecha: 2026-10-05

## 1. Contexto y objetivo

La 001 dejó un dominio que protege el dinero, pero sin forma de usarlo: no hay clientes, nada se guarda y no hay API. Esta feature le da al operador de backoffice lo mínimo para operar el banco: registrar clientes, abrir cuentas en COP o USD, bloquearlas, desbloquearlas y cerrarlas, y consultarlas. Todo queda persistido y probado contra una base de datos real. Es la base sobre la que el S4 registrará depósitos y retiros.

## 2. Actores

- Operador de backoffice: registra clientes, abre cuentas, cambia su estado y consulta clientes y cuentas.
- Cliente: no usa esta feature directamente. Su acceso a sus cuentas, con autenticación y roles, llega en el S3 (RF-05).

En el S2 la API no tiene autenticación (D-08 se resuelve en el S3); por eso no se registra qué operador hizo cada operación.

## 3. Requisitos funcionales

IDs globales de `docs/producto/spec-fase-1.md`. RF-12 se añadió allí a partir de esta entrevista.

- RF-01: Registrar cliente. El operador registra un cliente con su documento, nombres, apellidos, correo y teléfono. Un documento ya registrado se rechaza (RN-17).
- RF-02: Abrir cuenta en COP o USD para un cliente registrado. La cuenta nace Activa, con saldo 0 y un número que asigna el banco (RN-18).
- RF-03: Bloquear, desbloquear y cerrar una cuenta, según las transiciones de RN-14. Cerrar con saldo distinto de cero se rechaza (RN-06).
- RF-12: Consultar clientes. Buscar un cliente por tipo y número de documento y ver su ficha con sus cuentas.
- RF-05 (parcial, solo el lado del operador): consultar una cuenta por su identificador. La parte del cliente ("solo ve sus cuentas") y los roles quedan para el S3.

## 4. Datos

| Entidad | Campo | Tipo | Obligatorio | Reglas |
|---|---|---|---|---|
| Cliente | Id | Identificador | Sí | Lo asigna el sistema; no cambia. |
| Cliente | Tipo de documento | CC · CE · PA | Sí | Cédula de ciudadanía, cédula de extranjería o pasaporte. Solo personas naturales. |
| Cliente | Número de documento | Texto | Sí | Se normaliza antes de validar, comparar y guardar: sin espacios, puntos ni guiones y en mayúsculas (RN-17). CC y CE: solo dígitos, entre 3 y 10, sin empezar por 0 (propuesto). PA: letras y dígitos, entre 5 y 15 (propuesto). |
| Cliente | Nombres | Texto | Sí | 1 a 100 caracteres después de quitar espacios al inicio y al final (propuesto). Letras (incluidas tildes y ñ), espacios, apóstrofo y guion. |
| Cliente | Apellidos | Texto | Sí | Mismas reglas que Nombres. Un cliente con un solo apellido es válido. |
| Cliente | Correo | Texto | Sí | Forma `usuario@dominio`, máximo 254 caracteres, sin espacios al inicio y al final. **No es único**: dos clientes pueden compartirlo. |
| Cliente | Teléfono | Texto | Sí | Formato internacional E.164: `+` y entre 8 y 15 dígitos, con el código de país. Antes de validar se quitan espacios, guiones y paréntesis. |
| Cliente | Fecha de registro | Instante | Sí | La pone el sistema al registrar; no cambia. |
| Cuenta | Número | Texto de 10 dígitos | Sí | Lo asigna el banco (RN-18): único, no predecible, el último dígito es verificador. No empieza por 0 (propuesto). |
| Cuenta | Fecha de apertura | Instante | Sí | La pone el sistema al abrir; no cambia. |
| Cuenta | Fecha de cierre | Instante | No | Solo existe si la cuenta está Cerrada. Se fija al cerrar y no cambia (Cerrada es final, RN-06). |

Los demás campos de `Cuenta` (Id, ClienteId, Moneda, Estado, Saldo) y sus reglas son los de la spec 001, sección 4.

Un cliente puede tener cualquier número de cuentas, en cualquier moneda y en cualquier estado.

## 5. Reglas de negocio

IDs globales de `docs/producto/spec-fase-1.md`. RN-17 y RN-18 se añadieron allí a partir de esta entrevista.

- RN-01: El saldo de una cuenta nunca es negativo.
- RN-02: Cada cuenta tiene una sola moneda, fijada al abrirla y que no cambia.
- RN-06: Una cuenta solo se cierra con saldo cero. Cerrada es un estado final.
- RN-14: Una cuenta solo cambia de estado por Activa → Bloqueada, Bloqueada → Activa y Activa → Cerrada. Cualquier otra transición se rechaza, incluida la que repite el estado actual; una cuenta Bloqueada se desbloquea antes de cerrarla.
- RN-17: Un cliente se identifica por su documento: tipo (CC, CE o PA) y número. No pueden existir dos clientes con el mismo tipo y número. El número se compara después de quitarle espacios, puntos y guiones y de pasarlo a mayúsculas, y el mismo número con otro tipo es otro documento.
- RN-18: El número de cuenta lo asigna el banco al abrirla y no cambia nunca: es único, tiene 10 dígitos, no sigue un orden que permita deducir otros números y su último dígito es verificador.

## 6. Requisitos no funcionales

- RNF-01: Cada criterio de aceptación de la sección 8 tiene al menos una prueba de integración contra PostgreSQL real en contenedor, y se vio en rojo antes de implementarla.
- RNF-02: El esquema de la base se crea solo con migraciones. Una base vacía queda lista aplicando todas las migraciones, sin pasos manuales, y las pruebas usan esas mismas migraciones.
- RNF-03: Las respuestas de error distinguen cuatro casos: datos inválidos, recurso no encontrado, conflicto (duplicado o cambio concurrente) y regla de negocio rota. Cada tipo de error lleva un identificador estable que el frontend puede traducir a un mensaje de negocio. En los datos inválidos se informan **todos** los campos con error a la vez (ADR-0008).
- RNF-04 (propuesto): Ninguna respuesta de error revela detalles internos (traza de pila, SQL, nombres de tablas). Un error inesperado responde un error genérico.
- RNF-05: El contrato OpenAPI describe cada operación, sus cuerpos de entrada y salida y sus respuestas de error. Es el contrato con `core-bancario-web` (ADR-0005).
- RNF-06 (propuesto): Ninguna operación deja datos a medias. Si falla, no queda ni el cliente ni la cuenta ni el cambio de estado.

## 7. Errores y casos límite

**Registrar cliente**

- CL-01: Si el tipo y el número normalizado ya existen, se rechaza por duplicado. `1.234.567`, ` 1234567 ` y `1234567` son el mismo número (RN-17).
- CL-02: El mismo número con otro tipo (CC 1234567 y PA 1234567) se acepta: son documentos distintos (RN-17).
- CL-03: Si dos operadores registran el mismo documento a la vez, se registra uno solo. El otro recibe el **mismo** rechazo por duplicado que en CL-01, no un error inesperado.
- CL-04: Si varios campos son inválidos, se informan todos a la vez y no se guarda nada.
- CL-05: Un número de documento que no cumple el formato de su tipo se rechaza: una CC con letras, un pasaporte con símbolos, una longitud fuera de rango o una CC que empieza por 0.
- CL-06: Nombres o apellidos vacíos o con solo espacios se rechazan.
- CL-07: Un teléfono sin código de país (`3001234567`) se rechaza.
- CL-08: Si el operador reintenta un registro que sí se guardó (no recibió la respuesta), recibe el rechazo por duplicado. Encuentra al cliente buscándolo por documento (RF-12).

**Abrir cuenta**

- CL-09: Abrir una cuenta para un cliente que no existe responde no encontrado y no se crea nada.
- CL-10: Una moneda distinta de COP o USD se rechaza como dato inválido.
- CL-11: Si el número generado ya existe, el sistema genera otro sin que el operador lo note. Nunca existen dos cuentas con el mismo número (RN-18).
- CL-12: Si el operador reintenta una apertura que sí se guardó, se abre una segunda cuenta. Es un **límite conocido y aceptado**: la cuenta sobrante está vacía y se puede cerrar (RN-06). La idempotencia se construye en el S4 (RN-11, D-05).

**Cambiar estado**

- CL-13: Una transición que RN-14 no permite (bloquear una Bloqueada, cerrar una Bloqueada, cualquier cambio sobre una Cerrada) se rechaza como regla de negocio rota. El estado no cambia.
- CL-14: Cerrar una cuenta con saldo distinto de cero se rechaza (RN-06) y la cuenta sigue Activa.
- CL-15: Si dos operadores cambian el estado de la misma cuenta a la vez, se aplica uno solo. El otro recibe un conflicto y **ninguna operación se pierde en silencio**. Si reintenta, RN-14 se evalúa sobre el estado nuevo.
- CL-16: Cambiar el estado de una cuenta que no existe responde no encontrado.

**Consultar**

- CL-17: La búsqueda por documento normaliza el número igual que el registro: `1.234.567` encuentra a `1234567`.
- CL-18: Buscar un documento no registrado, o consultar un cliente o una cuenta que no existen, responde no encontrado.
- CL-19: Un cliente sin cuentas se consulta con su lista de cuentas vacía.

## 8. Criterios de aceptación

**RF-01 Registrar cliente**

- CA-01 (RF-01): Dado un documento no registrado, cuando el operador registra un cliente con datos válidos, entonces el cliente queda guardado con id, fecha de registro y el número de documento y el teléfono normalizados.
- CA-02 (RF-01, RN-17, CL-01): Dado un cliente con CC 1234567, cuando se registra otro con CC `1.234.567`, entonces se rechaza por duplicado y sigue existiendo un solo cliente con ese documento.
- CA-03 (RN-17, CL-02): Dado un cliente con CC 1234567, cuando se registra otro con PA 1234567, entonces el registro se acepta.
- CA-04 (RF-01, CL-04…CL-07): Dado un registro con documento, correo y teléfono inválidos, cuando el operador lo envía, entonces la respuesta informa los tres errores a la vez y no se guarda ningún cliente.
- CA-05 (RN-17, CL-03): Dadas dos solicitudes simultáneas con el mismo documento, cuando se procesan, entonces existe exactamente un cliente con ese documento y la otra solicitud recibe el rechazo por duplicado.

**RF-02 Abrir cuenta**

- CA-06 (RF-02, RN-18): Dado un cliente registrado, cuando el operador abre una cuenta en COP, entonces la cuenta queda Activa, con saldo 0 COP, fecha de apertura, sin fecha de cierre y con un número de 10 dígitos cuyo dígito verificador es correcto.
- CA-07 (RF-02, RN-02): Dado un cliente registrado, cuando el operador abre una cuenta en USD, entonces la cuenta queda en USD con saldo 0 USD.
- CA-08 (RF-02, CL-09): Dado un id de cliente que no existe, cuando se intenta abrir una cuenta, entonces responde no encontrado y no se crea ninguna cuenta.
- CA-09 (RN-18, CL-11): Dado un cliente registrado, cuando se abren 50 cuentas (propuesto), entonces los 50 números son distintos, todos tienen dígito verificador correcto y no son consecutivos.

**RF-03 Cambiar estado**

- CA-10 (RF-03, RN-14): Dada una cuenta Activa, cuando el operador la bloquea, queda Bloqueada; y cuando la desbloquea, vuelve a Activa.
- CA-11 (RF-03, RN-14, CL-13): Dada una cuenta Bloqueada, cuando el operador intenta cerrarla o bloquearla de nuevo, entonces se rechaza como regla de negocio rota y sigue Bloqueada.
- CA-12 (RF-03, RN-06): Dada una cuenta Activa con saldo 0, cuando el operador la cierra, entonces queda Cerrada con fecha de cierre; y cualquier cambio de estado posterior se rechaza.
- CA-13 (RF-03, RN-06, CL-14): Dada una cuenta Activa con saldo mayor que cero, cuando el operador intenta cerrarla, entonces se rechaza y sigue Activa sin fecha de cierre.
- CA-14 (RF-03, CL-15): Dada una cuenta Activa, cuando dos operadores la bloquean y la cierran a la vez, entonces se aplica una sola operación, la otra recibe un conflicto y el estado guardado coincide con la operación aplicada.

**RF-12 y RF-05 Consultar**

- CA-15 (RF-12, CL-17): Dado un cliente con CC 1234567, cuando el operador busca CC `1.234.567`, entonces obtiene su ficha con sus cuentas (número, moneda, estado y saldo).
- CA-16 (RF-12, CL-18, CL-19): Dado un documento no registrado, cuando el operador lo busca, entonces responde no encontrado; y un cliente sin cuentas se devuelve con la lista vacía.
- CA-17 (RF-05, CL-18): Dada una cuenta existente, cuando el operador la consulta por su id, entonces obtiene número, moneda, estado, saldo y fechas; y una cuenta inexistente responde no encontrado.

## 9. Fuera de alcance

- Editar o borrar los datos de un cliente. Ningún RF de la fase 1 lo pide; queda como mejora futura.
- Personas jurídicas (NIT) y otros tipos de documento.
- Mayoría de edad y fecha de nacimiento.
- Unicidad del correo: el login del S3 usará el documento.
- Listado paginado de clientes y búsqueda por nombre.
- Idempotencia al registrar clientes y abrir cuentas (ver CL-08 y CL-12).
- Autenticación, roles y registro de qué operador hizo cada operación (S3).
- Historial de cambios de estado y su motivo (igual que en la 001). Solo se guarda la fecha de cierre.
- Depósitos, retiros y cualquier movimiento de dinero (S4).
- Límite de cuentas por cliente.
- Observabilidad (logs estructurados, correlation id, métricas): S8.
- Rendimiento medido con carga: fase 4.

## 10. Preguntas para Design

- **D-06 (ADR):** mediador o handlers llamados directamente con filtros. Antes de la ADR, verificar la licencia vigente de MediatR. Los argumentos de las dos partes ya están estudiados (ver nota de contexto del proyecto).
- **Validación de datos (RNF-03, CL-04):** ¿`AddValidation()` de .NET 10 con `Resultado<T>` en Application, o excepciones que heredan de `ReglaDeNegocioException`? Si cambia el criterio, actualizar ADR-0008.
- **Unicidad del documento (CL-03):** índice único en la base y traducción de su violación (`23505`) al mismo error de duplicado. Si no, quien pierde la carrera recibe un 500.
- **Conflicto en los cambios de estado (CL-15):** ¿qué mecanismo (versión, `xmin`) y qué relación tiene con D-02 del S4? La RNF de concurrencia de la spec de producto pide "reintento acotado y luego 409": ¿se reintenta automáticamente un cambio de estado o se devuelve el conflicto directo?
- **Número de cuenta (RN-18, CL-11):** algoritmo de generación (aleatorio con reintento ante colisión u otro), algoritmo del dígito verificador (Luhn u otro) y dónde se valida. Hoy `Cuenta.Abrir` solo exige que no esté vacío: ¿value object `NumeroDeCuenta` en el dominio?
- **Cliente en el dominio:** entidad `Cliente` y value objects (`Documento`, `Correo`, `Telefono`?). ¿Dónde vive la normalización?
- **Fecha de cierre:** `Cuenta.Cerrar` tendrá que recibir la hora como parámetro, igual que `Acreditar` y `Debitar`. Es un cambio al dominio de la 001.
- **Persistencia:** moneda duplicada (`Moneda` y `Saldo_Moneda`), escala del monto (`numeric` frente a `numeric(19,2)` y forma canónica de `Dinero`), `CHECK (saldo >= 0)` y nombres en snake_case.
- **Pruebas de integración:** un contenedor para todo el proyecto (`AssemblyFixture`), estrategia de aislamiento y migraciones reales (`Migrate()`). ¿Cómo se prepara una cuenta con saldo para CA-13 si todavía no existe RF-04? Al añadir la primera prueba a cada proyecto de tests, quitar su `--ignore-exit-code 8`.
- **API:** código HTTP de cada tipo de error de RNF-03 y su identificador estable; ¿las rutas usan el id o el número de cuenta?

## 11. Decisiones del autor

Entrevista del 2026-10-05.

- **Datos del cliente:** documento, nombres y apellidos por separado, correo y teléfono, todos obligatorios. Eligió incluir el contacto aunque la recomendación era dejarlo fuera. En sus palabras: *"forman parte de la información mínima de contacto del cliente y facilitan notificaciones, validaciones de identidad y comunicaciones operativas. Son datos propios del cliente y no simples detalles de infraestructura o presentación"*.
- **Tipos de documento:** CC, CE y pasaporte, solo personas naturales.
- **Unicidad:** por tipo y número. El mismo número con otro tipo es otro documento, como en los bancos colombianos.
- **Normalizar en lugar de rechazar** el número de documento: si se guardaran `1.234.567` y `1234567` como textos distintos, la unicidad se saltaría sin que nadie lo notara.
- **Correo no único:** el login del S3 usará el documento, así que el correo es solo un dato de contacto (una familia puede compartirlo).
- **Teléfono en E.164:** sirve para clientes extranjeros (CE y pasaporte) con celular de otro país.
- **Número de cuenta generado por el banco, no predecible y con dígito verificador:** evita la enumeración y prepara el S5 para detectar errores de tipeo en la cuenta destino.
- **Sin límite de cuentas por cliente**, como en un banco real.
- **Conflicto en los cambios de estado concurrentes:** nada se pierde en silencio. Además deja lista la protección que el S4 necesita para no cerrar nunca una cuenta con saldo. En sus palabras: *"evita que una operación sobrescriba el resultado de otra usando información desactualizada; la segunda recibe un error de concurrencia y debe volver a cargar la cuenta antes de continuar"*. Matiz añadido en la revisión: al recargar se **vuelve a evaluar la regla** (RN-06) sobre datos frescos, y la protección debe ser por fila, no por columna, porque un cierre y un depósito escriben columnas distintas (*write skew*).
- **Se acepta la cuenta duplicada por reintento:** no mueve dinero, se puede cerrar y la idempotencia se construye bien en el S4.
- **Consultas mínimas:** por id y búsqueda por documento, que además resuelve el reintento del registro.
- **Editar contacto queda fuera de alcance.**
- **Trazabilidad:** fechas de registro, apertura y cierre. Eligió añadir la fecha de cierre aunque la recomendación era guardar solo las fechas de registro y apertura. En sus palabras: *"mantener trazabilidad explícita del ciclo de vida de la cuenta; permite saber cuándo una cuenta pasó al estado Cerrada y facilita auditorías, reportes históricos y análisis"*.
