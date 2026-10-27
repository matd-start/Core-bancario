# Revisión — 004-front-base, PR 1 de 004a (repo `core-bancario`, T01–T11)

## Veredicto: CAMBIOS REQUERIDOS

El trabajo está muy bien hecho: todas las pruebas pasan (1165 de 1165) y la trazabilidad es completa. La integridad de las pruebas es correcta y el código está limpio. Queda **un hallazgo de seguridad**: al iniciar sesión, la clave de la sesión no siempre se renueva (fijación de sesión). Va contra la garantía de RNF-01 y ADR-0020, y el arreglo es pequeño.

## Trazabilidad

Solo se revisan los IDs que cubren T01–T11. CA-03, CA-08, CA-11, CA-12 (navegador), CA-17…CA-20, CA-35 y CA-36 son del PR web (T12–T20) y quedan fuera.

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RF-14 | Sí (`EndpointsDelBff`, `ConfiguracionDeSesion`, `EventosDeOidc`) | `F004_RF14_*` (Bff), `F004_CA01_*`, `F004_CA02_*` (Keycloak) | |
| RF-15 | Sí (`/bff/cambiar-clave`, `kc_action`) | `F004_CA21_*` (Bff y Keycloak) | |
| RN-19 | Sí (`LectorDeRol`, dos esquemas) | `F004_CL04_LectorDeRol_*`, `F004_CA02_*` | |
| RN-21, RN-24 | Sí (Keycloak, `prompt=login`) | Recorridos de Keycloak.Tests con OTP | CA-03 es del web |
| RN-22 | Sí (`PoliticaDeSesion`, appsettings) | `F004_RN22_*`, `F004_RN22_TiemposDelBff_CoincidenConLosRealms` | |
| RN-25 | Sí (solo el latido mueve la actividad; el proxy nunca) | `F004_RN25_*` (Bff y Keycloak) | |
| RN-26 | Sí, con un fallo de seguridad | `F004_RN26_RegresoDeOtraSesion_CierraLaAnteriorEnKeycloak`, `F004_CA14_*`, `F004_CL10_*` | Ver hallazgo 1 |
| RNF-01 | Sí, salvo el hallazgo 1 | `F004_RNF01_*` (15 pruebas), `F004_CA05_*` | |
| RNF-02 | Sí (`ProteccionCsrf`, SameSite Strict, solo POST) | `F004_CA06_*`, `F004_RNF02_*` | Lo real entre orígenes es del web |
| RNF-04 | Sí (logs en Warning, sin tokens ni documento) | `F004_RNF04_*` | |
| RNF-05 | Sí (`iss`, PKCE S256, `state`) | `F004_RNF05_*`, `F004_CA04_*` | |
| RNF-06 | Sí (política en cada petición) | `F004_RNF06_*`, `F004_CA07_*` | |
| RNF-12 | Sí (enums en los dos contratos) | `F004_RNF12_*` | |
| RNF-14 | Sí (commit en rojo `7a0c936`) | Historial | |
| RNF-16 | Sí (perfiles `app` y `web`, Dockerfiles, CI) | CI `entorno-local` | Sin prueba local del compose completo |
| CL-01 | Sí | `F004_CA04_*` (×3) | |
| CL-02 | Sí | `F004_CL02_*` (×2) | |
| CL-03 | Sí | `F004_CL03_*` (×2) | |
| CL-04 | Sí (`rol` null) | `F004_CL04_*` | La pantalla es del web |
| CL-05 | Sí | `F004_CL05_*` | |
| CL-08 | Sí | `F004_CL08_*`, `F004_CA13_*` | |
| CL-09 | Sí | `F004_CL09_*`, `F004_CA10_*` | Spec ajustada en el Build (ver desviaciones) |
| CL-10 | Sí | `F004_CL10_*`, `F004_CA14_*` | |
| CL-11 | Sí (`no-store`, `nosniff`) | `F004_CL11_*` | |
| CL-12 | Sí | `F004_CL12_*`, `F004_CA16_*` | |
| CA-01, CA-02 | Sí | `F004_CA01_*`, `F004_CA02_*` | Recorrido real en el web |
| CA-04 | Sí | `F004_CA04_*` | |
| CA-05 | Sí (lado servidor) | `F004_CA05_CookieDeSesion_EsHttpOnlyYNoContieneTokens` | |
| CA-06 | Sí | `F004_CA06_*` (7 pruebas) | |
| CA-07, CA-09, CA-10 | Sí | `F004_CA07_*`, `F004_CA09_*`, `F004_CA10_*` | |
| CA-13, CA-14, CA-16, CA-21 | Sí | `F004_CA13_*`, `F004_CA14_*`, `F004_CA16_*`, `F004_CA21_*` | |
| CA-34 | Sí (lado contrato) | `F004_RNF12_ContratoOpenApi_EnumeraTodosLosCodigosDeError` | Los mensajes son del web |
| ADR-0020 | Sí | `F004_ADR0020_*` (4 pruebas) | Sustituye `F003_ADR0014_…` como pedía la tarea |

Todas las pruebas nuevas llevan el prefijo `F004_`. No hay ninguna sin prefijo ni con el de otra feature.

## Hallazgos obligatorios

- **[src/CoreBancario.Bff/Sesiones/AlmacenDeSesiones.cs:63-66 y src/CoreBancario.Bff/InicioDeSesion/EventosDeOidc.cs:81-83] Fijación de sesión: un inicio de sesión de otra persona hereda la clave de la cookie anterior.**
  - **Qué pasa:** cuando el regreso de Keycloak llega con una cookie de sesión válida, `TicketReceived` pide la cookie y el manejador guarda su clave K. Si la sesión es de otro `sid` u otra área, la cierra (`CerrarAsync`). Después, el manejador de cookies llama a `RenewAsync(K, ticketNuevo)` y no a `StoreAsync`. Lo dicen el propio comentario de `RenewAsync` y `learning.md`, sección 4. `RenewAsync` guarda la sesión nueva **con la misma clave K**.
  - **Consecuencia:** cualquiera que tuviera una copia de la cookie antigua tiene ahora la sesión de la persona que acaba de entrar.
  - **Escenario de ataque:**
    1. El atacante inicia sesión con su propia cuenta y obtiene la cookie C, con clave K.
    2. Con un XSS, la planta en el navegador de la víctima. JavaScript puede crear `cb-sesion` o `__Host-cb-sesion` si no existe ya la `HttpOnly`.
    3. La víctima termina un inicio de sesión que había empezado.
    4. La C del atacante abre ahora la sesión de la víctima desde fuera de su navegador.
  - **La prueba ya monta este escenario:** `F004_RN26_RegresoDeOtraSesion_…`, con `CopiarCookieDeSesionDe(navegadorDeA)` en `tests/CoreBancario.Keycloak.Tests/SesionDelBffTests.cs:369`. Pero no comprueba qué abre después la cookie de A.
  - **Requisito afectado:** es justo lo que RNF-01 y la decisión del autor quieren evitar: *"no puede convertir ese acceso temporal en una sesión reutilizable fuera del navegador de la víctima"*. También ADR-0020, que dice "la anterior se cierra … antes de guardar **la nueva**". Es además la regla habitual de rotar el identificador de sesión al autenticar (OWASP ASVS 3.2.1).
  - **Corrección para el test-writer:** en un commit `test(004)`, ampliar `F004_RN26_…` (o crear una prueba `F004_RN26_RegresoDeOtraSesion_LaCookieAnteriorNoAbreLaNueva`). Después del regreso de B, la cookie que conserva A debe responder 401 `sin-sesion` en `/bff/sesion`. Con el código actual esa aserción fallaría.
  - **Corrección para el coder**, la más simple: no reutilizar la clave cuando cambia la identidad. Dos opciones:
    - En la rama "otro `sid` u otra área" de `TicketReceived`, después de `CerrarAsync`, descartar el regreso. Sería `SignOutAsync` de la cookie, `HandleResponse()` y una redirección a `Avisos.UrlDe(area, Avisos.OtraSesionAbierta)`, con el cierre de la sesión nueva en Keycloak. Encaja con RN-26/CL-10 ("primero se cierra la actual") y es un caso raro.
    - Si se prefiere conservar el inicio, hay que forzar que el manejador llame a `StoreAsync` con una clave nueva. Antes, hay que comprobar si `SignOutAsync` limpia la clave guardada en el manejador.

    En los dos casos, `RenewAsync` solo debería reemplazar la sesión cuando el ticket trae el **mismo** `sid` y la **misma** área (caso de "Cambiar clave").
  - **Responsable:** test-writer (la prueba) y coder (el arreglo).

## Sugerencias opcionales

1. **`ServicioDeSesion.cs:31` y `:62`, `clave!`.** El `!` es seguro porque `ObtenerVigenteAsync` ya lanzó si `clave` era null, pero la skill pide no silenciar el análisis de nulos. `ObtenerVigenteAsync` podría devolver la sesión y usarse `sesion.Clave`.
2. **`ProxyDeLaApi.cs:32`.** `Api.Url` se lee al mapear la ruta, no de forma perezosa como dice el plan (sección 3.3). Funciona porque hay un valor por defecto, pero `IOptions` se resuelve antes de tiempo. Se puede dejar (KISS) o anotarlo.
3. **`Program.cs`, `ForwardedHeaders` sin redes conocidas.** Se fía de `X-Forwarded-*` de cualquier origen. Es aceptable mientras el BFF no publique puertos, como en compose. Un comentario o una restricción a la red de compose evitaría sorpresas si algún día se publica el 5180.
4. **`AlmacenDeSesiones.Eliminar`** quita el `SemaphoreSlim` sin `Dispose`. No pierde memoria de forma relevante, pero se puede liberar al eliminarlo.
5. **`EventosDeOidc.TokenValidated`, `context.Properties!`.** Mismo comentario que el punto 1.

## Desviaciones entre spec/plan y código

- **CL-09 cambió en el Build (commit `9bafe33`, aprobado por el autor).** Si la sesión de Keycloak sigue viva, `prompt=login` muestra la pantalla de reautenticación (clave y código) y no la de usuario, clave y código. La prueba se ajustó en un commit `test(004)` (`d1e84a4`), lo cual es válido. Queda documentado en la spec. El texto de CA-10 sigue diciendo "usuario, clave y código", y eso es correcto para el caso normal.
- **Mapper `preferred-username-en-token-de-identidad`** en los dos realms: añadido en el Build y registrado en el plan (sección 4).
- **Métodos públicos fuera de las firmas de la sección 3.3:**
  - `AlmacenDeSesiones.ObtenerParaEvaluar`, con una retención de 1 minuto pasada la vida máxima;
  - `PropiedadesDeSesion.LeerClaveAsync`.

  Están explicados en `learning.md`, pero no en el plan. Conviene una línea en el plan para que siga siendo la fuente.
- **`AlmacenDeSesiones.RenewAsync` reemplaza la sesión** cuando el ticket trae tokens. El plan no lo describía (ver el hallazgo).
- **`.dockerignore` en la raíz:** no está en la lista de archivos de T10, pero está justificado (deja `.env` fuera del contexto de build). Es un cambio de alcance menor y razonable.
- **Spec y plan aprobados** se editaron después de la aprobación. Está trazado en commits `docs(004)`.

## RNF

- **RNF-01, RNF-02, RNF-04, RNF-05, RNF-06:** las pruebas del BFF y de Keycloak real lo demuestran, salvo el hallazgo 1. La parte de navegador real (CA-05 y CA-06 entre orígenes) se verifica en el PR web.
- **RNF-16:** el compose y el job `entorno-local` del CI están bien construidos, pero no levanté `--profile app` en local. Queda pendiente de que el CI salga en verde.
- **T11, comprobación manual de logs sin token ni documento:** el código solo registra el área, el estado HTTP y el tipo de excepción, y hay pruebas (`F004_RNF01_FlujoDeSesion_LosLogsNoContienenTokens`, `F004_RNF04_*`). No la repetí a mano.
- **Otras comprobaciones:**
  - Integridad de las pruebas: el commit en rojo es `7a0c936`. Después, `tests/` solo cambia en `d1e84a4` (`test(004)`). Ni `c8f2148` (`feat`) ni `9e0397e` (`fix`) tocan `tests/`.
  - No queda ningún `// SDD: esqueleto creado por test-writer` en `src/`.
  - Arquitectura: el BFF no referencia Application ni Domain, como pide el plan. La interfaz `IClienteDeIdentidad` está justificada porque es una frontera con un sistema externo.
  - `specs/004-front-base/learning.md` existe.

## Resultado de tests

- `dotnet build`: correcto, 0 advertencias y 0 errores. Los contratos regenerados no cambian el árbol.
- `dotnet format --verify-no-changes`: sin cambios.
- `dotnet test` (con Docker 29.9.2): **1165 correctas, 0 errores, 0 omitidas** (8 min 03 s).
  - Domain: 3 s
  - Application: 7 s
  - Infrastructure: 1 min 37 s
  - Api: 1 min 38 s
  - Bff: 7 s
  - Keycloak: 8 min 01 s

## Preguntas de comprensión para el autor

1. Las peticiones a `/api/*` renuevan el token cuando hace falta, pero **nunca** cuentan como actividad. Solo `POST /bff/actividad` mueve `UltimaActividad`. ¿Por qué no basta el reloj de inactividad de Keycloak (`ssoSessionIdleTimeout`) para cumplir RN-25? ¿Qué pasaría con una pestaña abierta que refresca datos sola cada minuto?
2. La cookie es `SameSite=Strict` y, aun así, el BFF exige `X-CSRF: 1` y `Sec-Fetch-Site: same-origin`. ¿Qué ataque deja pasar `SameSite` y frenan las otras dos defensas? Pista: la spec habla de un subdominio hermano, que es del "mismo sitio".
3. Sobre el hallazgo: si el BFF reutiliza la clave de la cookie anterior al iniciar una sesión nueva, ¿por qué eso rompe la promesa de RNF-01 ("un XSS no puede llevarse una credencial reutilizable") aunque los tokens nunca salgan del servidor?

Archivos clave:
- `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Bff\Sesiones\AlmacenDeSesiones.cs`
- `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Bff\InicioDeSesion\EventosDeOidc.cs`
- `C:\Users\miguel\source\repos\core-bancario\tests\CoreBancario.Keycloak.Tests\SesionDelBffTests.cs`
- `C:\Users\miguel\source\repos\core-bancario\specs\004-front-base\learning.md`
