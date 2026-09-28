# ADR-0005: Repositorios separados para backend y frontend

- Estado: Aceptada
- Fecha: 2026-09-28
- Feature: transversal (resuelve D-10 de la spec)

## Contexto

El sistema tiene un backend .NET y un frontend React + TypeScript. Hay que decidir si viven en el mismo repositorio. El kit SDD de este repositorio está preparado para .NET; el frontend necesita otras herramientas, otros comandos y otra skill de principios (`ts-clean-code`).

## Opciones consideradas

1. **Monorepo.** Una feature de punta a punta en un solo pull request, un solo CI y un solo `docker compose up`. En contra: el kit SDD tendría que manejar dos stacks a la vez y el CI se vuelve más complejo.
2. **Repositorios separados.** Cada repositorio con su stack, su CI y su versión del kit SDD, y cada uno se despliega por separado. En contra: una feature completa se reparte en dos pull requests y el contrato entre ambos tiene que mantenerse explícito.

## Decisión

Repositorios separados: `core-bancario` (este, backend .NET) y `core-bancario-web` (frontend, se crea en el Sprint 3). El motivo principal es que cada repositorio tenga un kit SDD y un CI simples y enfocados en su stack.

## Consecuencias

- El contrato entre ambos es el documento OpenAPI que publica la API; el frontend genera sus tipos a partir de él. Un cambio de contrato se hace primero en el backend.
- La spec de producto vive aquí, en `docs/producto/`; el frontend la referencia.
- Para levantar todo el sistema hará falta un `docker compose` que combine ambos; se resuelve en el Sprint 3.
- Una feature de punta a punta requiere coordinar dos ramas y dos pull requests.
