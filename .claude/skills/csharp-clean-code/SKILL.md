---
name: csharp-clean-code
description: Principios de diseño para escribir y revisar código C#/.NET — SOLID, KISS, DRY, YAGNI, Clean Architecture y nombres claros. Usar al crear o refactorizar clases, servicios, handlers, entidades o endpoints en C#, o cuando el usuario pida revisar calidad, "clean code", SOLID o buenas prácticas.
---

# Código C# limpio

## Cuando los principios chocan, este es el orden

1. **Correcto y legible** antes que elegante.
2. **KISS**: la solución más simple que resuelve el problema de hoy.
3. **YAGNI**: no agregar abstracciones, parámetros ni capas "por si acaso".
4. **SOLID**: aplicarlo cuando hay un motivo real (varias implementaciones, testabilidad, un cambio que ya se ve venir). No como ritual.
5. **DRY**: duplicar dos veces está bien; abstraer a la tercera, y solo si es el mismo *conocimiento*, no código que se parece por casualidad.

## Al escribir código

**Clases y métodos**
- Una responsabilidad por clase. Si el nombre pide `And`, `Manager`, `Helper` o `Utils`, probablemente hay que dividirla.
- Métodos cortos, con un solo nivel de abstracción. Usar guard clauses y early return en vez de `if` anidados.
- Composición sobre herencia. Heredar solo si el hijo puede reemplazar al padre sin sorpresas (LSP).

**Dependencias**
- Inyectar por constructor (los primary constructors de C# 12 están bien).
- Crear una interfaz solo en fronteras (base de datos, mensajería, APIs externas, reloj) o cuando hay varias implementaciones. No crear `IFoo` para cada clase `Foo`.
- Nunca hacer `new` de servicios dentro de la lógica de negocio.

**Dominio**
- Entidades que protegen sus invariantes: setters privados, constructores o métodos de fábrica que validan, métodos con intención (`order.Cancel()`, no `order.Status = 3`).
- Value objects con `record` para conceptos como `Money`, `Email`, `Quantity`.

**Errores y async**
- Excepciones para lo excepcional. Para fallos de negocio esperados, usar un tipo `Result` si el proyecto ya lo tiene. Nunca tragar excepciones con un `catch` vacío.
- `async` de punta a punta, pasar `CancellationToken`, nunca `.Result` ni `.Wait()`.

**Estilo**
- Nombres completos y con intención: verbos para métodos, sufijo `Async` en métodos asíncronos, sin abreviaturas.
- Nullable reference types activado; no usar `!` para silenciar advertencias.

## Clean Architecture (si el proyecto está en capas)

- Las dependencias apuntan hacia dentro: `Api` e `Infrastructure` → `Application` → `Domain`.
- **Domain** no referencia EF Core, ASP.NET Core ni ningún paquete de infraestructura.
- **Application** contiene los casos de uso y las interfaces (puertos) que Infrastructure implementa.
- **Infrastructure** tiene EF Core, mensajería y clientes de servicios externos.
- **Api**: controllers o endpoints delgados que solo delegan al caso de uso.

Si el proyecto tiene su propio `CLAUDE.md` o una skill de proyecto con otras convenciones, esas mandan sobre esta.

## Explicar las decisiones

El usuario está aprendiendo diseño de software. Cuando tomes una decisión de diseño que no sea obvia, explícala en una o dos frases en español nombrando el principio (por ejemplo: "Separé el envío del correo en un handler del evento `OrderPlaced` por SRP: el caso de uso no debería cambiar si cambia el canal de notificación"). No expliques lo trivial.

## Al revisar código

1. Recorre [checklist.md](checklist.md).
2. Reporta los problemas ordenados por impacto, no por orden de aparición.
3. Por cada uno: qué principio rompe, por qué importa aquí y un antes/después mínimo.
4. No reescribas todo el archivo; propone el cambio más pequeño que arregla el problema.
5. Si algo "rompe" un principio pero es la opción más simple y el cambio no se ve venir, dilo y déjalo (KISS/YAGNI ganan).

## Referencias

- [principles.md](principles.md): cada principio con un ejemplo malo y uno bueno en C#. Leerlo cuando haya dudas sobre cómo aplicar uno.
- [checklist.md](checklist.md): lista de revisión.
