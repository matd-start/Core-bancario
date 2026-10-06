# ADR-0011: Contrato de errores HTTP: ProblemDetails con un código estable

- Estado: Aceptada
- Fecha: 2026-10-05
- Feature: 002-backoffice-clientes-cuentas (concreta la consecuencia de ADR-0008 para la Api; transversal)

## Contexto

ADR-0008 dejó para el S2 dos cosas: el código HTTP de cada error de dominio y el identificador estable que verá el frontend. RNF-03 de la 002 pide distinguir cuatro casos (datos inválidos, no encontrado, conflicto y regla de negocio rota), con un identificador estable que `core-bancario-web` pueda traducir a un mensaje de negocio. RNF-04 pide no revelar detalles internos. El contrato entre los dos repositorios es el OpenAPI (ADR-0005), así que esta decisión la consumirá el frontend desde el S3 y no se puede cambiar a la ligera.

Un concepto para leer esta ADR: **ProblemDetails (RFC 9457)** es el formato estándar de errores de las APIs HTTP: un JSON con `type`, `title`, `status`, `detail` e `instance`, que admite campos extra (extensiones). ASP.NET Core lo genera de forma nativa con `AddProblemDetails()`, `IExceptionHandler` e `IProblemDetailsService`.

## Opciones consideradas

1. **ProblemDetails con una extensión `codigo`** (texto corto, estable, en kebab-case: `documento-duplicado`).
   - A favor: formato estándar que el framework ya produce; el frontend hace `switch (problema.codigo)`; los nombres se leen solos.
   - En contra: `codigo` es una convención propia encima del estándar; hay que documentarla en el OpenAPI.
2. **ProblemDetails usando `type` como identificador** (una URI por tipo de error, como sugiere el RFC).
   - A favor: es lo que el estándar propone.
   - En contra: URIs largas que habría que inventar y, en rigor, publicar; menos cómodas de comparar en el frontend; ASP.NET Core ya rellena `type` con el enlace al RFC del código HTTP.
3. **Un sobre de error propio** (`{ "error": { "code": …, "message": … } }`).
   - En contra: abandona el estándar y el soporte del framework sin ganar nada. Se descarta.

Para los códigos HTTP se consideró poner todas las reglas rotas en 400 (más simple, pero el frontend no distinguiría "datos inválidos" de "regla rota", como pide RNF-03) o en 409 (que aquí se reserva para conflictos de estado concurrente o de unicidad).

## Decisión

Opción 1, con esta tabla:

| Caso (RNF-03) | HTTP | `codigo` |
|---|---|---|
| Datos inválidos (con `errors` por campo, `ValidationProblemDetails`; también JSON mal formado) | 400 | `datos-invalidos` |
| Recurso no encontrado | 404 | `no-encontrado` |
| Conflicto: duplicado | 409 | `documento-duplicado` |
| Conflicto: cambio concurrente | 409 | `conflicto-de-concurrencia` |
| Regla de negocio rota | 422 | uno por subclase de `ReglaDeNegocioException`: `transicion-no-permitida`, `saldo-distinto-de-cero`, `operacion-no-permitida`, `saldo-insuficiente`, `monto-no-positivo`, `monedas-distintas`, `monto-negativo`, `precision-excedida` |
| Error inesperado | 500 | `error-inesperado` (sin `detail`) |

- **422 Unprocessable Content** para las reglas rotas: la petición está bien formada y sus datos son válidos, pero el estado del negocio no permite la operación (cerrar una cuenta Bloqueada). Así el frontend separa "corrige el formulario" (400) de "esto no se puede hacer ahora" (422) y de "alguien cambió algo a la vez" (409).
- La tabla vive en **un solo lugar** de la Api (`CatalogoDeErrores`), con los códigos como constantes (`CodigosDeError`). Un único `IExceptionHandler` la aplica. Para los 4xx, `detail` lleva el mensaje en español de la excepción (es un mensaje propio y nunca contiene SQL). El 500 no lleva `detail` y se registra en el log con nivel Error, porque desde .NET 10 el middleware ya no registra las excepciones que maneja un `IExceptionHandler`.
- Una prueba recorre por reflexión las subclases concretas de `ReglaDeNegocioException` y falla si alguna no tiene código propio. Así, una regla nueva del dominio no puede llegar al frontend como un código genérico sin que nadie lo note.

El motivo principal es aprovechar el estándar que el framework ya produce y añadir solo lo mínimo (un código corto) que el frontend necesita para traducir el error a un mensaje de negocio.

## Consecuencias

- Los códigos son un **contrato público**: no se renombran. Si un código deja de usarse, se marca obsoleto en el catálogo y en el README; no se reutiliza para otra cosa.
- Cada endpoint declara en el OpenAPI sus respuestas de error (`ProducesProblem`, `ProducesValidationProblem`). Una prueba compara el documento con la tabla de endpoints (RNF-05).
- Los mensajes de `detail` van en español y son para el operador. El frontend puede usarlos como texto por defecto, pero su lógica debe depender de `codigo`, nunca del texto.
- En el S3 se añadirán 401 y 403 (escenario de falla 8) con la misma forma.
- Un id que no es un GUID no llega a ningún endpoint y responde 404 sin cuerpo. Es un límite aceptado.

## Decisiones del autor

_Pendiente._
