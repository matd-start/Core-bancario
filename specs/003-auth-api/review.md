# Revisión — 003-auth-api, bloque 003a (Proteger la API)

## Veredicto: APROBADO

El bloque 003a cumple la spec, el plan y las ADR-0014 a ADR-0019, y todas las pruebas pasan. No hay hallazgos obligatorios. Quedan sugerencias de defensa en profundidad y de trazabilidad que conviene resolver, en este PR o al empezar 003b.

Qué revisé:
- El alcance son T01 a T14, CA-06 a CA-22 y la sección 7 del plan, "Pruebas por bloque" de 003a. El bloque 003b (T15 a T27, CA-01 a CA-05, CL-12 a CL-16, RF-13 y RN-20) queda fuera y no se trata como pendiente.
- Integridad de las pruebas:
  - El commit en rojo es `1e2e69c test(003): pruebas en rojo del bloque 003a`.
  - `git show 9931b37 -- tests/` sale vacío, así que el commit `feat` no toca `tests/`.
  - Compilé el commit en rojo aparte con `git archive`. Compila con 22 esqueletos, y en él fallan 15 pruebas de Domain y 11 de Application.
- Esqueletos: en `src/` no queda ningún `// SDD: esqueleto creado por test-writer`.
- Nombres de las pruebas: las 101 pruebas nuevas llevan el prefijo `F003_`. Las de la 002 que se adaptaron conservan `F002_`, como pide el plan.
- `dotnet format --verify-no-changes` no deja cambios. Tras `dotnet build`, el contrato `src/CoreBancario.Api/openapi/core-bancario-api.json` sigue sin diferencias con lo versionado (`git status` limpio).
- Existe `specs/003-auth-api/learning.md`.

## Trazabilidad

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RF-05 (cliente) | `/mis-cuentas`, `ICuentasDelTitular`, `CuentasDelTitular` | Por CA-14 a CA-17 | Ninguna prueba lleva `RF05` en el nombre (sugerencia 5) |
| RF-14 | Realms, flujo `browser-con-otp`, temas | Por CA-06 a CA-12 (E2E) | Ninguna prueba lleva `RF14` en el nombre (sugerencia 5) |
| RF-15 | `UPDATE_PASSWORD` con `max_auth_age=0`, `passwordPolicy` | `F003_RF15_*` (3), `F003_CA13_*` (2) | |
| RN-12 | Filtro `EXISTS` dentro de la consulta | `F003_RN12_CuentasDelTitular_SoloDevuelveLasDelClienteVinculado`, `F003_RN12_ClienteLeeCuentaAjenaPorLaRutaDelOperador_Responde403` | |
| RN-17 | `AccesoDigital.NombreDeUsuarioDe`, `documento.js` | `F003_RN17_*` (3), `F003_CA12_ScriptDelTema_NormalizaComoElDominio` | |
| RN-19 | Esquema por realm, rol en `TokenValidated`, `Sub()` del token | `F003_RN19_*` (9) | |
| RN-21 | Acciones obligatorias; sin concesión directa ni `offline_access` | `F003_RN21_*` (2), `F003_CA07_*` | |
| RN-22 | Tiempos de sesión por realm | `F003_RN22_RealmsDeProduccion_TienenLosTiemposDeSesion`, `F003_CA10_*` | La vida máxima solo se comprueba en la configuración, como prevé el plan |
| RN-23 | Protección contra fuerza bruta, 5 fallos y 900 s | `F003_RN23_*`, `F003_CA09_*`, `F003_CL03_*` | |
| RN-24 | OTP `REQUIRED` en el flujo de navegador; TOTP de 6 dígitos y 30 s | `F003_RN24_*` (2), `F003_CA06_*` | |
| CL-01 | Mismo texto en las 5 claves de mensaje | `F003_CL01_CredencialBloqueada_MismoMensajeGenerico`, `F003_CA08_*` | |
| CL-02 | `normalizarUsuario` | Por `F003_CA12_*` | Ninguna prueba lleva `CL02`; el plan la asigna a CA-12 |
| CL-03 | Configuración de Keycloak | `F003_CL03_CodigoOtpIncorrecto_CuentaComoIntentoFallido` | |
| CL-04 | Acciones obligatorias; sin token la API responde 401 | Por `F003_CA07_*` | Ninguna prueba lleva `CL04` (plan: CA-07) |
| CL-05 | Límite conocido, documentado en el README | Sin prueba | Así lo decidió el autor (enmienda del 2026-10-13) |
| CL-06 | `passwordPolicy`, aviso en `documento.js` | `F003_CL06_*` (4), `F003_CA13_ClaveNuevaDemasiadoCorta_*` | |
| CL-07 | `JwtBearer` y `EventosDeJwt.Challenge` | Por `F003_CA18_*` (teoría) | Ninguna prueba lleva `CL07` (plan: CA-18) |
| CL-08 | Grupos con `RequireAuthorization(Politicas.Operador)` | Por `F003_CA19_ClienteLlamaOperacionDelOperador_Responde403` | Ninguna prueba lleva `CL08` (plan: CA-19) |
| CL-09 | Política `Cliente` en `/mis-cuentas` | Por `F003_CA19_OperadorLlamaMisCuentas_Responde403` | Ninguna prueba lleva `CL09` (plan: CA-19) |
| CL-10 | Revocación de la sesión en Keycloak; validación local en la API | `F003_CL10_CerrarSesion_ElAccessTokenYaEntregadoSigueAceptadoHastaQueVence`, `F003_CA11_*` | |
| CL-11 | `ssoSessionIdleTimeout` y `ssoSessionMaxLifespan` | Por `F003_CA10_*` y `F003_RN22_*` | Ninguna prueba lleva `CL11` (plan: CA-10) |
| CL-17 | Filtro de dueño y mensaje compartido | Por `F003_CA15_*` (5) | Ninguna prueba lleva `CL17` (plan: CA-15) |
| CL-18 | `ListarAsync` sin filtrar por estado | `F003_CL18_*` (2) | |
| CA-06 | Realms, temas y `/mis-cuentas` | `F003_CA06_PrimerIngreso_ExigeCambiarClaveEInscribirOtpYLuegoEntraConRolCliente` | E2E |
| CA-07 | Acciones obligatorias y 401 | `F003_CA07_ConClaveTemporalSinCompletarLosPasos_NoSeEmiteTokenYLaApiResponde401` | E2E |
| CA-08 | Tema `core-bancario` | `F003_CA08_DocumentoInexistenteClaveIncorrectaYDeshabilitado_MismoMensaje` | E2E |
| CA-09 | Bloqueo | `F003_CA09_CincoFallos_ElSextoCorrectoSeRechazaYTrasLaEsperaEntra` | E2E con tiempos cortos |
| CA-10 | Inactividad | `F003_CA10_ClienteInactivo_*`, `F003_CA10_OperadorInactivo_*` | E2E |
| CA-11 | Cierre de sesión | `F003_CA11_CerrarSesion_ElRefreshTokenYaNoSirve` | E2E |
| CA-12 | `documento.js` y `NombreDeUsuarioDe` | `F003_CA12_*` (5) | Jint y E2E |
| CA-13 | `max_auth_age=0` y política de clave | `F003_CA13_*` (2) | E2E |
| CA-14 | `ListarMisCuentas` | `F003_CA14_*` (5) | App, Infra y Api |
| CA-15 | `ObtenerMiCuenta` y filtro en la consulta | `F003_CA15_*` (6) | Incluye la comparación de las dos respuestas salvo el `traceId` |
| CA-16 | `ObtenerMiCuenta` | `F003_CA16_*` (3) | |
| CA-17 | Lista vacía | `F003_CA17_*` (6) | |
| CA-18 | 401 `no-autenticado` | `F003_CA18_OperacionProtegidaConCredencialInvalida_Responde401` | Teoría: cada operación por cada defecto del token |
| CA-19 | 403 `prohibido` | `F003_CA19_*` (3) | |
| CA-20 | `RegistroDeOperacionDelOperador` en 5 operaciones | `F003_CA20_*`, las pruebas `F002_*` con token de operador, `F003_RNF05_*` | |
| CA-21 | 270 s + 30 s | `F003_CA21_*` (2), `F003_RNF01_*` (2) | |
| CA-22 | OpenAPI generado y versionado | `F003_CA22_*` (5), `F002_RNF05_ContratoOpenApi_*` ampliada | |

## Hallazgos obligatorios

Ninguno.

## Sugerencias opcionales

Ordenadas por impacto.

1. **`delete_credential` y otras acciones iniciadas por la aplicación siguen activas en los dos realms.**
   - Dónde: `C:\Users\miguel\source\repos\core-bancario\infra\keycloak\realms\clientes-realm.json:810` y `empleados-realm.json:810`.
   - El plan quitó `manage-account` porque "la consola de cuenta permitiría a un usuario borrar su OTP" (tabla "Ajustes de los dos realms"). La acción `delete_credential` permite lo mismo con `kc_action=delete_credential:<id>` desde `core-bancario-web`. Revisé la clase `DeleteCredentialAction` en la imagen 26.3.3: solo comprueba el nivel de autenticación, no el rol `manage-account`.
   - El riesgo práctico es bajo, porque el id de la credencial no se puede descubrir sin la API de cuenta.
   - Aun así contradice la intención de la spec: si alguien pierde el celular, el que restablece es el operador (sección 11, RN-20).
   - Propuesta: desactivar en los dos realms `delete_credential`, `UPDATE_PROFILE` y `VERIFY_EMAIL`, que la SPA no usa, y ampliar `F003_RN21_RealmsDeProduccion_RolesPorDefectoSinOfflineAccessNiConsolaDeCuenta` para que lo compruebe.
   - Responsables: coder y test-writer.

2. **Un emisor sin configurar falla en silencio.**
   - Dónde: `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Api\Autenticacion\ConfiguracionDeAutenticacion.cs:23`.
   - Fuera de Development, `appsettings.json` solo trae `Audiencia`. Si el despliegue olvida `Autenticacion:Clientes:Emisor` o `Autenticacion:Empleados:Emisor`, `Authority` queda vacía y no se configura ninguna clave de firma.
   - Resultado: todas las peticiones reciben 401 y el log dice `firma-invalida`. Es seguro porque falla cerrado, pero cuesta diagnosticarlo.
   - Propuesta: `AddOptions<OpcionesDeAutenticacion>().Bind(...).Validate(o => entorno.IsDevelopment() || (Uri.IsWellFormedUriString(o.Clientes.Emisor, UriKind.Absolute) && ...)).ValidateOnStart()`. Puede esperar a D-11. Responsable: coder.

3. **`RegistroDeOperacionDelOperador` registra "completada" cuando la respuesta es 400.**
   - Dónde: `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Api\Autenticacion\RegistroDeOperacionDelOperador.cs:21`.
   - `RegistrarCliente` y `AbrirCuenta` devuelven `ValidationProblem` como resultado, sin lanzar excepción, así que el log dice "completada" aunque no se cambió nada.
   - Se cumple RNF-05, porque el log lleva el identificador del operador, pero la etiqueta engaña. El código sigue la letra del plan, que solo distinguía "lanzó / no lanzó".
   - Propuesta: si `resultado is IStatusCodeHttpResult { StatusCode: >= 400 }`, registrar "no completada". Responsables: coder y test-writer (un caso 400).

4. **El rol de la API se añade sin quitar antes los claims de rol que traiga el token.**
   - Dónde: `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Api\Autenticacion\EventosDeJwt.cs:152`.
   - Con `MapInboundClaims = false`, un token firmado por el realm `clientes` que incluyera un claim llamado `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` con el valor `Operador` llegaría con ese rol. Haría falta un mapper creado por un administrador de Keycloak.
   - La afirmación de ADR-0015, "el rol depende del esquema", solo es estructural si ese claim se elimina. Basta con borrar los claims de `ClaimTypes.Role` antes de añadir el rol del esquema.
   - Hay un detalle menor en la línea 90: `r.GetString()!` usa `!`, que la skill desaconseja; se puede cambiar por `OfType<string>()`.
   - Responsable: coder.

5. **Varios CL y RF no tienen una prueba con su ID en el nombre.**
   - Afecta a CL-02, CL-04, CL-07, CL-08, CL-09, CL-11, CL-17, RF-05 y RF-14.
   - No es obligatorio: el plan aprobado (sección 5, "Casos de la spec") los asigna a pruebas de un CA que cita esos CL, y el comportamiento está probado.
   - Sin embargo, la 001 y la 002 sí usaron nombres `F00N_CLxx_`, así que la búsqueda por ID ya no es uniforme.
   - Propuesta: renombrar una prueba representativa de cada uno, por ejemplo `F003_CL07_…`, o anotar la convención en el plan. Responsable: test-writer, en un commit `test(003)`.

6. **El requisito de seguridad del OpenAPI solo se detecta con metadatos explícitos.**
   - Dónde: `C:\Users\miguel\source\repos\core-bancario\src\CoreBancario.Api\OpenApi\RequisitoDeSeguridadEnOpenApi.cs:16`.
   - Solo marca `security` cuando hay `IAuthorizeData`. Una ruta protegida únicamente por la `FallbackPolicy` quedaría protegida en tiempo de ejecución, pero sin `security` en el contrato.
   - Hoy todas las rutas declaran su política, así que es solo un aviso para 003b.

7. **El riesgo 4 del plan quedó sin cerrar por escrito.** El plan pedía comprobar que la cookie SSO no reabre la sesión después de la inactividad. Las pruebas de CA-10 comprueban el refresh token, no la cookie. Conviene anotar el resultado en el plan o en el README.

## Desviaciones entre spec/plan y código

- **Plan, sección 4, "Aviso del número de documento"** (`specs/003-auth-api/plan.md:816`): todavía dice "Hay que verificarlo en el Build". La plantilla `keycloak.v2` no trae el campo `username`, así que se aplicó la alternativa prevista: se sobrescribió `login-update-password.ftl` con `data-usuario`. Conviene anotarlo en el plan, junto al riesgo 8, porque esa plantilla queda acoplada a Keycloak 26.3.3.
- **Plan, tabla "Ajustes de los dos realms", fila `requiredActions`**: no menciona que siguen activas `UPDATE_PROFILE`, `VERIFY_EMAIL`, `delete_credential` y `update_user_locale` (ver la sugerencia 1).
- **Plan, sección 8** (`plan.md:1019`): dice "ADRs nuevas (todas en `Propuesta`)", pero las ADR-0014 a ADR-0019 están en `Aceptada`. Hay que actualizar el texto.
- **`scripts=` funciona con `keycloak.v2` en la pantalla de login**: no hizo falta sobrescribir `login.ftl`. Coincide con el plan; se puede cerrar ese riesgo.
- Lo demás coincide con el plan:
  - firmas;
  - rutas y `operationId`;
  - el contrato JSON (todas las operaciones con 401, 403 y 500, más el esquema `Bearer`);
  - la migración `VinculoDeAccesoDigital`, con PK, FK `Restrict` e índice único `ux_accesos_digitales_sub`;
  - 270 s + 30 s y `ValidAlgorithms = RS256`;
  - la `FallbackPolicy`;
  - el README con los límites conocidos;
  - en CLAUDE.md, D-08 → ADR-0014 y la excepción `CoreBancario.Keycloak.Tests`.
- **Arquitectura**:
  - Domain sigue sin paquetes.
  - Application solo define el puerto `ICuentasDelTitular`, e Infrastructure lo implementa con EF Core y `AsNoTracking`. Usar Dapper no se justificaba (ADR-0004).
  - Los endpoints son delgados y toman `Sub` del token, nunca de la petición (RN-19).
- **Requisitos no funcionales**:

  | RNF | Estado | Evidencia o comentario |
  |---|---|---|
  | RNF-01 | Verificado | Configuración, CA-21 y `F003_CL10` E2E. La espera real de 5 min contra Keycloak no se ejecuta; se verifica la suma de los dos valores. |
  | RNF-02 | Verificado | CA-15, CA-18 y CA-19 |
  | RNF-03 (parte de 003a) | Verificado en parte | `IncludeErrorDetails=false` probado; el token no aparece en el log (prueba de RNF-04); los realms no traen claves; gitleaks corre en el CI. Lo de la clave temporal es de 003b. |
  | RNF-04 | Verificado | `F003_RNF04_*` (4) |
  | RNF-05 | Verificado | CA-20 y `F003_RNF05_*`, con el matiz de la sugerencia 3 |
  | RNF-06 | Verificado | CA-22, la teoría de la 002 ampliada y el paso del CI `git diff --exit-code` |
  | RNF-07 | Verificado | El commit en rojo existe y las pruebas fallaban en él |
  | RNF-08 | Verificado | `F003_CA06` hace un login real con PKCE y luego llama a `/mis-cuentas` |
  | Ejecución en el CI | No verificable en local | Que `CoreBancario.Keycloak.Tests` corra en el runner de GitHub (unos 9 min en local) solo se confirma en el PR. |

## Resultado de tests

`dotnet build`: compilación correcta, 0 advertencias y 0 errores. El contrato OpenAPI se regenera sin diferencias.

`dotnet test`, con Docker en marcha:

| Proyecto | Duración | Resultado |
|---|---|---|
| CoreBancario.Domain.Tests | 7,6 s | Correcto |
| CoreBancario.Application.Tests | 7,6 s | Correcto |
| CoreBancario.Infrastructure.Tests | 1 min 47 s | Correcto |
| CoreBancario.Api.Tests | 1 min 47 s | Correcto |
| CoreBancario.Keycloak.Tests | 8 min 51 s | Correcto (Keycloak 26.3.3 real) |
| **Total** | 8 min 55 s | **659, error: 0, correcto: 659, omitido: 0** |

## Preguntas de comprensión para el autor

1. "Mis cuentas" filtra por dueño dentro de la consulta (`EXISTS` sobre `accesos_digitales`) en vez de cargar la cuenta y comparar después su `ClienteId`. ¿Qué ataque concreto evita esto (BOLA/IDOR)? ¿Por qué garantiza "por construcción" que una cuenta ajena y una inexistente den una respuesta idéntica (CA-15)?
2. El rol `Operador` solo lo puede conceder el esquema `Empleados`, aunque el token de `clientes` diga tener ese rol en `realm_access.roles`. ¿Por qué el selector puede leer el `iss` sin validarlo y aun así un token falso que diga venir de `empleados` no consigue nada? ¿Qué pasaría si el rol se mapeara con `IClaimsTransformation`?
3. La tolerancia de reloj es una constante en el código (30 s) y no un valor de `appsettings`. ¿Por qué esa decisión protege RNF-01? ¿Y qué dos números hay que cambiar a la vez si algún día se quiere una ventana de revocación de 2 minutos?
