# ADR-0019: Contrato OpenAPI fuera de Development

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api, bloque 003a (RNF-06, CA-22; tareas 3 y 4 de la review de la 002)

## Contexto

ADR-0005 dice que el contrato entre `core-bancario` y `core-bancario-web` es **el documento OpenAPI de la API**: el frontend genera sus tipos a partir de él. Hoy `Program.cs` publica `/openapi/v1.json` **solo en Development**, así que fuera de ese entorno no hay contrato. Además, el documento no declara el 500 (review de la 002, puntos 3 y 4).

La 003 cambia el contrato (autenticación, 401/403, rutas nuevas) y la 004 lo consume justo después. RNF-06 pide que el contrato:

- esté disponible fuera de Development, publicado o generado y versionado;
- describa cómo autenticarse;
- documente en cada operación sus respuestas 401, 403 y 500.

Dos conceptos para leer esta ADR:

- **Generar al compilar (`Microsoft.Extensions.ApiDescription.Server`):** al hacer `dotnet build`, una herramienta arranca la aplicación sin servir peticiones, le pide el documento y lo escribe en un archivo (por ejemplo `openapi/core-bancario-api.json`). Ese archivo se versiona en Git. Analogía: imprimir el menú y pegarlo en la vitrina, en vez de que el cliente tenga que entrar a la cocina a preguntar.
- **Contrato como archivo versionado:** cada cambio del contrato aparece en el diff del pull request. Un revisor (o el autor de la 004) ve exactamente qué cambió en la API sin ejecutarla.

## Opciones consideradas

1. **Generar el documento al compilar y versionarlo** (y seguir sirviéndolo en Development).
   - A favor:
     - el contrato existe aunque la API no esté desplegada;
     - cada cambio se ve en el PR, y un paso del CI (`git diff --exit-code -- src/CoreBancario.Api/openapi`) falla si alguien cambió la API sin regenerar el archivo;
     - la 004 puede generar sus tipos desde el archivo del repositorio o de una release;
     - no expone la superficie de la API en producción.
   - En contra:
     - la compilación ejecuta `Program.cs`, que debe tolerarlo (no migrar ni exigir configuración al construir el host; la documentación propone detectar el ensamblado `GetDocument.Insider`);
     - un paquete más;
     - la compilación tarda algo más.
2. **Publicar `/openapi/v1.json` también en producción** (anónimo).
   - A favor: lo más simple (una línea); el contrato siempre coincide con lo desplegado.
   - En contra: la 004 necesita una API levantada para generar sus tipos; publica en internet el mapa completo de la API (no es un secreto, pero facilita el reconocimiento a un atacante); los cambios del contrato no se ven en el PR.
3. **Las dos cosas.**
   - A favor: lo mejor de ambas.
   - En contra: suma las desventajas de exposición de la opción 2, sin una necesidad concreta que hoy lo justifique (YAGNI).

Lo común a todas las opciones (no depende de esta decisión):

- un esquema de seguridad `Bearer` (JWT) en `components.securitySchemes`, con la descripción de los dos realms y la audiencia (ADR-0014, ADR-0015);
- el requisito de seguridad en cada operación protegida;
- `401`, `403` y `500` con `ProblemDetails` en cada operación protegida.

## Decisión

**Opción 1: generar el documento al compilar y versionarlo en el repositorio, sin exponerlo en producción** (decidido por el autor el 2026-10-13; coincide con la recomendación del planner). Cumple RNF-06 sin exponer la API, convierte el contrato en algo revisable en cada PR (la evidencia que busca el portafolio) y desacopla la 004 del despliegue.

- Paquete `Microsoft.Extensions.ApiDescription.Server`. En el `.csproj`: `<OpenApiDocumentsDirectory>openapi</OpenApiDocumentsDirectory>` y `<OpenApiGenerateDocumentsOptions>--file-name core-bancario-api</OpenApiGenerateDocumentsOptions>`.
- El archivo `src/CoreBancario.Api/openapi/core-bancario-api.json` se versiona en Git. No es un artefacto del CI.
- En el CI, tras compilar, `git diff --exit-code -- src/CoreBancario.Api/openapi` falla si alguien cambió la API sin regenerar y versionar el contrato.
- `MapOpenApi()` sigue **solo en Development**, como hasta ahora.

## Consecuencias

- Cada compilación regenera el archivo. Quien cambie un endpoint debe hacer commit también del contrato, o el CI falla.
- La prueba de CA-22 lee ese archivo y comprueba el esquema de seguridad y los 401, 403 y 500 de cada operación protegida.
- `Program.cs` debe tolerar que la herramienta de compilación lo arranque. Si la configuración o la migración de Development estorban, se aplica el patrón `GetDocument.Insider` de la documentación.
- La 004 genera sus tipos desde el archivo versionado de este repositorio.
- Si en la fase 4 (D-11) hiciera falta publicarlo, se reevalúa con una ADR nueva que reemplace a esta.

## Decisiones del autor

En sus palabras: *"Generar el openapi.json en compilación y publicarlo versionado, pero no exponer Swagger/OpenAPI públicamente en producción"*.

Antes de decidir se le explicó la diferencia entre versionar el archivo en Git (el contrato está en el repositorio y sus cambios aparecen en el diff de cada PR) y publicarlo como artefacto del CI (se genera en cada ejecución, pero no queda en el historial). Eligió versionarlo en Git.
