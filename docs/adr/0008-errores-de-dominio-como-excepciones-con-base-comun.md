# ADR-0008: Errores de dominio como excepciones con una clase base común

- Estado: Aceptada
- Fecha: 2026-10-01
- Feature: 001-dominio-dinero-cuentas (transversal: la usan todas las features que tocan el dominio)

## Contexto

El dominio rechaza operaciones que rompen una regla de negocio: saldo insuficiente (RN-01), monedas distintas (RN-03), cuenta Bloqueada que no admite débitos (RN-05), etc. Hay que decidir cómo se comunica ese rechazo, porque la decisión se repite en cada feature:

- Los value objects (`Dinero`) validan al crearse; si la creación falla, no debe existir un `Dinero` inválido.
- CL-13 exige que una operación rechazada no deje cambios.
- CL-12 exige que, si se rompen varias reglas, se informe solo una, en un orden fijo.
- En S2 la Api debe traducir los errores de dominio a respuestas HTTP (ADR-0002) y el frontend debe mostrarlos como mensajes de negocio, no como errores genéricos (spec de producto, sección Frontend). Eso exige distinguir "regla de negocio rota" (4xx) de "fallo del programa" (500).

## Opciones consideradas

1. **Excepciones, con una clase base abstracta común y una subclase por regla rota.**
   - A favor: una fábrica o un constructor que lanza garantiza que no exista un objeto inválido; la operación valida todo antes de modificar nada y lanza al primer fallo, lo que da CL-12 y CL-13 casi gratis; las pruebas se leen solas (`Assert.Throws<SaldoInsuficienteException>`); la Api captura la base en un solo punto.
   - En contra: la firma de un método no dice qué errores puede producir; lanzar excepciones es más lento que devolver un valor (irrelevante con este volumen); un `catch` mal puesto puede tragarse un error.
2. **Tipo `Resultado` (Result pattern): cada operación devuelve éxito o error, sin lanzar.**
   - A favor: los errores esperados quedan explícitos en la firma y el flujo es visible.
   - En contra: crear un `Dinero` también tendría que devolver `Resultado<Dinero>` y cada suma o resta obligaría a comprobarlo; más código en cada llamada; el proyecto no tiene todavía ese tipo y habría que diseñarlo (o añadir una librería, prohibido en Domain por RNF-01).
3. **Una sola excepción de dominio con un código de error.**
   - A favor: una única clase.
   - En contra: las pruebas y la Api comparan textos o códigos en vez de tipos; es fácil equivocarse de código sin que el compilador avise.

## Decisión

Opción 1. Todos los errores de dominio heredan de `ReglaDeNegocioException` (abstracta, en `CoreBancario.Domain`). Hay una subclase sellada por cada regla rota y su nombre describe la condición que encontró quien llamó (`SaldoInsuficienteException`, `MonedasDistintasException`, `TransicionNoPermitidaException`…). El comentario XML de cada una cita su RN.

Los errores de programación (un argumento `null` o un texto vacío) no son reglas de negocio: usan las excepciones estándar de .NET (`ArgumentNullException`, `ArgumentException`) y **no** heredan de la base, para que la Api los trate como fallos del programa.

El motivo principal es que una invariante protegida por un constructor o una fábrica que lanza es la forma más simple de garantizar que nunca exista un objeto inválido, y que CL-12 y CL-13 salen de forma natural validando con guard clauses antes de modificar el estado.

## Consecuencias

- En S2, un único manejador de excepciones de la Api (por ejemplo, `IExceptionHandler` de ASP.NET Core) traduce `ReglaDeNegocioException` a `ProblemDetails` con un 4xx. Allí se decide el código HTTP de cada subclase y el identificador estable que verá el frontend.
- Cada método que modifica estado valida todas sus reglas **antes** de cambiar cualquier campo. Esa disciplina es la que cumple CL-13 y la revisión la verifica.
- Nunca se captura `ReglaDeNegocioException` dentro del dominio para seguir adelante.
- Si en el futuro aparecen validaciones que deben acumular varios errores (por ejemplo, un formulario), se valoran en Application sin cambiar esta decisión para el dominio.

## Decisiones del autor

> Acepto excepciones con una clase base común. Todas las excepciones de negocio representan violaciones de reglas del dominio y comparten el mismo significado conceptual, lo que permite capturarlas de forma uniforme. Prefiero excepciones a Result en el dominio porque un error que aparece lejos de su causa es más difícil de diagnosticar, auditar y corregir, en especial con las reglas de negocio. Result lo usaría en la entrada de formularios, porque permite mostrar al operador todos los errores de una sola vez en lugar de detenerse en el primero.
