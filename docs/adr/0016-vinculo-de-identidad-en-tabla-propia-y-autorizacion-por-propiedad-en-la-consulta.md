# ADR-0016: Vínculo de identidad en una tabla propia y autorización por propiedad en la consulta

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api (RN-12 en consultas; el S5 lo reutilizará para el origen de las transferencias)

## Contexto

Un cliente inicia sesión en Keycloak y la API recibe un token cuyo `sub` (identificador del usuario en Keycloak) **no es** el `ClienteId` del banco. Hay que decidir:

1. **Dónde se guarda el vínculo `sub` ↔ `ClienteId`** y quién lo escribe.
2. **Dónde vive la comprobación de propiedad** de RN-12: un cliente solo ve sus cuentas, y pedir una ajena responde **exactamente igual** que pedir una inexistente (CA-15).
3. **Qué rutas usa cada rol** (RN-19: una credencial, un rol).

Dos conceptos para leer esta ADR:

- **BOLA/IDOR (Broken Object Level Authorization, OWASP API1):** el fallo de seguridad más común en APIs. El atacante está autenticado, pero cambia un id en la URL (`/cuentas/123` → `/cuentas/124`) y la API le devuelve un objeto ajeno porque solo comprobó *quién es*, no *si es suyo*. Analogía: un casillero que se abre con cualquier llave del gimnasio, no solo con la tuya.
- **Filtrar en la consulta frente a "cargar y luego comprobar":** en la segunda forma, la API busca la cuenta por id y después compara su dueño. Funciona, pero hay dos caminos (no existe / no es tuya) que alguien debe recordar igualar en cada endpoint, y una diferencia sutil de tiempo o de mensaje revela que la cuenta existe. Filtrando en la consulta (`WHERE id = @id AND dueño = @yo`), los dos casos son **el mismo camino**: la consulta no devuelve nada.

## Opciones consideradas

**Vínculo:**

1. **Tabla propia `accesos_digitales`** (`cliente_id` PK, `sub` único) que escribe la API al habilitar el acceso. En cada petición del rol Cliente, la consulta pasa por esa tabla.
   - A favor: la base del banco decide quién es dueño de qué. El usuario no puede cambiarlo, y la integridad se apoya en la clave foránea y los índices únicos.
   - En contra: una tabla y una escritura más al habilitar (ADR-0017).
2. **Atributo `cliente_id` en el usuario de Keycloak, con un mapper que lo ponga en el token.**
   - A favor: no hay consulta extra.
   - En contra: si el atributo fuera editable desde el perfil (consola de cuenta o una configuración descuidada del perfil de usuario), **un cliente podría cambiarlo y ver cuentas ajenas**. La autorización quedaría en manos de una configuración externa.
3. **Usar el documento (`preferred_username`) para buscar al cliente en cada petición.**
   - En contra: ata la identidad a un dato de negocio y obliga a normalizar en cada petición. El `sub` es inmutable y está pensado justo para esto.

**Propiedad:**

1. **Filtrar por dueño en la consulta.**
2. **Cargar y luego comprobar.**

**Rutas:**

1. **Rutas separadas por rol:** `GET /mis-cuentas` y `GET /mis-cuentas/{id}` para el Cliente; el operador conserva `GET /cuentas/{id}` y el resto de la 002.
2. **La misma ruta `/cuentas/{id}` para los dos roles**, con un `if (rol == Cliente)` dentro del handler.

## Decisión

- **Vínculo en tabla propia** (opción 1). `AccesoDigital` (dominio) tiene `ClienteId`, `Sub` y `FechaDeVinculo`. Lo escribe la API al completar un habilitar, o al adoptar un usuario que quedó de un intento interrumpido (ADR-0017). La tabla y la lectura llegan en el bloque 003a; la escritura, con RF-13, en el bloque 003b. Hasta entonces, solo la escriben las pruebas.
- **Filtrar por dueño en la consulta.** `ICuentasDelTitular` (puerto de lectura) recibe el `sub` del token y consulta, con EF Core y `AsNoTracking`:

  ```sql
  … FROM cuentas c
  WHERE c.id = @id
    AND EXISTS (SELECT 1 FROM accesos_digitales a WHERE a.cliente_id = c.cliente_id AND a.sub = @sub)
  ```

  Una cuenta ajena y una inexistente devuelven `null`, y el handler lanza el **mismo** `RecursoNoEncontradoException`, con el mismo mensaje que el 404 del operador. CA-15 se cumple **por construcción**, no por disciplina. Nunca se carga una cuenta para comprobar después a quién pertenece.
- **Rutas separadas, cada una con un solo rol** (política `Cliente` u `Operador`, sin `if` de rol en los handlers). El `ClienteId` del cliente **nunca va en la URL**: sale del token (RN-19).
- En el realm `empleados`, el `sub` del operador **es** su identificador. Es lo que guarda el registro de accesos (ADR-0017) y lo que va al log de RNF-05. No hace falta un vínculo.

El motivo principal es que la propiedad de los datos del banco la decide la base del banco, en la misma consulta que los lee. Así un error de configuración externo o un endpoint descuidado no pueden abrir cuentas ajenas.

## Consecuencias

- Un token del rol Cliente sin vínculo (por ejemplo, un usuario creado a mano en Keycloak) ve una lista vacía y 404 en todo. No ve nada ajeno.
- El S5 aplicará la misma forma a RN-12 en las transferencias: la cuenta origen se busca con el filtro de dueño, y una cuenta ajena se comporta como inexistente.
- `ICuentasDelTitular` es un puerto aparte de `ICuentaRepositorio`. Es una lectura con otro criterio (ADR-0004) y así los fakes de la 002 no cambian.
- Hay que vigilar que ningún endpoint del rol Cliente use `ICuentaRepositorio.ObtenerAsync(id)`, que no filtra por dueño. La revisión de cada feature debe comprobarlo.
- La consulta añade un `EXISTS` por índice único (`ux_accesos_digitales_sub`): coste despreciable.

## Decisiones del autor

Vínculo en tabla propia, y no en un atributo de Keycloak con mapper. Razón del autor (revisión de la sección 10 de la spec): el riesgo concreto es que **un atributo editable del perfil permitiría a un cliente cambiarlo y ver cuentas ajenas**.

Autorización por propiedad **en la consulta**, nunca "cargar y luego comprobar", y **rutas separadas** por rol: decisiones del autor en la misma revisión, para que una cuenta ajena y una inexistente sigan el mismo camino (CA-15).

_Comprobación sugerida antes de aceptar:_ explicar con sus palabras por qué "cargar y luego comprobar" puede revelar que una cuenta ajena existe aunque responda 404.
