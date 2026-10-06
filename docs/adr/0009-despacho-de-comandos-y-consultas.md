# ADR-0009: Despacho de comandos y consultas (D-06)

- Estado: Aceptada
- Fecha: 2026-10-05
- Feature: 002-backoffice-clientes-cuentas (resuelve D-06 de la spec de producto)

## Contexto

En el S2 aparece la primera API: ocho endpoints que llaman a seis casos de uso de Application (registrar cliente, abrir cuenta, cambiar estado, tres consultas). Hay que decidir cómo llega un endpoint a su caso de uso. La decisión se repite en cada feature y, en la fase 3, también en las entradas que no son HTTP (mensajes de un broker).

Dos conceptos para leer esta ADR:

- **Handler:** la clase que ejecuta un caso de uso (por ejemplo `RegistrarClienteHandler`). Recibe un comando o una consulta (un `record` con los datos) y devuelve el resultado.
- **Mediador (patrón Mediator):** un objeto intermedio. El endpoint no conoce al handler: le entrega el comando al mediador (`mediator.Send(comando)`) y el mediador busca el handler registrado para ese tipo. La ventaja es que ofrece un **pipeline**: comportamientos que envuelven a todos los handlers (validación, log, transacción), parecidos a los middleware de ASP.NET Core pero válidos para cualquier entrada, no solo HTTP. Analogía: en lugar de llamar directamente a la extensión de una persona, llamas a la recepción, que te pasa con quien corresponde y de paso anota la llamada.

Qué pide el S2 en concreto:

- Ningún requisito transversal necesita un pipeline. La validación vive dentro del caso de uso (ADR-0010), los errores los traduce un `IExceptionHandler` (ADR-0011), cada comando es una sola transacción (un `SaveChanges`) y la observabilidad llega en el S8, con OpenTelemetry, que ya instrumenta ASP.NET Core y EF Core.
- En la fase 3, los consumidores de mensajes vivirán en una librería de mensajería (por ejemplo MassTransit), que tiene sus propios filtros de consumo.

**Licencia de MediatR (verificada el 2026-10-05):**

- **MediatR 12.5.0** (1 de abril de 2025) es la última versión con licencia **Apache 2.0**. Se puede fijar esa versión sin pagar nunca, pero esa línea ya no recibe correcciones ni parches de seguridad. [NuGet: MediatR 12.5.0](https://www.nuget.org/packages/MediatR/12.5.0)
- Desde **MediatR 13.0** (2 de julio de 2025), las versiones (hoy la 14.x; 14.2.0 del 2 de julio de 2026) tienen **licencia doble**: Reciprocal Public License 1.5 (copyleft: obliga a publicar en los mismos términos el código que la usa) o la licencia comercial de Lucky Penny Software. [Anuncio de Jimmy Bogard](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) · [LICENSE.md del repositorio](https://github.com/LuckyPennySoftware/MediatR/blob/main/LICENSE.md)
- La **licencia Community es gratuita** para empresas e individuos con menos de 5 millones de dólares de ingresos brutos anuales que no hayan recibido más de 10 millones de capital externo, para uso educativo y para entornos que no son de producción. Requiere registrarse y configurar una clave (`cfg.LicenseKey = "…"`). Sin clave, la librería funciona pero escribe avisos en el log. Los planes de pago empiezan en 799 USD al año. [mediatr.io](https://mediatr.io/)
- Para este proyecto personal, la licencia Community aplica. Aun así, la dependencia queda atada a condiciones que el autor no controla y que ya cambiaron una vez.

## Opciones consideradas

1. **Handlers llamados directamente.** Cada handler es una clase concreta con un método `EjecutarAsync(comando, ct)`, registrada en la inyección de dependencias. El endpoint la recibe por parámetro y la llama. Lo transversal de HTTP va en filtros de endpoint o en middleware.
   - A favor: navegación directa en el IDE (F12 lleva al handler); un handler que falta da error al arrancar (el contenedor no puede resolverlo), no al ejecutar; sin dependencias ni licencias; menos "magia" para alguien que está aprendiendo.
   - En contra: si en el futuro hace falta un comportamiento común a todas las entradas (HTTP y mensajes), hay que añadirlo con decoradores o adoptar entonces un mediador. Cada endpoint declara qué handler usa (una línea por endpoint).
2. **MediatR.** El estándar de facto durante años en .NET, con mucha documentación.
   - A favor: pipeline de comportamientos para cualquier entrada; patrón muy reconocible en entrevistas.
   - En contra: la 12.5.0 está congelada sin parches; la 13+ exige aceptar la RPL o registrar la licencia Community (con condiciones que pueden volver a cambiar); despacho por reflexión: un handler que falta se descubre al ejecutar, y F12 lleva a `Send`, no al handler. El autor lo encontró "abstracto y complejo" al estudiarlo.
3. **Otra librería: `Mediator` (martinothamar), MIT, con generador de código.** Misma idea que MediatR, pero el despacho se genera al compilar.
   - A favor: licencia MIT; errores al compilar en vez de al ejecutar; mejor rendimiento. [GitHub](https://github.com/martinothamar/Mediator)
   - En contra: menos conocida que MediatR; sigue siendo una capa de indirección que hoy no resuelve ningún requisito.
4. **Dispatcher propio.** Unas decenas de líneas que buscan el handler por tipo.
   - En contra: reinventa la opción 2 o la 3, con sus mismos costes de indirección y sin su documentación. Se descarta.

### Argumentos que el autor ya estudió

- El de su compañero (opción 1: errores al compilar o al arrancar, F12, menos magia) se confirma arriba.
- El a favor del mediador (un pipeline que sirve también para la mensajería de la fase 3) es real. Pierde fuerza porque las librerías de mensajería traen sus propios filtros, y porque adoptarlo más tarde es un cambio mecánico (plan de la 002, sección 8.1).
- La idea propia del autor ("las reglas del caso de uso van en Application/Domain y la forma del HTTP en los filtros, lo que le quita fuerza al mediador") **se mantiene**, con un matiz que conviene saber explicar. Al diseñar la 002 se descartó `AddValidation()` de .NET 10 (ADR-0010): no puede expresar la regla del documento, que depende de dos campos, sin cortar la acumulación de errores de CA-04, y duplicaría reglas que ya viven en los value objects. Así que en el S2 **no hay ninguna regla en la capa HTTP**. Eso refuerza la idea por otro camino: si las reglas están dentro del caso de uso, el pipeline del mediador no tiene nada que validar.
- "Abstracto y complejo" es un dato válido en un proyecto de aprendizaje: una decisión que no se puede explicar no sirve para el portafolio.

## Decisión

**Opción 1, handlers llamados directamente** (decidido por el autor el 2026-10-05; coincide con la recomendación del planner). Hoy no hay ningún requisito que necesite un pipeline. Es la opción más simple y explicable y no tiene riesgo de licencia. Si en la fase 3 aparece un comportamiento común a HTTP y mensajería que los filtros de cada entrada no cubran, se reevalúa con una ADR nueva que reemplace a esta (la opción 3 sería la primera candidata, por licencia y por errores al compilar).

## Consecuencias

Si se acepta la recomendación:

- Application no depende de ninguna librería de despacho. Los handlers son clases `sealed` con un único método público `EjecutarAsync`, y los comandos y consultas son `record`s simples.
- `AddAplicacion()` registra cada handler a mano. Olvidar uno produce un error al arrancar la Api (o en la primera prueba de API), no en producción.
- Lo transversal de HTTP (autorización en el S3, correlation id en el S8) va en middleware o filtros de endpoint.
- Hay que vigilar la tentación de meter lógica en los endpoints: deben seguir siendo una línea que traduce la petición a un comando y el resultado a HTTP.

Si el autor elige un mediador, cambia solo la forma (interfaces `IRequest`/`IRequestHandler`, el registro y la inyección de `ISender` en los endpoints); los handlers y su lógica quedan igual. Detalle en la sección 8.1 del plan de la 002.

## Decisiones del autor

> Handlers directos porque ASP.NET Core ya aporta DI, middleware y filtros; menos complejidad y fallos más tempranos. No descarto un mediador en el futuro: si el sistema creciera y aparecieran muchos orígenes de ejecución distintos, como mensajería, procesos programados o integraciones externas, volvería a evaluar la decisión.

Comprobación (fase 3, registrar en el log cada caso de uso venga de HTTP o de un mensaje). Respuesta del autor: *"mover los comportamientos transversales (log, auditoría, etc.) a una capa compartida que puedan usar tanto HTTP como los consumidores del broker"*. Matices añadidos en la revisión:

- Esa capa compartida es el patrón **Decorator**: una clase que envuelve al handler, hace lo transversal y le delega la llamada.
- Para decorar todos los handlers de una vez hace falta antes una **interfaz común** (por ejemplo `ICasoDeUso<TEntrada, TSalida>`), que hoy no existe porque los handlers son clases concretas. Introducirla es construir medio mediador, y esa es justo la señal para reevaluar esta ADR.
- "Fallos más tempranos" se refiere a que un handler sin registrar hace fallar la Api al arrancar. Con un mediador fallaría al enviar el comando.
