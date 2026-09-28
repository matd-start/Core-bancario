# Checklist de revisión C#

## Diseño
- [ ] Cada clase tiene una sola razón para cambiar (SRP).
- [ ] No hay nombres genéricos tipo `Manager`, `Helper`, `Utils`, `Processor` que escondan varias responsabilidades.
- [ ] Agregar una variante nueva (descuento, canal, proveedor) no obliga a editar un `switch` que ya creció (OCP).
- [ ] Ningún `override` lanza `NotSupportedException` ni contradice al padre (LSP).
- [ ] Las interfaces son pequeñas; nadie implementa métodos que no usa (ISP).
- [ ] La lógica de negocio no hace `new` de servicios ni conoce clases de infraestructura (DIP).
- [ ] No hay interfaces con una sola implementación que no están en una frontera (YAGNI).
- [ ] No hay abstracciones, parámetros ni configuración sin un requisito actual (YAGNI).
- [ ] La misma regla de negocio no está escrita en dos lugares (DRY).

## Dominio
- [ ] Las entidades protegen sus invariantes (setters privados, validación al crear).
- [ ] Los cambios de estado son métodos con intención (`Cancel()`, `Confirm()`).
- [ ] Conceptos como dinero, correo o cantidad son value objects, no `decimal` o `string` sueltos, cuando tienen reglas.

## Capas (si aplica Clean Architecture)
- [ ] Domain no referencia EF Core, ASP.NET Core ni paquetes de infraestructura.
- [ ] Las interfaces que implementa Infrastructure están definidas en Application.
- [ ] Controllers y endpoints solo reciben, delegan y responden.

## Legibilidad
- [ ] Métodos cortos con guard clauses, sin anidación profunda (KISS).
- [ ] Nombres completos, verbos en métodos, sufijo `Async`.
- [ ] No hay números o strings mágicos; se usan constantes, enums o value objects.

## Robustez
- [ ] `async` de punta a punta, con `CancellationToken`, sin `.Result` ni `.Wait()`.
- [ ] Ningún `catch` vacío ni `catch (Exception)` que oculte errores.
- [ ] Nullable activado y sin `!` usado para silenciar advertencias.
- [ ] Recursos `IDisposable` liberados con `using`.

## Tests
- [ ] La lógica de negocio se puede probar sin base de datos ni red.
- [ ] Los casos nuevos tienen al menos un test del camino feliz y uno del error esperado.
