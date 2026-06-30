# AGENTS.md — greenfield-checkout

> Instrucciones para agentes IA (Copilot, Foundry Agents, background agents) que trabajen en este repo.
> Última revisión: 2026-06-30. Owner: pendiente de asignar en el demo.
> Consume catálogo común: [`cliente-ia-platform@v2026.06`](https://github.com/dpardora1/cliente-ia-platform/tree/v2026.06) — ver §6.bis.

## 1. Propósito del repo

API + SPA para el flujo de **checkout de excursiones opcionales** en touroperación. Subproyecto **greenfield** del demo de validación del playbook.

## 2. Tipo de proyecto

- **Tipo**: greenfield
- **Tipo de cliente**: simulado (regulado, turismo)
- **Estado**: en construcción
- **Base**: plantilla [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture)

## 3. Stack y convenciones

### 3.1 Tecnologías permitidas
- Lenguaje backend: C# (.NET 10)
- Framework: ASP.NET Core + Aspire
- Frontend: React 19 o Angular 21 (según generación)
- Tests: NUnit + Shouldly + Moq + Respawn
- DB: PostgreSQL (o la elegida en setup)
- ORM: Entity Framework Core 10
- CI/CD: GitHub Actions (pasable a Azure DevOps Pipelines en piloto cliente)

### 3.2 Anti-librerías (no usar sin ADR)
- MediatR fuera del bounded context Application.
- AutoMapper en bordes de API (usar DTOs explícitos).
- Cualquier librería con licencia GPL/AGPL.

### 3.3 Estructura de carpetas
- `src/Domain/` — entidades, value objects, eventos de dominio.
- `src/Application/` — casos de uso, validators, comandos/consultas.
- `src/Infrastructure/` — EF Core, integraciones externas.
- `src/Web/` — API y endpoints.
- `src/Web.Client/` o `src/Web.SPA/` — frontend.
- `tests/{Domain,Application,Infrastructure,Web}.UnitTests/`.
- `tests/Application.FunctionalTests/`.
- `docs/specs/`, `docs/adr/`, `docs/glosario.md` — **obligatorios**.

### 3.4 Convenciones
- Commits: Conventional Commits.
- PRs: título `SPEC-XXXX / #N - <descripción>` (donde `#N` es el número de Issue de este repo).
- Branches: `feature/SPEC-XXXX-<slug>`, `fix/issue-N-<slug>`.
- Tests verdes obligatorios para mergear a `main`.
- No introducir dependencias nuevas sin ADR en `docs/adr/`.

## 4. Cómo trabajar con specs

- Toda feature no trivial requiere SPEC en `docs/specs/`. Ver [plantilla](../../VECI_Methodology/playbook/anexos/B-plantilla-spec.md).
- Spec de referencia ya incluida: `docs/specs/SPEC-2026-0042-reserva-excursion-checkout.md`.
- Cada `Scenario` Gherkin de la spec se mapea a test funcional con nombre `Spec_<id>_<scenario_slug>`.
- Umbral: trabajo >2 días o que toque dominio/datos/seguridad → spec obligatoria.

## 5. Niveles de autonomía permitidos en este repo

| Tipo de tarea | Nivel máximo |
|---|---|
| Generación de docs/comentarios | N4 (background agent) |
| Generación de tests unitarios o funcionales | N4 |
| Implementación de feature con spec aprobada | N3 (agent mode supervisado) |
| Refactor mecánico (renombrados, extracciones) | N4 |
| Cambios en autenticación/autorización | N2 |
| Cambios en código que procesa datos personales | N2 |
| Migraciones EF Core que alteran esquema | N2 |
| Despliegue a PRO | N0 |

> Niveles definidos en [playbook/03-ciclo-vida-y-rituales.md](../../VECI_Methodology/playbook/03-ciclo-vida-y-rituales.md) §2.

## 6. MCP servers disponibles en el demo

| Capacidad | MCP | Permisos |
|---|---|---|
| GitHub Issues + PRs | mcp-github (o `gh` CLI por scripts) | read + comment + transition; no force-push, no delete |
| Repo + Git | nativo IDE | read + write rama feature; protección en `main` |
| Tests/build local | mcp-runner-local | execute |

> Cualquier MCP no listado: prohibido sin ADR.

## 6.bis Skills compartidas (catálogo externo)

Este repo **consume** skills del catálogo común publicado en [`dpardora1/cliente-ia-platform`](https://github.com/dpardora1/cliente-ia-platform/tree/v2026.06), versionado con el tag `v2026.06`.

- **Política** (ver [ADR-002 del catálogo](https://github.com/dpardora1/cliente-ia-platform/blob/v2026.06/adr/ADR-002-referencia-desde-demo.md)): consumir siempre por **URL absoluta + tag inmutable**, nunca por `main`. Promoción a una versión nueva del catálogo requiere PR en este repo que cambie el tag aquí declarado.
- **Skills disponibles en `v2026.06`** (resumen — la fuente de verdad está en el catálogo):

| Skill | Cuándo usarla | Frontmatter |
|---|---|---|
| [`slicing-assist`](https://github.com/dpardora1/cliente-ia-platform/blob/v2026.06/skills/slicing-assist.prompt.md) | Spec aprobada → plan de slices verticales | `SKILL-2026-0001`, `bootstrap: true` |
| [`revisar-pr-aware`](https://github.com/dpardora1/cliente-ia-platform/blob/v2026.06/skills/revisar-pr-aware.prompt.md) | Revisar PR con conciencia de spec + issue | `SKILL-2026-0002`, `bootstrap: true` |

- **Cómo invocar una skill**: el agente lee el `.prompt.md` referenciado por URL fija (tag pinneado). No copiar el contenido a este repo — eso fragmentaría el catálogo.
- **Skills marcadas `bootstrap: true`**: están en periodo de validación y pueden cambiar o desaparecer. No depender de ellas para flujos críticos hasta que pasen a `bootstrap: false`.

## 7. Glosario de dominio

Pendiente — el glosario aún no existe en el catálogo (deuda registrada para slice 3D / spec futura). Hasta entonces, los términos de dominio se definen inline en cada SPEC.

## 8. Patrones a seguir y a evitar

### Seguir
- Clean Architecture según la plantilla base. No mezclar capas.
- Eventos de dominio para integraciones asíncronas.
- DTOs explícitos en bordes; nunca exponer entidades de dominio en API.
- Validación con FluentValidation en `Application/`.
- Spec ↔ test funcional ↔ work item: enlace trazable.

### Evitar
- Microservicios por anticipación.
- Lógica de dominio en controllers o handlers HTTP.
- `catch (Exception ex) { }` silencioso.
- Strings mágicos en lugar de enums/options.

## 9. Cómo abrir un PR

1. Rama desde `main`.
2. Commits convencionales.
3. Tests verdes locales (`dotnet test`).
4. PR con título referenciando spec y work item.
5. CI verde antes de pedir review.
6. PR de background agent: etiqueta `agent:background` y dueño asignado.

## 10. Cómo NO hacer

- **No** mergear con CI rojo.
- **No** cambiar aserciones de tests para que pasen sin investigar.
- **No** introducir secrets en código (bloqueado por GHAS).
- **No** llamar a modelos IA fuera del tenant.
- **No** crear MCP custom sin aprobación.
- **No** silenciar tests rotos.

## 11. Contactos

- Pendiente de asignar (demo interno NTT DATA).

## 12. Referencias

- [Playbook](../../VECI_Methodology/playbook/README.md)
- [Cap. 08 — Validación en demo](../../VECI_Methodology/playbook/08-validacion-en-demo.md)
- [Catálogo de skills `cliente-ia-platform@v2026.06`](https://github.com/dpardora1/cliente-ia-platform/tree/v2026.06) — ver §6.bis
- [Plantilla Clean Architecture](https://github.com/jasontaylordev/CleanArchitecture)
