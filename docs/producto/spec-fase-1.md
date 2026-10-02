# Core Bancario — Spec fase 1

Sep 28, 2026 · @Miguel

## Visión

Un core bancario simplificado en .NET con frontend en React + TypeScript, construido para defender decisiones en una entrevista: saldos consistentes bajo concurrencia, transferencias que no se duplican y un comportamiento claro cuando algo falla.

Al terminar la fase 1 debes poder explicar, con código y pruebas que lo respalden:

- Por qué el saldo nunca queda negativo aunque lleguen dos transferencias al mismo tiempo.
- Por qué reintentar una transferencia no la ejecuta dos veces.
- Cómo se convierte dinero entre COP y USD sin perder centavos ni reescribir la historia.
- Qué alternativas descartaste en cada decisión y por qué (ADRs).

Alcance de esta Spec: detalla la fase 1 (núcleo) y deja las fases 2 a 4 como hoja de ruta de una línea cada una.

## Alcance

La fase 1 cubre cuentas multimoneda, transferencias internas con idempotencia y concurrencia, y un frontend funcional para cliente y operador. Todo lo asíncrono queda para después.

**Entra en fase 1**

- Registro de clientes y apertura, bloqueo y cierre de cuentas en COP o USD.
- Depósitos y retiros de ventanilla registrados por el operador, para que las cuentas tengan saldo.
- Transferencias internas en la misma moneda y entre COP y USD con una tasa configurada por el operador.
- Extracto de movimientos paginado.
- Autenticación con roles, idempotencia y concurrencia optimista.
- Frontend React + TypeScript con área de cliente y área de backoffice.
- Logs estructurados, pruebas y CI desde el primer commit.

**Queda fuera de fase 1**

- Transferencias interbancarias, mensajería, antifraude y sagas (fase 3).
- Tasas de cambio en tiempo real de un proveedor externo.
- Intereses, préstamos, tarjetas y sobregiros.
- Asistente de normativa con RAG (proyecto aparte).

**Hoja de ruta**

1. Núcleo: cuentas, transferencias internas, multimoneda, idempotencia y concurrencia. Esta Spec.
2. Ledger de doble partida y CQRS: saldos como proyección, extractos por lotes con un background service y conciliación diaria.
3. Interbancario asíncrono: Outbox, broker, antifraude con motor de reglas (Strategy, Specification, Chain of Responsibility), Saga con compensación y Polly.
4. Endurecimiento: despliegue en nube emulada gratuita, trazas y métricas completas, pruebas de carga.

## Actores y casos de uso

Dos actores humanos operan en fase 1; el banco externo entra en fase 3 como sistema simulado.

| Actor | Casos de uso | Fase |
| --- | --- | --- |
| Cliente | Ver sus cuentas y saldos · consultar extracto · transferir a una cuenta propia o de otro cliente del banco · consultar el estado y comprobante de una transferencia | 1 |
| Operador backoffice | Registrar clientes · abrir cuentas en COP o USD · bloquear, desbloquear y cerrar cuentas · registrar depósitos y retiros de ventanilla · configurar la tasa COP/USD · consultar cualquier cuenta | 1 |
| Banco externo (sistema) | Enviar y recibir transferencias interbancarias · confirmar o rechazar una transferencia recibida | 3 |

## Lenguaje ubicuo

Estos términos se usan igual en la Spec, el código, las pruebas y el frontend; el nombre en código es la propuesta para C# y TypeScript.

| Término | Significado | En código |
| --- | --- | --- |
| Cliente | Persona titular de una o más cuentas, identificada por un documento único | `Cliente` |
| Cuenta | Depósito de dinero en una sola moneda, con número único, estado y saldo | `Cuenta` |
| Moneda | COP o USD; fija la precisión de los montos | `Moneda` |
| Dinero | Par monto + moneda; nunca un número suelto | `Dinero` |
| Movimiento | Registro inmutable de un débito o crédito sobre una cuenta, con el saldo resultante | `Movimiento` |
| Transferencia | Operación que debita una cuenta origen y acredita una cuenta destino de forma atómica | `Transferencia` |
| Transferencia entre monedas | Transferencia cuyo origen y destino tienen monedas distintas; aplica una tasa | — |
| Tasa de cambio | Valor COP por 1 USD con fecha de vigencia; cada cambio es una versión nueva | `TasaDeCambio` |
| Tasa aplicada | Copia de la tasa usada en una transferencia; no cambia después | `TasaAplicada` |
| Reverso | Movimiento que compensa otro erróneo; la única forma de corregir | `Reverso` |
| Clave de idempotencia | Identificador que envía el cliente o el operador para que un reintento no repita la operación | `IdempotencyKey` |
| Estado de cuenta | Activa, Bloqueada o Cerrada | `EstadoCuenta` |

## Modelo de dominio

Cinco agregados; la única regla de DDD que se rompe a propósito es "un agregado por transacción", porque una transferencia modifica dos `Cuenta` a la vez (D-04).

&#91;embedded content: modelo de dominio · 5 agregados y sus value objects\]

- `Cuenta` guarda el saldo y su versión. Los movimientos viven aparte para no cargar el historial en cada operación, pero se insertan en la misma transacción que actualiza el saldo.
- Los agregados se referencian por id, nunca por objeto.
- Las dos cuentas de una transferencia se actualizan siempre en el mismo orden (por id) para evitar bloqueos cruzados en la base de datos.
- La multimoneda vive en `Dinero`: una transferencia entre monedas tiene dos montos, el debitado en la moneda de origen y el acreditado en la de destino.

## Reglas de negocio

Cada regla tiene un identificador para citarla en pruebas, ADRs y commits; cada una necesita al menos una prueba que falle si se rompe.

| ID | Regla |
| --- | --- |
| RN-01 | El saldo de una cuenta nunca es negativo. No hay sobregiro en fase 1. |
| RN-02 | Cada cuenta tiene una sola moneda, fijada al abrirla y que no cambia. |
| RN-03 | Solo se suma o resta Dinero de la misma moneda; mezclar monedas sin conversión es un error de dominio. |
| RN-04 | Todo monto de una operación es mayor que cero. COP se maneja en pesos enteros y USD con 2 decimales; la regla de redondeo se fija en un ADR. |
| RN-05 | Una cuenta Bloqueada rechaza débitos y acepta créditos. Una cuenta Cerrada rechaza todo. |
| RN-06 | Una cuenta solo se cierra con saldo cero. Cerrada es un estado final. |
| RN-07 | Origen y destino de una transferencia son cuentas distintas. |
| RN-08 | Una transferencia debita y acredita ambas cuentas, o ninguna. |
| RN-09 | Una transferencia entre monedas usa la tasa vigente al ejecutarse y la guarda como tasa aplicada. |
| RN-10 | Los movimientos no se editan ni se borran; un error se corrige con un reverso. |
| RN-11 | Toda transferencia, depósito y retiro lleva clave de idempotencia, única por quien la envía (el cliente en transferencias, el operador en depósitos y retiros). La misma clave con el mismo contenido devuelve el resultado original; con otro contenido se rechaza. Dos solicitudes simultáneas con la misma clave ejecutan la operación una sola vez. |
| RN-12 | Un cliente solo usa como origen sus propias cuentas; el destino puede ser de cualquier cliente del banco. |
| RN-13 | Solo el operador cambia la tasa de cambio; cada cambio crea una versión nueva y conserva el histórico. |
| RN-14 | Una cuenta solo cambia de estado por estas transiciones: Activa → Bloqueada, Bloqueada → Activa y Activa → Cerrada. Cualquier otra se rechaza, incluida la que repite el estado actual; una cuenta Bloqueada se desbloquea antes de cerrarla. |
| RN-15 | El monto de un Dinero nunca es negativo; cero es válido. |
| RN-16 | Un monto con más decimales de los que admite su moneda se rechaza. Solo el resultado de un cálculo se ajusta a la precisión de su moneda, siempre hacia el valor más cercano, sin favorecer al banco ni al cliente; el caso del punto medio exacto lo fija el ADR de D-07. |

## Requisitos funcionales

Once requisitos cierran la fase 1; cada criterio de aceptación se convierte en una prueba de integración.

| ID | Requisito | Actor | Criterio de aceptación |
| --- | --- | --- | --- |
| RF-01 | Registrar cliente | Operador | Un documento ya registrado se rechaza. |
| RF-02 | Abrir cuenta en COP o USD | Operador | La cuenta nace Activa, con saldo 0 y número único. |
| RF-03 | Bloquear, desbloquear y cerrar cuenta | Operador | Cerrar con saldo distinto de cero se rechaza (RN-06). |
| RF-04 | Registrar depósito o retiro de ventanilla | Operador | Genera un movimiento; un retiro que deja saldo negativo se rechaza (RN-01); repetirlo con la misma clave no genera un segundo movimiento (RN-11). |
| RF-05 | Consultar cuentas y saldos | Cliente, Operador | El cliente solo ve sus cuentas; pedir una ajena responde igual que una inexistente. |
| RF-06 | Consultar extracto | Cliente, Operador | Paginado por rango de fechas, del más reciente al más antiguo, con saldo resultante en cada movimiento. |
| RF-07 | Transferir en la misma moneda | Cliente | Débito y crédito en una sola transacción; dos movimientos enlazados a la transferencia. |
| RF-08 | Transferir entre COP y USD | Cliente | Con la tasa vigente en COP por 1 USD: de USD a COP, acreditado = debitado × tasa; de COP a USD, acreditado = debitado ÷ tasa. Se redondea una sola vez, al final, en la moneda de destino según RN-04; la vista previa del frontend usa la misma fórmula y la tasa aplicada aparece en el comprobante. |
| RF-09 | Reintentar una transferencia | Cliente | Repetir la solicitud con la misma clave devuelve la misma transferencia sin nuevo débito (RN-11). |
| RF-10 | Configurar tasa COP/USD | Operador | Crea una versión con fecha de vigencia; las transferencias ya hechas conservan su tasa aplicada. |
| RF-11 | Consultar una transferencia | Cliente, Operador | Devuelve estado, montos, tasa aplicada y movimientos asociados. |

## Requisitos no funcionales

La consistencia del saldo manda sobre la velocidad: ante la duda, el sistema rechaza y deja que el cliente reintente.

| Área | Requisito | Cómo se verifica |
| --- | --- | --- |
| Concurrencia | Concurrencia optimista con versión por cuenta; ante conflicto, reintento acotado y luego 409. | Prueba con muchas transferencias simultáneas sobre la misma cuenta: saldo final exacto y nunca negativo. |
| Idempotencia | Se guardan la clave, un hash de la solicitud y la respuesta, con índice único por (emisor, clave) y en la misma transacción que la operación; la vigencia se fija en un ADR. | Pruebas de RF-09 y del rechazo por contenido distinto. |
| Dinero | Montos siempre en `decimal`, nunca `double` ni `float`; en el frontend, montos como texto o enteros en la unidad mínima. | Pruebas del value object `Dinero` y revisión de contratos de la API. |
| Seguridad | JWT con roles Cliente y Operador; autorización por recurso; validación de entrada; secretos fuera del repositorio. | Pruebas de 401 y 403; escaneo de secretos en CI. |
| Observabilidad | Logs estructurados con correlation id de punta a punta; trazas y métricas con OpenTelemetry (transferencias por estado, conflictos de concurrencia). | Una transferencia se sigue completa desde el frontend hasta la base de datos. |
| Pruebas | Unitarias de dominio, integración contra base real en contenedor, pruebas de componentes en el frontend. | Cada RN y cada RF tiene al menos una prueba; no hay meta de cobertura en porcentaje. |
| Operación | Todo levanta con un solo comando en Docker; CI en cada pull request (build, pruebas, lint). | Un clon limpio del repositorio corre en otra máquina sin pasos manuales. |
| Documentación | README con trade-offs, ADRs, diagramas C4 niveles 1 y 2, y un registro de cómo se usó la IA con SDD y qué se corrigió en revisión. | Alguien externo entiende las decisiones sin leer el código. |

## Escenarios de falla

Estos ocho escenarios son el corazón del portafolio: cada uno tiene una prueba automatizada y un párrafo en el README.

| Escenario | Comportamiento esperado | Cómo se demuestra |
| --- | --- | --- |
| Dos transferencias simultáneas intentan vaciar la misma cuenta | Solo pasa la que cabe en el saldo; la otra recibe conflicto o rechazo por saldo | Prueba de integración con tareas concurrentes |
| El cliente no recibe la respuesta y reintenta | Obtiene la misma transferencia; no hay doble débito | Prueba que descarta la primera respuesta y reenvía con la misma clave |
| Dos solicitudes con la misma clave de idempotencia llegan a la vez | Solo una se ejecuta; la otra recibe el resultado de la primera o un conflicto; nunca hay doble débito | Prueba de integración que envía ambas en paralelo |
| Misma clave de idempotencia con otro monto | Rechazo explícito; no se ejecuta nada | Prueba de API |
| Falla la base de datos entre el débito y el crédito | Rollback completo; no quedan movimientos sueltos | Prueba que fuerza una excepción dentro de la transacción |
| El operador cambia la tasa mientras se procesa una transferencia | Se usa la tasa leída dentro de la transacción y queda registrada como tasa aplicada | Prueba de integración con cambio de tasa concurrente |
| El operador bloquea la cuenta durante una transferencia | Conflicto de versión; al reintentar se ve Bloqueada y se rechaza el débito | Prueba de integración concurrente |
| Token vencido o rol incorrecto | 401 o 403, sin revelar si existe una cuenta ajena | Pruebas de API |

## Frontend

Una sola aplicación React + TypeScript con dos áreas según el rol; debe ser completa y cuidada, no una demo mínima.

| Área | Pantalla | Qué demuestra |
| --- | --- | --- |
| Común | Inicio de sesión y rutas protegidas por rol | Manejo de tokens, expiración y redirecciones |
| Cliente | Mis cuentas | Estado del servidor en caché, estados de carga, error y vacío |
| Cliente | Detalle de cuenta con extracto | Paginación, filtros por fecha, formato de moneda por locale |
| Cliente | Nueva transferencia | Formulario validado, vista previa de conversión con la tasa vigente, confirmación en dos pasos |
| Cliente | Comprobante | Lectura de la transferencia y su tasa aplicada |
| Operador | Clientes y ficha de cliente | Búsqueda, tablas, navegación entre entidades |
| Operador | Gestión de cuenta | Abrir, bloquear, desbloquear y cerrar con mensajes claros de las reglas rotas |
| Operador | Depósitos y retiros | Formularios con montos seguros y retroalimentación inmediata |
| Operador | Tasas de cambio | Histórico de versiones y alta de una nueva |

Prácticas que el frontend debe mostrar:

- TypeScript en modo estricto, con tipos generados desde el contrato OpenAPI de la API.
- La clave de idempotencia se genera al abrir el formulario de transferencia y se reutiliza en cada reintento, para que la protección funcione de punta a punta.
- Los errores de dominio de la API (saldo insuficiente, cuenta bloqueada) se muestran como mensajes de negocio, no como errores genéricos.
- Accesibilidad básica: formularios etiquetados, navegación por teclado y contraste suficiente.
- Pruebas de componentes y de los flujos de transferencia.

## Decisiones abiertas para Design

Estas decisiones no se toman en la Spec: cada una se convierte en un ADR en `docs/adr/` (numerado por orden de creación) con contexto, opciones, decisión y alternativa descartada. Resueltas: D-01 → ADR-0002, D-02 → ADR-0003 (base de datos; el mecanismo que protege el saldo se decide en el Sprint 4), D-03 → ADR-0004, D-07 → ADR-0007, D-10 → ADR-0005.

| ID | Pregunta | Opciones a comparar |
| --- | --- | --- |
| D-01 | ¿Qué estilo de arquitectura organiza el backend? | Clean Architecture · hexagonal · vertical slices |
| D-02 | ¿Qué base de datos y qué mecanismo protege el saldo? | PostgreSQL (`xmin`) · SQL Server (`rowversion`) · además, comparar la concurrencia optimista con un `UPDATE` atómico condicional (`WHERE saldo >= monto`) |
| D-03 | ¿Cómo se accede a los datos? | EF Core · Dapper · ambos por lado de lectura y escritura |
| D-04 | ¿Una transferencia puede modificar dos agregados `Cuenta` en una transacción? | Sí, en fase 1, por consistencia fuerte · consistencia eventual con eventos (se retoma en fase 3) |
| D-05 | ¿Dónde se guardan las claves de idempotencia y cuánto duran? | Tabla en la misma base · Redis |
| D-06 | ¿Cómo se despachan comandos y consultas? | MediatR en su última versión abierta (las nuevas tienen licencia comercial) · otra librería · dispatcher propio · handlers llamados directamente |
| D-07 | ¿Qué regla de redondeo en conversiones? | Al par (bancario) · hacia arriba desde la mitad |
| D-08 | ¿Quién emite los tokens? | Keycloak en Docker · la propia API · ASP.NET Core Identity |
| D-09 | ¿Cómo se construye el frontend? | Herramienta de build, manejo de estado del servidor, formularios y generación de cliente desde OpenAPI |
| D-10 | ¿Un repositorio o varios? | Monorepo backend + frontend · repositorios separados |
| D-11 | ¿Dónde se despliega sin costo? | LocalStack · otras alternativas gratuitas; verificar condiciones vigentes antes de decidir |
