# ADR-0014: Keycloak como emisor de tokens, con dos realms

- Estado: Aceptada
- Fecha: 2026-10-13
- Feature: 003-auth-api (resuelve D-08 de la spec de producto)

## Contexto

La 003 protege la API. Cada petición debe identificar a quien la hace y su rol, Cliente u Operador (RN-19). La spec pide además funciones de identidad que no son triviales:

- segundo factor TOTP obligatorio, inscrito en el primer ingreso (RN-24);
- bloqueo temporal tras 5 intentos fallidos (RN-23);
- sesiones con dos relojes, inactividad y vida máxima, distintos para clientes y operadores (RN-22);
- claves temporales de un solo uso (RN-21);
- cambio de clave que exige la actual (RF-15);
- cerrar sesión (RF-14);
- un mensaje que no revele si un documento es cliente del banco (CL-01).

Hay que decidir **quién emite los tokens** (D-08).

Tres conceptos para leer esta ADR:

- **Emisor de tokens (proveedor de identidad):** el sistema que comprueba quién eres (clave, segundo factor) y te entrega un token firmado. Analogía: la Registraduría, que expide la cédula. Un banco no la fabrica: solo comprueba que es auténtica.
- **OpenID Connect (OIDC):** el protocolo estándar con el que una aplicación delega el inicio de sesión en un emisor y recibe un JWT. Al ser estándar, la SPA (004) y la API usan librerías conocidas, no código propio.
- **Realm (Keycloak):** un espacio aislado de usuarios, roles, clientes y políticas dentro de Keycloak. Dos realms son como dos edificios con porterías distintas: compartir el mismo servidor no significa compartir las llaves.

Dos experimentos contra Keycloak 26.3.3 real alimentaron esta decisión. Sus hallazgos están en la delegación del diseño y en el plan de la 003.

## Opciones consideradas

1. **La propia API como emisor.** Control total y ninguna pieza más. En contra: habría que construir y asegurar a mano:
   - el hash de claves;
   - TOTP con su ventana y la prohibición de reutilizar códigos;
   - el bloqueo por fuerza bruta;
   - la rotación de refresh tokens;
   - la revocación de sesiones;
   - las claves temporales;
   - el formulario de login.

   Cada pieza es un error de seguridad posible, y su impacto es mucho mayor que el de un error de negocio.
2. **ASP.NET Core Identity.** Trae hash de claves, TOTP y bloqueo. En contra: sus *bearer tokens* no son JWT ni OIDC estándar. Para que la SPA y la API hablen OIDC haría falta añadir OpenIddict o Duende IdentityServer (licencia comercial por encima de cierto tamaño), y además habría que construir las pantallas de login, inscripción del OTP y cambio de clave.
3. **Keycloak con un solo realm y dos clients.** Una sola configuración. En contra:
   - los tiempos de sesión del *client* no pueden superar los del realm, así que los 15 min / 9 h del operador forzarían al realm entero a esos valores, y la regla del cliente (5 min / 30 min) quedaría como excepción;
   - la credencial de servicio de la API necesita `manage-users`, y en un realm compartido podría tocar operadores y asignar el rol Operador a un cliente (escalada de privilegios);
   - la separación entre clientes y empleados sería una regla que vigilar en cada cambio, no una propiedad del sistema.
4. **Keycloak con dos realms, `clientes` y `empleados`.** Trae de serie MFA, claves temporales, bloqueo, refresh tokens con rotación, revocación de sesiones y OIDC. Cada realm tiene sus tiempos de sesión. La credencial de servicio vive solo en `clientes`. En contra: un componente más que operar, y los costos que lista "Consecuencias".

## Decisión

**Opción 4: Keycloak 26 en Docker con dos realms, `clientes` y `empleados`.** Su configuración (realm JSON de import y temas de login) está **versionada en el repositorio** (`infra/keycloak/`) y se levanta con `docker compose up -d`.

Reparto de responsabilidades:

- **Keycloak** autentica:
  - inicio y cierre de sesión;
  - segundo factor;
  - bloqueo;
  - clave temporal;
  - cambio de clave (`kc_action=UPDATE_PASSWORD` con `max_auth_age = 0`, para que siempre pida la actual);
  - tiempos de sesión.

  RF-14, RF-15, RN-21…RN-24, CL-01, CL-03, CL-10 y CL-11 se cumplen con **configuración**, no con código de la API.
- **La API** valida los tokens (ADR-0015), autoriza (ADR-0016) y gestiona el acceso digital de los clientes (RF-13) mediante la Admin REST API de Keycloak (ADR-0017).

**Credencial de servicio de la API:**

- un client confidencial `core-bancario-api-admin`, **solo en el realm `clientes`**, sin login de usuario, con cuenta de servicio y flujo *client credentials*;
- roles de `realm-management`: **`manage-users` y `view-users`**, nunca `realm-admin`. Con ellos basta para crear usuarios, poner claves temporales, borrar la credencial OTP, cambiar acciones obligatorias y atributos, y cerrar sesiones. La documentación de Keycloak indica que `manage-users` no permite asignar roles de administración que la propia cuenta no tenga. Además, en `clientes` no existe el rol Operador: no hay a quién ascender;
- el secreto vive en variables de entorno o user-secrets. En el realm JSON aparece solo el marcador `${KC_CLIENTES_API_SECRET}`, que Keycloak resuelve al importar. `.env.example` lo deja sin valor.

El motivo principal es que un emisor dedicado trae listas y probadas casi todas las funciones de identidad que pide la spec, y eso reduce el riesgo de construir un sistema de identidad propio. Los dos realms hacen que la separación entre clientes y empleados sea estructural.

## Consecuencias

**Lo que se gana:**

- MFA, bloqueo, rotación de refresh tokens, revocación de sesiones y OIDC sin código propio.
- La API valida **sin conexión** con JWKS (las claves públicas del realm): si Keycloak cae, quien ya tiene sesión sigue operando hasta que vence su token (≤ 5 min, ADR-0015). Lo que no funciona mientras Keycloak está caído es iniciar sesión, renovar y gestionar accesos (503).
- Los tiempos de sesión y las políticas de cada población se ajustan sin afectar a la otra.

**Lo que cuesta (aceptado):**

- **Tema de login propio**, por dos motivos:
  - Keycloak muestra "Account is disabled, contact your administrator." con un usuario deshabilitado y la clave correcta (hallazgo 1), y CA-08 exige el mensaje genérico;
  - Keycloak no normaliza el documento (hallazgo 2), y CA-12 exige aceptar `1.234.567`. El tema normaliza con JavaScript antes de enviar.
- **Pruebas de punta a punta lentas:** el primer arranque de Keycloak tardó unos 188 s. Se mitiga con cuatro niveles de pruebas, y solo uno arranca Keycloak (ADR-0018).
- **Dos sistemas que pueden desincronizarse** (el usuario en Keycloak y el vínculo y el registro en la base). Lo resuelve ADR-0017.
- **La API valida dos emisores** (ADR-0015) y **la SPA (004) tiene dos entradas de login**, una por realm.
- **Límite conocido:** en el login en dos pasos, la pantalla del OTP confirma que la clave era correcta (hallazgo 4). Lo mitiga el bloqueo, porque un OTP erróneo también cuenta como fallo (hallazgo 3).
- **Límite conocido (CL-05):** Keycloak 26 no permite que una clave temporal venza. "Expire password" obliga a cambiar la clave, pero no impide usarla. La clave temporal sirve hasta que el operador restablece o deshabilita el acceso. El autor lo aceptó y la spec quedó enmendada. Mejora futura: un proceso en segundo plano de la API que deshabilite los accesos con clave temporal de más de 72 h.
- **Cobertura parcial de "la clave no contiene el número de documento":** `notContainsUsername` solo rechaza la clave que contiene el usuario completo (`CC1234567`). El número suelto lo avisa el JavaScript del tema en las pantallas de cambio de clave. Ese aviso se puede saltar, y se acepta porque la regla protege al usuario de sí mismo, no frente a un atacante.
- **Configuración sensible a revisar en cada cambio** (la vigila una prueba que lee los realm JSON):
  - sin `offline_access`, porque crearía sesiones que no caducan;
  - sin consola de cuenta, porque permitiría a un usuario borrar su OTP;
  - sin concesión directa de usuario y clave;
  - scope `basic`, porque sin él el token no lleva `sub`;
  - mapper de audiencia.
- La imagen queda fijada en `quay.io/keycloak/keycloak:26.3.3`, la de los experimentos. Actualizarla es un cambio `chore` que vuelve a pasar las pruebas contra Keycloak.

**Mejoras futuras:** autenticación del client de servicio con `private_key_jwt` en vez de secreto compartido; passkeys (WebAuthn) contra el phishing en tiempo real; HTTPS de punta a punta con el despliegue (D-11).

## Decisiones del autor

Por qué Keycloak, en sus palabras: *"Elegí Keycloak porque incorpora de forma nativa la mayoría de los requisitos de autenticación del proyecto (MFA, claves temporales, bloqueo por fuerza bruta, refresh tokens con rotación, revocación de sesiones y OIDC), reduciendo el riesgo de construir un sistema de identidad propio. Acepto el coste de mantener un componente adicional porque los errores en autenticación tienen un impacto de seguridad mucho mayor que los errores habituales de negocio."*

Por qué dos realms, en sus palabras: *"permite configurar políticas de sesión distintas, garantiza el principio de menor privilegio para las credenciales técnicas de la API y hace que la separación entre identidades de clientes y empleados sea una propiedad estructural del sistema en lugar de una regla que deba validarse constantemente."*

Credencial de servicio: client credentials solo en `clientes`, con `manage-users` y `view-users`, por **menor privilegio**.

La decisión se tomó en tres rondas de revisión de la sección 10 de la spec, con dos experimentos ejecutados contra Keycloak 26.3.3.
