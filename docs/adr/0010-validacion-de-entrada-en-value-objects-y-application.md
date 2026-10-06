# ADR-0010: Validación de entrada: reglas en value objects del dominio y acumulación de errores en Application

- Estado: Aceptada
- Fecha: 2026-10-05
- Feature: 002-backoffice-clientes-cuentas (complementa a ADR-0008; no la reemplaza)

## Contexto

La 002 recibe por primera vez datos de un formulario: el registro de cliente (seis campos) y la apertura de cuenta (la moneda). Hay que cumplir a la vez:

- **CA-04 / CL-04 / RNF-03:** si varios campos son inválidos, se informan **todos a la vez** y no se guarda nada.
- **Normalización:** el número de documento (sin espacios, puntos ni guiones, en mayúsculas) y el teléfono (sin espacios, guiones ni paréntesis) se normalizan antes de validar, comparar y guardar. RN-17 depende de ello: si la normalización vive en dos sitios, la unicidad se puede saltar sin que nadie lo note (decisión del autor en la spec).
- **El formato del documento depende de su tipo** (CC/CE: dígitos de 3 a 10 sin 0 inicial; PA: alfanumérico de 5 a 15). Es una regla de **dos campos**.
- **ADR-0008:** el dominio comunica las reglas rotas con excepciones de base `ReglaDeNegocioException`. `Result` queda para "validar formularios en Application", porque acumula errores. En palabras del autor: *"Result lo usaría en la entrada de formularios, porque permite mostrar al operador todos los errores de una sola vez en lugar de detenerse en el primero"*.

**Qué hace `AddValidation()` de .NET 10** (verificado en la [documentación oficial](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/validation?view=aspnetcore-10.0) y en [What's new in ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0)):

- Valida los parámetros de los endpoints de Minimal APIs con atributos de `DataAnnotations` o con `IValidatableObject`, mediante un generador de código, y responde 400 con los errores.
- El orden es fijo: primero los atributos de cada propiedad; **si alguna propiedad falla, se omiten** la validación a nivel de tipo e `IValidatableObject`. Una regla de dos campos (documento según su tipo) escrita en `IValidatableObject` **no se informaría** junto con los errores de correo y teléfono, que es justo lo que pide CA-04.
- Solo valida las entradas HTTP. Un consumidor de mensajes de la fase 3 no pasaría por ella.
- Los atributos son otra forma de escribir reglas que ya deben vivir en el dominio (la normalización es parte de RN-17), así que se duplicarían.

Dos conceptos para leer esta ADR:

- **Value object con `TryCrear`:** es el idioma estándar de .NET para interpretar una entrada (`int.TryParse`). Devuelve `true` y el objeto si la entrada es válida, o `false` sin lanzar nada. Analogía: un control de calidad que devuelve "apto / no apto" en vez de detener la cadena de montaje.
- **Acumular frente a cortocircuitar:** una excepción detiene el flujo en el primer error. Acumular significa evaluar todos los campos y reunir los errores en una colección. Lo que permite mostrar todos los errores es **acumular**, no el tipo de retorno: acumular se puede hacer devolviendo un `Resultado` o lanzando al final una única excepción que lleve la lista.

## Opciones consideradas

1. **`AddValidation()` con DataAnnotations en los DTOs de la Api** (atributos propios que llaman al dominio).
   - A favor: integrado en el framework; el 400 sale antes de entrar al handler.
   - En contra: la regla de documento según tipo no se puede acumular con las de propiedad (se omite si otra propiedad falla), así que CA-04 no se cumple o depende de detalles internos del framework (`ValidationContext.ObjectInstance` en un atributo de propiedad); las reglas quedan en dos sitios; no valida entradas que no son HTTP; los tipos de validación de `Microsoft.Extensions.Validation` son *experimentales* en .NET 10.
2. **Reglas en value objects del dominio (`TryCrear`) y acumulación en Application, devolviendo `Resultado<T>`.** Cada handler llama a `TryCrear` de cada value object, reúne un error por campo inválido y, si hay alguno, devuelve `Resultado<T>.Invalido(errores)` sin tocar la base. Los demás errores (no encontrado, conflicto, regla rota) siguen siendo excepciones.
   - A favor: una sola fuente de verdad por regla (el value object); CA-04 se cumple por construcción; vale para cualquier entrada (HTTP hoy, mensajes mañana); aplica literalmente lo que ADR-0008 reservó para formularios; el `Resultado` en la firma deja visible que el caso de uso puede rechazar la entrada.
   - En contra: dos canales de error (el `Resultado` para datos inválidos y las excepciones para el resto); el endpoint tiene que comprobar `EsExito` (una línea).
3. **Igual que la 2, pero lanzando al final una `DatosInvalidosException` con todos los errores.**
   - A favor: un solo canal de errores; el handler devuelve el DTO directamente y el `IExceptionHandler` lo traduce todo.
   - En contra: usa una excepción para un caso esperado; se aparta de lo que el autor escribió en ADR-0008; la firma del handler no muestra que la entrada se puede rechazar.

## Decisión

**Opción 2**, con `Resultado<T>` como canal de los datos inválidos (confirmado por el autor el 2026-10-05):

- **Domain:** cada regla de formato y cada normalización vive en un value object con `TryCrear` (sin excepciones) y `Crear` (lanza `ArgumentException`: llegar a él con datos inválidos es un error de programación, como ya dice ADR-0008). Son `Documento`, `NombreDePersona`, `Correo`, `Telefono`, `NumeroDeCuenta` y `Moneda.TryDesdeCodigo`. Un dato de entrada inválido **no** es una `ReglaDeNegocioException`: no rompe una regla de un objeto que ya existe; es un dato que no llega a ser objeto.
- **Application:** los handlers evalúan **todos** los campos y devuelven `Resultado<T>`, cuyos errores son un diccionario campo → mensajes, con las claves en camelCase del contrato. Application no escribe reglas propias: solo traduce un `false` en un mensaje.
- **Api:** no valida. Traduce `Resultado.Invalido` a `ValidationProblemDetails` (400) y no usa `AddValidation()`. Los campos del request son `string?`, incluidos los que son enums, para que el model binding nunca rechace el cuerpo antes de acumular.

El motivo principal es que CA-04 exige acumular errores de reglas que dependen de varios campos y que ya pertenecen al dominio. Solo Application puede reunirlos todos sin duplicar reglas, y solo así sirve también para las entradas que no son HTTP.

## Consecuencias

- ADR-0008 sigue igual para el dominio. Esta ADR concreta la parte que ADR-0008 dejó abierta ("se valoran en Application sin cambiar esta decisión para el dominio").
- `Resultado<T>` es pequeño y propio (sin librería) y **solo** representa datos inválidos. Si mañana alguien quiere usarlo para "no encontrado" o "regla rota", hace falta otra ADR: mezclar los dos canales para el mismo tipo de error sería lo peor de ambos.
- Si el autor elige la opción 3, cambian pocas piezas: desaparece `Resultado<T>` y aparece `DatosInvalidosException` (plan de la 002, sección 10, pregunta 2). Las reglas y la acumulación quedan igual.
- Hay que vigilar que nadie añada atributos `[Required]`/`[StringLength]` en los requests "por si acaso": serían una segunda fuente de verdad y romperían la acumulación.
- `AddValidation()` se puede reconsiderar para validaciones **puramente de forma HTTP** que no sean reglas de negocio (por ejemplo, un tamaño máximo de página en el extracto del S4), siempre que no corten la acumulación.

## Decisiones del autor

El autor eligió `Resultado<T>` frente a la excepción con la lista. Es coherente con lo que escribió en ADR-0008: *"Result lo usaría en la entrada de formularios, porque permite mostrar al operador todos los errores de una sola vez en lugar de detenerse en el primero."*

Comprobación (¿por qué "cliente no encontrado" sigue siendo una excepción y no un error dentro de `Resultado<T>`?). Respuesta del autor: *"porque es una condición de negocio excepcional que impide continuar y encaja naturalmente con un 404"*. Matices añadidos en la revisión:

- Lo de "excepcional" es débil: un id mal escrito o un documento no registrado son situaciones normales (CA-16 lo prueba).
- Las razones de fondo son dos. `Resultado<T>` representa solo una entrada mal formada, y "no encontrado" habla del estado del banco con una entrada válida. Además, no hay nada que acumular: es un único error que detiene el flujo.

Por qué `Resultado<T>` y no una excepción, en palabras del autor: *"la diferencia no está en la cantidad de errores, está en el significado del fallo. La excepción comunica saldo insuficiente, cuenta no encontrada o errores técnicos; su objetivo principal es señalar"* que algo impidió continuar. Un formulario mal llenado es un resultado esperado, no una falla.

Matiz añadido en la revisión: lo que permite mostrar todos los errores a la vez es **acumular**, es decir, evaluar todos los campos con `TryCrear` antes de decidir. El tipo de retorno solo decide cómo viaja esa lista (devuelta en `Resultado` o lanzada en una excepción). Analogía: lo que permite ver todos los errores de un examen es que la profesora corrigió todas las preguntas, no el sobre en que lo devuelve.
