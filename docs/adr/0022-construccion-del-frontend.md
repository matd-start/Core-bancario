# ADR-0022: Cómo se construye el frontend

- Estado: Aceptada
- Fecha: 2026-10-26
- Feature: 004-front-base (resuelve D-09 de la spec de producto; repositorio `core-bancario-web`, ADR-0005)

## Contexto

La 004 crea `core-bancario-web`: una SPA en React + TypeScript (spec de producto, sección "Frontend") con dos áreas. La spec de la 004 pide:

- tipos generados desde el contrato OpenAPI versionado, con TypeScript estricto (RNF-13);
- estado del servidor en caché con estados de carga, error y vacío (CA-18);
- formularios validados en el navegador con las reglas de la 002 (CA-23);
- componentes accesibles WCAG 2.1 AA, con diálogos que no se cierran por accidente (CL-17, RNF-08);
- montos exactos (CL-14);
- pruebas automáticas de cada CA (RNF-14), dos recorridos en un navegador real contra Keycloak real (RNF-15), una revisión de accesibilidad automática (CA-35) y pruebas de sesión sin esperar 5 ni 30 min.

El roadmap ya nombra TanStack Query (S3) y React Hook Form con esquemas (S6). La sesión la lleva el BFF (ADR-0020 y ADR-0021), así que la SPA **no usa ninguna librería OIDC**.

Conceptos para leer esta ADR:

- **Estado del servidor frente a estado de la interfaz:** los saldos y las fichas pertenecen a la API, y la SPA solo tiene una copia que puede quedar vieja. TanStack Query gestiona esa copia: la pide, la guarda, la refresca y dice si está cargando o falló. Analogía: un tablero de vuelos. No es tuyo, lo consultas y se actualiza solo.
- **Validación por esquemas:** se describe la forma válida de un formulario una vez (por ejemplo, "teléfono: `+` y de 8 a 15 dígitos") y de ahí salen los mensajes y el tipo TypeScript.
- **Componentes *headless* accesibles:** librerías que dan el comportamiento accesible (foco, teclado, roles ARIA) sin imponer estilos.

## Opciones consideradas

| Pieza | Opciones | Elegida y por qué |
|---|---|---|
| Build | **Vite** · Next.js · Create React App (abandonado) | **Vite 8.3.5** + `@vitejs/plugin-react` 6.1.2. Una SPA servida estática, sin SSR. Next.js trae servidor y SSR que la arquitectura no usa: el servidor ya es el BFF |
| Lenguaje | TypeScript 5.9 · 6.0 · 7.0 (compilador nativo) | **TypeScript 5.9.3** en modo `strict`, más `noUncheckedIndexedAccess` y `exactOptionalPropertyTypes`. `openapi-typescript` 7.13.0 declara `typescript ^5.x` como *peer*, y TS 7 aún no tiene una API programática estable para esas herramientas. Se revisa cuando el generador amplíe su rango |
| Ruteo | **React Router** · TanStack Router | **React Router 8.5.0**, en modo de librería, sin *framework mode* ni SSR. Es el estándar más extendido y basta para siete rutas. TanStack Router (1.170.x) da rutas tipadas, pero añade otra API que aprender sin un requisito que lo pida |
| Estado del servidor | **TanStack Query** · SWR · `useEffect` a mano | **TanStack Query 5.105.0** (roadmap). Consultas con 1 reintento salvo en 401/403/404, y **mutaciones con `retry: 0`** (CL-15: nunca reintenta sola una acción que cambia datos) |
| Formularios | **React Hook Form + Zod** · Formik · a mano | **React Hook Form 7.91.0 + `@hookform/resolvers` 6.0.0 + Zod 4.6.7** (roadmap S6). Los errores 400 de la API (`errors` por campo) se vuelcan con `setError` en el mismo campo (CA-23) |
| Cliente OpenAPI | **openapi-typescript + openapi-fetch** · `@hey-api/openapi-ts` (0.99, aún sin 1.0) · Orval | **openapi-typescript 7.13.0** (solo tipos) **+ openapi-fetch 0.17.0** (un `fetch` tipado de pocos KB, con *middleware*). El *middleware* pone `X-CSRF: 1` (ADR-0020) y convierte cada 401 en "sesión terminada". Se generan tipos de dos contratos: el de la API y el del BFF |
| Componentes | **React Aria Components** · Radix UI · MUI | **React Aria Components 1.22.1** (Adobe). Es *headless* y con accesibilidad probada. Su `Modal` tiene `isDismissable` (por defecto `false`) e `isKeyboardDismissDisabled`, justo lo que pide el diálogo de la clave temporal (CL-17). Estilos con CSS Modules de Vite, sin framework de CSS |
| Dinero | `Intl.NumberFormat` con **texto** · librería decimal | **`Intl.NumberFormat('es-CO', …).format(monto)` pasando el texto de `DineroDto.monto`.** Desde ES2023 (Intl.NumberFormat v3), `format` acepta una cadena decimal y la formatea **sin convertirla a `number`**, así que `12345678901234.56` sale exacto. COP con 0 decimales y USD con 2. Nunca se usa `Number`, `parseFloat` ni el `+` unario |
| Pruebas unitarias y de componentes | **Vitest** · Jest | **Vitest 5.0.5 + jsdom 30.1.3 + Testing Library (`react` 16.3.3, `user-event` 14.6.8) + MSW 3.0.3**, que simula la API en la red sin tocar el código |
| Navegador real | **Playwright** · Cypress | **Playwright 1.64.1** con Chromium en el CI (RNF-10), con dos proyectos: **simulado** (`page.route` sustituye `/bff` y `/api`, y el **reloj de Playwright** adelanta el tiempo para el aviso de sesión) y **real** (compose completo con Keycloak). Accesibilidad con **`@axe-core/playwright` 4.14.0** (etiquetas `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`). TOTP con **`otpauth` 9.5.2** (MIT) a partir del secreto que Keycloak muestra al inscribir el segundo factor, como Otp.NET en Keycloak.Tests (ADR-0018) |
| Runtime | Node 22 · **Node 24** | **Node 24 LTS** (Vite 8 pide `^20.19.0 \|\| >=22.12.0`), fijado en `.nvmrc` y en el CI |

Lint y formato (ESLint con typescript-eslint 8.72.1 y Prettier) los fija `chore/kit-web` con la skill `ts-clean-code`. No son parte de esta decisión.

## Decisión

La tabla anterior, con estas reglas de uso:

- **Contratos:** `contratos/core-bancario-api.json` y `contratos/core-bancario-bff.json` son copias versionadas en el repositorio web de los archivos de ADR-0019 y ADR-0020. Un script (`npm run contratos:sincronizar`) las copia desde `../core-bancario`, y `npm run tipos` genera `src/api/generado/*.d.ts`, que también se versionan. El CI falla si los tipos no corresponden al contrato o si el contrato copiado difiere del de `core-bancario` en `main`.
- **Ningún dato sensible en el navegador:** nada en `localStorage` ni en `sessionStorage`. El número de documento no aparece en las rutas. La clave temporal vive solo en el estado del diálogo, y su mutación se borra (`reset()`) al cerrarlo.
- **Errores (RNF-12):** un único `interpretarError(estado, cuerpo)` traduce `codigo` → mensaje con un `Record<CodigoDeError, string>`, donde `CodigoDeError` es el *enum* que publica el contrato. Si falta un mensaje, **no compila**. Nunca se muestra `detail`.

El motivo principal es usar las piezas más estándar de cada categoría que cumplen los requisitos de accesibilidad, exactitud y seguridad, sin añadir capas (SSR, librería OIDC, framework de CSS) que la arquitectura no necesita.

## Consecuencias

- La SPA es estática (HTML, JS y CSS). En compose y en el CI la sirve nginx, que también aplica la CSP (RNF-03). En desarrollo la sirve Vite **sin CSP**, porque la recarga en caliente usa scripts en línea. La CSP se verifica en los recorridos reales.
- `Intl` depende de los datos de ICU del navegador. El espacio entre el símbolo y el número es un espacio duro (U+00A0), y las pruebas lo comparan como tal. CA-19 se comprueba también dentro del navegador real.
- Las versiones quedan fijadas en `package.json` y `package-lock.json`. Actualizarlas es un `chore` que vuelve a pasar todas las pruebas.
- TypeScript queda en la línea 5.9 hasta que `openapi-typescript` acepte 6 o 7. Es un riesgo de obsolescencia, vigilado en el plan.

## Decisiones del autor

El roadmap ya había nombrado TanStack Query y React Hook Form. **Enrutador: React Router** (2026-10-26), elegido frente a TanStack Router porque es el más extendido y basta para unas siete rutas. Las rutas tipadas de TanStack Router no responden a ningún requisito. El resto lo confirma al aprobar el plan.
