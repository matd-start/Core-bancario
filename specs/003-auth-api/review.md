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

---

# Revisión: 003-auth-api, bloque 003a (correcciones de la primera revisión)

## Veredicto: APROBADO

Las cuatro sugerencias que el autor eligió (1, 3, 4 y 5) quedaron resueltas tal como las describe `review.md`. Las pruebas nuevas fallaban en el commit en rojo y ahora pasan. El commit `fix` no toca `tests/`. Los renombres solo cambiaron la firma de cada prueba. La suite completa pasa: 670 pruebas, 0 errores. No encontré regresiones ni huecos de seguridad.

Lo único pendiente es documental, y no impide el merge: en `plan.md`, varias tablas siguen citando los nombres anteriores de las pruebas, y dos firmas no describen el comportamiento nuevo (ver "Desviaciones").

Qué comprobé:
- **Alcance.** Solo los commits `d8f711e`, `d14e86f`, `512cca6` y `1968201`. El bloque 003b (T15 a T27) queda fuera.
- **Commit en rojo.** Saqué `d8f711e` con `git archive` a una carpeta temporal y ejecuté ahí las pruebas nuevas: 13 en total, 9 fallan y 4 pasan. Las que fallan son las que deben fallar:
  - las 6 de `F003_RN21_RealmsDeProduccion_AccionesQueBorranElOtpOEditanElPerfilEstanDesactivadas`;
  - `F003_RNF05_OperacionDelOperadorQueRespondeError400_ElLogDiceNoCompletada`;
  - los 2 casos de `F003_RN19_TokenDe…ConClaimDeRol…` que usan el tipo `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`.
- **Casos que ya pasaban en rojo.** Los 2 casos con el claim corto `"role"` pasaban antes del arreglo. Es lo esperado: `RoleClaimType` se queda en `ClaimTypes.Role`, así que `"role"` nunca concedió un rol. Esos casos protegen contra regresiones, no reproducían un fallo.
- **Pruebas intactas tras el rojo.** `git log d8f711e..HEAD -- tests/` sale vacío, así que ni `d14e86f` ni los commits `docs` tocan `tests/`.
- **Calidad.** En `src/` no queda ningún esqueleto. `dotnet format --verify-no-changes` termina sin cambios. Después de `dotnet build`, `git status` sigue limpio, así que el contrato OpenAPI no cambia.

## Trazabilidad (solo lo que tocan estas correcciones)

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RN-21 (sug. 1) | `delete_credential`, `UPDATE_PROFILE` y `VERIFY_EMAIL` con `enabled: false` en los dos realms | `F003_RN21_RealmsDeProduccion_AccionesQueBorranElOtpOEditanElPerfilEstanDesactivadas` (6 casos) | Los dos realms tienen la lista `requiredActions` idéntica. Quedan activas `CONFIGURE_TOTP`, `UPDATE_PASSWORD` y `update_user_locale`, que es inocua |
| RN-19 / ADR-0015 (sug. 4) | `EventosDeJwt.TokenValidated` borra los claims de rol del token antes de añadir el del esquema | `F003_RN19_TokenDeClientesConClaimDeRolOperador_Responde403`, `F003_RN19_TokenDeEmpleadosConClaimDeRolCliente_Responde403EnMisCuentas` (2 casos cada una) | `GetString()!` se cambió por `OfType<string>()` |
| RNF-05 (sug. 3) | `RegistroDeOperacionDelOperador`: un resultado `IStatusCodeHttpResult` con código >= 400 se registra como "no completada"; antes desenvuelve `INestedHttpResult` | `F003_RNF05_OperacionDelOperadorQueRespondeError400_ElLogDiceNoCompletada` | La comprobación `DoesNotContain(": completada")` sirve: no coincide con ": no completada" |
| CL-02 | sin cambios | `F003_CL02_ScriptDelTema_NormalizaComoElDominio` (antes `CA12`) | CA-12 conserva 3 pruebas en Keycloak.Tests y 1 en Api.Tests |
| CL-08 | sin cambios | `F003_CL08_ClienteRegistraCliente_Responde403YNoCreaElCliente` (antes `CA19`) | CA-19 conserva `F003_CA19_ClienteLlamaOperacionDelOperador_Responde403` |
| CL-09 | sin cambios | `F003_CL09_OperadorLlamaMisCuentas_Responde403` (antes `CA19`) | Como en CL-08 |
| CL-11 | sin cambios | `F003_CL11_ClienteInactivo_SesionNoSeRenueva` (antes `CA10`) | CA-10 conserva `F003_CA10_OperadorInactivo_SesionNoSeRenueva` |
| CL-17 | sin cambios | `F003_CL17_CuentaAjenaEInexistente_RespuestasIdenticasSalvoTraceId` (antes `CA15`, en Api) | CA-15 conserva 4 pruebas en Application y 1 en Infrastructure. Ya no tiene ninguna en Api con su ID |
| RF-05 | sin cambios | `F003_RF05_MiCuenta_DevuelveNumeroMonedaEstadoSaldoYFechas` (antes `CA16`, en Api) | CA-16 conserva sus pruebas de Application e Infrastructure |
| CL-04, CL-07, RF-14 | sin cambios | Por `F003_CA07_…`, `F003_CA18_…` y `F003_CA06/07/08/11_…` | Son la excepción que documenta la nueva "Convención de nombres" del plan, y la excepción está justificada |

Ningún CA se quedó sin al menos una prueba con su ID. Todas las pruebas nuevas llevan el prefijo `F003_`.

## Hallazgos obligatorios

Ninguno.

Por qué no hay nada de seguridad que corregir:
- **`EventosDeJwt`.** Las políticas usan `RequireRole`, que consulta `identity.RoleClaimType`, y el arreglo borra exactamente ese tipo de claim antes de conceder el rol del esquema. Ahora ADR-0015 ("el rol depende del esquema") se cumple por construcción.
- **Realms.** Desactivar esas tres acciones no afecta a `CONFIGURE_TOTP` ni a `UPDATE_PASSWORD`. El restablecimiento del OTP que hará el operador en 003b va por la API de administración, que no depende de estas acciones. Las 670 pruebas pasan, incluidas las E2E contra Keycloak 26.3.3 real.

## Sugerencias opcionales

1. **Los dos bucles de `EventosDeJwt.cs:36-39` hacen lo mismo hoy.**
   - Como `RoleClaimType` no se configura, vale `ClaimTypes.Role` y los dos bucles borran el mismo tipo de claim.
   - Dejarlos es una defensa razonable por si algún día alguien configura `RoleClaimType = "role"`. Pero en ese caso el `AddClaim(new Claim(ClaimTypes.Role, …))` de la línea 42 dejaría de conceder el rol.
   - Propuesta: añadir el rol con `identidad.RoleClaimType` en vez de `ClaimTypes.Role`, para que borrar y conceder usen el mismo tipo. Responsable: coder.
2. **Cabecera de sección en `MisCuentasApiTests.cs:108`.** Dice "CA-16: detalle de una cuenta propia", pero la prueba que hay debajo ahora es `F003_RF05_…`. Conviene poner "CA-16 / RF-05". Responsable: test-writer, en un commit `test(003)`.
3. **Volumen local de Keycloak en el PR.** El realm solo se importa si no existe (riesgo 10 del plan). Quien ya tenga el volumen local seguirá con `delete_credential` activo hasta borrarlo. Basta con decirlo en la descripción del PR.

## Desviaciones entre spec/plan y código

`512cca6` añadió a `plan.md` la "Convención de nombres", pero no actualizó las tablas que citan las pruebas renombradas. Hoy, buscar en el plan un nombre de prueba lleva a uno que ya no existe:

- `plan.md:886` (CL-02) cita `F003_CA12_ScriptDelTema_NormalizaComoElDominio`. Ahora es `F003_CL02_…`.
- `plan.md:892` (CL-08) solo cita `F003_CA19_ClienteLlamaOperacionDelOperador_…`. Falta `F003_CL08_ClienteRegistraCliente_…`.
- `plan.md:893` (CL-09) y `plan.md:983` (CA-19) citan `F003_CA19_OperadorLlamaMisCuentas_Responde403`. Ahora es `F003_CL09_…`.
- `plan.md:895` (CL-11) y `plan.md:974` (CA-10) citan `F003_CA10_ClienteInactivo_…`. Ahora es `F003_CL11_…`.
- `plan.md:901` (CL-17) y `plan.md:979` (CA-15) citan `F003_CA15_CuentaAjenaEInexistente_RespuestasIdenticasSalvoTraceId`. Ahora es `F003_CL17_…`.
- `plan.md:976` (CA-12) cita el nombre anterior de la prueba de Jint.
- `plan.md:980` (CA-16) cita como prueba de Api `F003_CA16_MiCuenta_…`. En Api ahora es `F003_RF05_…`; el nombre `CA16` solo sobrevive en Application.

Además:
- **`plan.md:632`, firma de `RegistroDeOperacionDelOperador`.** Sigue diciendo "no completada" solo "si la ejecución lanzó". El código también lo registra cuando la respuesta es >= 400. `learning.md` sí lo explica.
- **`plan.md:590-592`, firma de `EventosDeJwt.TokenValidated`.** No menciona que primero se borran los claims de rol del token.
- **Pruebas nuevas.** Las 3 de esta corrección (`F003_RN21_…AccionesQueBorran…`, `F003_RN19_TokenDe…ConClaimDeRol…` y `F003_RNF05_…Error400…`) no aparecen en las tablas de pruebas del plan (secciones 5 y 7).
- **`1968201`, cambios en la spec.** La aclaración de CA-15 y el nuevo punto de "Fuera de alcance" coinciden con el código. `HuellaDeLaRespuestaAsync` quita `traceId` y cambia por `{id}` el id que aparece en la respuesta, que es justo lo que la aclaración permite. "Sección 9" sí es "Fuera de alcance".
- **Coherente con el código:**
  - la fila `requiredActions` del plan (`plan.md:788`);
  - la nota de `login-update-password.ftl`;
  - "Aceptada" en la sección 8;
  - los pendientes 13 y 14 del plan, que recogen las sugerencias 2 y 6 y `admin-cli`.
- **Sugerencia 7 de la primera revisión** (cookie SSO tras la inactividad). Quedó anotada como pendiente en el riesgo 4, como pedía la sugerencia.

## Resultado de tests

- `dotnet build`: compilación correcta, 0 advertencias y 0 errores.
- `dotnet test` en HEAD (`1968201`), con Docker 29.8.2:

| Proyecto | Duración | Resultado |
|---|---|---|
| CoreBancario.Domain.Tests | 11 s | Correcto |
| CoreBancario.Application.Tests | 11 s | Correcto |
| CoreBancario.Infrastructure.Tests | 1 min 31 s | Correcto |
| CoreBancario.Api.Tests | 1 min 18 s | Correcto |
| CoreBancario.Keycloak.Tests | 5 min 21 s | Correcto (Keycloak 26.3.3 real) |
| **Total** | 5 min 25 s | **670, error: 0, correcto: 670, omitido: 0** |

670 = 659 de la primera revisión + 11 casos nuevos (6 de RN-21, 4 de RN-19 y 1 de RNF-05).

- En `d8f711e`, con el filtro de las pruebas nuevas: 13 en total, 9 errores y 4 correctas. El detalle está arriba, en "Commit en rojo".

## Preguntas de comprensión para el autor

1. Antes del arreglo, el rol `Operador` ya dependía del esquema `Empleados`. Aun así, un token del realm `clientes` podía pasar la política `Operador`. ¿Qué cadena exacta lo permitía (`MapInboundClaims = false`, `RoleClaimType`, `IsInRole`)? ¿Y por qué el caso con el claim corto `"role"` nunca fue peligroso?
2. Quitar `manage-account` no bastaba para impedir que un cliente borrara su OTP, y hubo que desactivar `delete_credential`. ¿Qué diferencia hay entre un permiso (un rol que habilita la consola de cuenta) y una acción obligatoria que la SPA puede invocar con `kc_action`? ¿Qué regla de la spec se rompería si un cliente pudiera borrar su propio OTP?
3. El registro del operador ahora dice "no completada" ante un 400 o un 409 aunque nada haya lanzado una excepción. ¿Por qué en un registro de auditoría es peor un "completada" falso que un "no completada" de más? ¿Por qué hay que mirar dentro de `Results<A, B>` (`INestedHttpResult`) para saber el código real?

---

# Revisión — 003-auth-api, bloque 003b (Gestionar el acceso, T15–T27)

## Veredicto: APROBADO

No encontré nada que impida el merge. Todos los CA, CL y RN del bloque están implementados y tienen pruebas con el prefijo `F003_`. Se respetaron la integridad de las pruebas y las ADR-0014 y ADR-0017. La compilación da 0 advertencias, `dotnet test` pasa 890 de 890 (incluido Keycloak real) y `dotnet format --verify-no-changes` queda limpio. Tras compilar, el contrato OpenAPI regenerado no cambia el árbol de trabajo. Quedan sugerencias menores, ninguna obligatoria.

## Trazabilidad

| ID | Implementado | Test | Nota |
|---|---|---|---|
| RF-13 | Sí (handlers, endpoints, adaptador) | `F003_RF13_AddAplicacion_RegistraElAccesoDigitalComoScoped` y todas las de CA-01…CA-05 | |
| RN-20 | Sí (`SolicitudDeAcceso`, `ResultadoDeSolicitud`, trigger, PK `pk_resultados_de_acceso`) | `F003_RN20_EditarOBorrarElRegistro_LoRechazaLaBase`, `F003_RN20_DosResultadosParaLaMismaSolicitud_LanzaConflicto`, `F003_RN20_Solicitud_IdEsVersion7DeLaFecha`, `F003_RN20_HabilitarAcceso_RegistraAlOperadorDelComando` y unas 20 más | |
| RN-19 (en 003b) | Sí (operador desde `usuario.Sub()`) | `F003_CA01_HabilitarAcceso_RegistraAlOperadorDelToken`, `F003_CA05_ClienteGestionaAcceso_Responde403SinRegistros` | |
| CA-01 | Sí | App `F003_CA01_HabilitarClienteSinAcceso_CreaUsuarioYRegistraSolicitudYCompletado`, `…_VinculaElSubConElCliente`; Api `F003_CA01_HabilitarAcceso_Responde200ConClaveTemporal`, `…_RegistraAlOperadorDelToken`; Keycloak `F003_CA01_CrearAsync_UsuarioHabilitadoConClaveTemporalAccionesObligatoriasYMarca` | |
| CA-02 | Sí | App `F003_CA02_HabilitarClienteHabilitado_RestableceConClaveDistintaYRegistraRestablecer`; Api `F003_CA02_HabilitarDosVeces_LaSegundaEsRestablecerConOtraClave`; Keycloak `F003_CA02_ReiniciarAcceso_BorraOtpCierraSesionesYPoneClaveTemporal`, `F003_CA02_Restablecer_ElClienteEntraConLaClaveNuevaYReinscribeElSegundoFactor` | |
| CA-03 | Sí | App `F003_CA03_DeshabilitarClienteHabilitado_RegistraDeshabilitar`; Api `F003_CA03_DeshabilitarAcceso_Responde200YRegistraDeshabilitar`; E2E `F003_CA03_Deshabilitar_SesionNoSeRenuevaYNoPuedeIniciarSesion` | |
| CA-04 | Sí | App y Api: `F003_CA04_HabilitarClienteInexistente_…`, `F003_CA04_DeshabilitarYaDeshabilitado_…` (la de Api cuenta filas antes y después), `F003_CA04_DeshabilitarClienteInexistente_…` | |
| CA-05 | Sí | `F003_CA05_ClienteGestionaAcceso_Responde403SinRegistros` (teoría: habilitar y deshabilitar) | El nombre difiere un poco del plan (`ClienteHabilitaAcceso`). Es aceptable. |
| CL-12 | Sí (paso 1, sin escribir nada) | `F003_CL12_HabilitarClienteInexistente_LanzaRecursoNoEncontrado`, `F003_CL12_…_RespondeNoEncontradoConSuCodigo` | |
| CL-13 | Sí | `F003_CL13_ParaHabilitarConAccesoHabilitado_EsRestablecer`, `F003_CL13_HabilitarClienteHabilitado_NoCreaOtroVinculo` | |
| CL-14 | Sí (`ParaDeshabilitar` lanza antes de crear la solicitud) | `F003_CL14_ParaDeshabilitarSinAccesoODeshabilitado_LanzaAccesoNoHabilitado`, `F003_CL14_DeshabilitarClienteSinAcceso_Responde422SinRegistros`, `F003_CL14_AccesoNoHabilitado_EsUnaReglaDeNegocio` | |
| CL-15 | Sí (logout en Keycloak) | `F003_CL15_Restablecer_SesionNoSeRenueva` (E2E) | |
| CL-16 | Sí | `F003_CL16_ParaHabilitarConAccesoDeshabilitado_EsHabilitar`, `F003_CL16_HabilitarClienteDeshabilitado_RegistraHabilitarYLoHabilita`, `F003_CL16_ReiniciarAcceso_DeUnUsuarioDeshabilitado_LoHabilita`, `F003_CL16_HabilitarUnAccesoDeshabilitado_ElClienteVuelveAEntrar` | |
| RNF-03 (clave temporal y secreto) | Sí | `F003_RNF03_ClaveTemporal_ToStringNoRevelaElValor`, `F003_RNF03_AccesoHabilitadoDto_ToStringNoRevelaLaClave`, `F003_RNF03_HabilitarAcceso_NingunLogContieneLaClaveTemporal`, `F003_RNF03_HabilitarAcceso_RespondeNoStore`, `F003_RNF03_RealmClientes_NoContieneElSecretoDeServicio`, `F003_RNF03_ErrorAlCrear_NoIncluyeLaClaveTemporalEnElMotivo` | |
| RNF-05 (acceso) | Sí (filtro en el grupo) | `F003_RNF05_GestionarAcceso_ElLogIncluyeElOperador`, `F003_RNF05_HabilitarClienteInexistente_ElLogDiceNoCompletada` | |
| RNF-06 (acceso) | Sí (el contrato versionado lleva `security: Bearer` y todos los códigos) | `F003_RNF06_ContratoVersionado_DocumentaLasRespuestasDelAccesoDigital`, `F003_CA22_ContratoFueraDeDevelopment_IncluyeLasOperacionesDeAccesoDigital`, filas nuevas en `F002_RNF05_ContratoOpenApi_…` | |
| RNF-08 (flujo completo) | Sí | `F003_RNF08_FlujoCompleto_OperadorHabilitaYClienteConsultaSusCuentas` (sin usar el admin de `master`) | |
| ADR-0014 | Sí | `F003_ADR0014_CuentaDeServicio_SoloTieneManageUsersYViewUsers`, `…_NoPuedeModificarElRealm` (Keycloak real), `…_EsUnClienteConfidencialSoloDeServicio`, las cuatro de `TokenDeServicio_…` | Ver la sugerencia 1, sobre los roles de realm. |
| ADR-0017 | Sí | Todas las de la tabla "Intención primero" del plan, más `F003_ADR0017_ReintentoTrasUnaRespuestaPerdida_CierraLaAnteriorYResponde200` (Api), `F003_ADR0017_ConsultaDocumentadaDeSolicitudesSinResultado_DevuelveLasPendientes`, las de traducción HTTP y `F003_ADR0017_RealmClientes_DefineLaMarcaSoloVisibleYEditablePorAdmin` | |

**Integridad de las pruebas:**
- **Cambios en `tests/` dentro del commit `feat`:** ninguno. `git diff --stat 40ee080 339ca97 -- tests/` sale vacío.
- **Prefijos:** todas las pruebas nuevas llevan `F003_`. Las dos teorías de la 002 ampliadas (`F002_RNF05_ContratoOpenApi_…` y la base vacía) siguen lo que manda el plan (sección 7).
- **Esqueletos:** en `src/` no queda ningún `// SDD: esqueleto creado por test-writer`. En el commit en rojo había 18.
- **Alcance:** todos los archivos cambiados pertenecen a T15–T27 o al commit en rojo. No hay cambios fuera del plan.
- **`learning.md`:** existe y tiene la parte de 003b, con el mapa, el recorrido, los conceptos y los ejercicios.

## Hallazgos obligatorios

Ninguno.

## Sugerencias opcionales

1. **[infra/keycloak/realms/clientes-realm.json:972-974] La cuenta de servicio hereda `default-roles-clientes`, y por tanto el rol de realm `cliente`.**
   - ADR-0014 pide "solo `manage-users` y `view-users`" (menor privilegio).
   - Hoy no se puede explotar: el token de la cuenta de servicio no lleva la audiencia `core-bancario-api`, así que la API lo rechaza con 401.
   - Pero queda un rol que no necesita. Si algún día se añadiera el mapper de audiencia a un scope por defecto del realm, ese token entraría como Cliente.
   - Arreglo: dejar `realmRoles: []` (o quitar `cliente` de esa cuenta) y ampliar `F003_ADR0014_CuentaDeServicio_NoTieneRolesDeRealmDeAdministracion` para que exija una lista vacía.
   - Responsable: coder, con la prueba a cargo de test-writer.
2. **[src/CoreBancario.Application/AccesosDigitales/DeshabilitarAccesoHandler.cs:34] `acceso.Usuario!`.** La skill `csharp-clean-code` pide no silenciar el análisis de nulos con `!`. La lógica es correcta, porque `ParaDeshabilitar` ya lanzó si no había usuario, pero la garantía queda implícita. Alternativa mínima: `var usuario = acceso.Usuario ?? throw new InvalidOperationException(...)`.
3. **[src/CoreBancario.Domain/AccesosDigitales/SolicitudDeAcceso.cs:51-52] `Completar` no valida el largo de `motivo`; `Fallar` sí.** Hoy solo recibe una constante corta, pero un motivo de más de 200 caracteres fallaría en la base y no en el dominio. Basta con la misma guarda de longitud.
4. **[src/CoreBancario.Infrastructure/Identidad/ProveedorDeIdentidadKeycloak.cs:157] `GetValue<string>()` lanza `InvalidOperationException` si el atributo no es un string.** Eso daría un 500, no un 502. El caso es improbable porque el perfil limita el atributo. Se podría usar `(string?)nodo` con `TryParse`.
5. **[src/CoreBancario.Infrastructure/Identidad/TokenDeServicioHandler.cs:23] El `SemaphoreSlim` no se libera en `Dispose(bool)`.** Es menor: `IHttpClientFactory` rota el handler cada 2 min.
6. **[specs/003-auth-api/learning.md:1] El título sigue diciendo "(bloque 003a: proteger la API)"**, aunque el documento ya cubre los dos bloques.

## Desviaciones entre spec/plan y código

- **Deshabilitar también adopta.** Si existe un usuario en Keycloak sin vínculo, `DeshabilitarAccesoHandler` crea el vínculo (líneas 37-39). Es coherente con "sigue los mismos pasos" (plan, sección 5, paso 7), pero el plan solo lo dice explícitamente para habilitar. Conviene añadir una línea en la sección 5.
- **Nombres de prueba.** Hay pequeñas diferencias con la tabla "Dónde va cada CA". Por ejemplo, `F003_CA05_ClienteGestionaAcceso_…` frente a `ClienteHabilitaAcceso`, `F003_CA04_HabilitarClienteInexistente_NoRegistraNadaNiLlamaAlProveedor` frente a `…_LanzaNoEncontradoSin…`, y `F003_ADR0017_ObtenerUsuarioInexistente_DevuelveNull` frente a `…_UsuarioInexistente_…`. Los ID coinciden, así que la trazabilidad no se ve afectada. Para mantener la spec viva, convendría actualizar la tabla del plan.
- **Riesgo 4 del plan (cookie SSO tras la inactividad).** El plan decía "se hace en 003b o se documenta como límite conocido", y no se hizo ninguna de las dos cosas. No es un CA de 003b, pero conviene cerrarlo por escrito, en el plan o en el README.
- **Riesgos 1 y 2 de 003b del plan.** Se cumplió lo que la revisión de 003a pedía para 003b: las rutas nuevas declaran `RequireAuthorization(Politicas.Operador)` y aparecen con `security: Bearer` en el contrato.

**RNF:**
- **RNF-03:** está cubierto con pruebas: logs en memoria, `no-store`, `ToString`, marcador del secreto y motivo sin cuerpo. Lo que no se puede verificar en local es gitleaks en el CI, que se verá en el PR.
- **RNF-05, RNF-06 y RNF-08:** hay evidencia en pruebas que pasan.
- **RNF-07:** existe el commit en rojo `40ee080`, con esqueletos. No volví a ejecutar ese commit para ver el rojo.

## Resultado de tests

- `dotnet build`: compilación correcta, 0 advertencias y 0 errores. El contrato OpenAPI regenerado no cambia el árbol de trabajo.
- `dotnet test` (con Docker): **890 correctas, 0 con error, 0 omitidas**, en 5 min 45 s.

| Proyecto | Resultado | Duración |
|---|---|---|
| Domain.Tests | correcto | 6,5 s |
| Application.Tests | correcto | 6,0 s |
| Infrastructure.Tests | correcto | 1 min 16 s |
| Api.Tests | correcto | 1 min 16 s |
| Keycloak.Tests (Keycloak 26.3.3 real) | correcto | 5 min 41 s |

- `dotnet format --verify-no-changes`: sin cambios.

## Preguntas de comprensión para el autor

1. Cuando Keycloak responde con un error (502), se escribe un resultado `Fallido`. Cuando no responde (503), no se escribe nada y la solicitud queda pendiente. ¿Por qué tratar un tiempo de espera agotado como `Fallido` rompería RN-20 en el caso "aplicó y no respondió"? ¿Qué prueba demuestra que el diseño lo evita?
2. La marca `ultima_solicitud_de_acceso` se escribe en el **último** paso. Al deshabilitar, las sesiones se cierran **antes** de poner `enabled: false` y la marca. ¿Qué afirmaría el registro, de forma falsa, si el orden fuera el inverso y fallara el cierre de sesiones?
3. El estado del acceso (habilitado, OTP inscrito) no se guarda en PostgreSQL, y el resultado va en otra tabla cuya PK es `solicitud_id` en vez de una columna `estado` en la solicitud. ¿Qué problema evita cada una de esas dos decisiones?
