---
# Identificación
id: SPEC-2026-0045
title: Plataforma IA mínima del cliente (cliente-ia-platform)
slug: platform-ia-minima
status: approved

# Ownership
owner_funcional: dpardora@nttdata.example
owner_tecnico: dpardora@nttdata.example

# Trazabilidad
work_item: dpardora1/veci-demo-greenfield#30
related_specs: [SPEC-2026-0042, SPEC-2026-0043]
related_adrs: []
superseded_by: null

# Metadatos
created: 2026-06-30
updated: 2026-06-30
tags: [plataforma, ia, catalogo, skills, governance]

# Trazabilidad IA
ai_assisted: true
ai_model: claude-opus-4.7
ai_use_id: AIUSE-2026-0009
---

# SPEC-2026-0045 — Plataforma IA mínima del cliente (`cliente-ia-platform`)

## 1. Contexto y problema

Hasta el slice 2C, el demo greenfield ha validado el ciclo `specs → slicing → agente → PR → main` con un **agente IDE-side** (Copilot) operado en cada turno por una persona. Funciona, pero deja sin validar dos de las tres patas del modelo propuesto en el playbook (cap. 05 §3 y anexo C):

- **Pata 2** — un **catálogo común del cliente** (`cliente-ia-platform/`) con skills, plantillas e instrucciones versionadas, reutilizables entre squads.
- **Pata 3** — el rol de **champion IA** que promueve skills locales al catálogo (cap. 05 §5).

Sin la pata 2 materializada, cada squad reinventa prompts y la ventaja compuesta de la metodología se pierde. La demo, tal cual hoy, valida un tercio del modelo y predica los otros dos tercios. Registrado como [GAP-2026-0018](../../../../VECI_Methodology/playbook/08-validacion-en-demo.md#gap-2026-0018) en el playbook.

Esta spec materializa una **versión mínima viable** del catálogo y la integra con el demo.

## 2. Objetivo y no-objetivos

### Objetivo

- Crear `cliente-ia-platform/` como **repo git independiente, hermano de `greenfield-checkout/`**, replicando la topología esperada en cliente real (un repo de plataforma compartido por N squads). Decisión firmada en sesión 2026-06-30, formalizada en ADR-002.
- Materializar **2-3 skills mínimas reales** (no plantillas vacías) extraídas del trabajo que el operador ya viene haciendo a mano en slices 1-2C.
- Integrar el catálogo con el `AGENTS.md` del demo de forma que los slices 3+ consuman skills **desde el catálogo**, no improvisadas.
- Documentar el formato de skill en un ADR para que la promoción de skills locales → catálogo sea reproducible.

### No-objetivo

- Construir un agente autónomo (loop agentivo / MCP server). Se trata como **Caso D** en una spec posterior, una vez esta esté en `main`.
- Multi-tenant del catálogo (varios squads consumiendo). Se valida con un solo squad (el del propio demo).
- Hosting del catálogo en plataforma del cliente (Foundry, Azure ML, etc.). El catálogo vive como repo git plano; la integración con plataformas se difiere.
- Migrar las skills de Copilot a otro proveedor de modelo. El formato debe ser portable (.prompt.md / .instructions.md estándar), pero la validación se hace con el modelo actual.

## 3. Actores y stakeholders

- **Operador del demo** (rol dev + champion): mantiene el catálogo y consume sus skills.
- **PO metodológico** (NTT DATA): valida que la estructura del catálogo es enseñable a un cliente.
- **Futuros squads** (hipotéticos): consumirán el catálogo cuando el demo se replique en otro contexto.

## 4. User stories

- **US1**: Como operador del demo, quiero que las skills que uso en cada slice estén versionadas en un repo, para no reinventarlas y para que un compañero pueda usarlas igual.
- **US2**: Como PO metodológico, quiero poder enseñar a un cliente la carpeta `cliente-ia-platform/` con contenido real, no una plantilla vacía.
- **US3**: Como champion IA (rol futuro en squad cliente), quiero un ADR que describa el formato de skill para promover una skill local al catálogo siguiendo un proceso reproducible.
- **US4**: Como operador del slice 3+, quiero que el `AGENTS.md` del demo cargue automáticamente las skills del catálogo, sin que tenga que copiar contenido a mano.

## 5. Reglas de negocio (de plataforma)

| ID | Regla |
|---|---|
| RN1 | El catálogo vive como **repo git independiente** (hermano de `greenfield-checkout/` en la misma organización GitHub). Tiene su propio `README.md` con propósito, governance y cómo contribuir. La decisión hermano vs subcarpeta queda firmada en ADR-002. |
| RN2 | Toda skill del catálogo es un fichero `.prompt.md` o `.instructions.md` con frontmatter YAML obligatorio (`id`, `title`, `version`, `owner`, `last_validated`, `applies_to`). |
| RN3 | Una skill solo entra al catálogo si está **ejecutada con éxito en al menos 2 contextos reales distintos** del demo (no se promueven skills hipotéticas). |
| RN4 | El `AGENTS.md` del demo referencia las skills del catálogo por **path relativo + versión**, no copia su contenido. |
| RN5 | Cada skill tiene un **owner** humano nombrado (en el demo: `dpardora`). Sin owner, la skill se marca `deprecated` en 30 días. |
| RN6 | Cambios al catálogo siguen el mismo ciclo metodológico que el código: spec/issue de slice → PR → revisión humana → merge (no se editan skills directamente en `main`). |
| RN7 | El formato de skill se fija en un **ADR-001** del catálogo. Cambios al formato son cambios de ADR (no de skill individual). |
| RN8 | Toda skill declara `applies_to` con los stacks/contextos donde se ha validado (`dotnet-10`, `clean-architecture`, `ef-core`, etc.). Una skill sin `applies_to` no se usa. |

## 6. Criterios de aceptación (ejecutables)

```gherkin
Feature: Plataforma IA mínima del cliente
  Como operador del demo y futuro champion IA
  Quiero un catálogo común versionado con skills reutilizables
  Para validar la pata 2 del modelo de transformación (catálogo común)
  Y para que los slices 3+ consuman skills del catálogo, no improvisadas

  Scenario: El catálogo existe y es navegable
    Dado que el operador clona la organización del demo
    Cuando lista los repos
    Entonces existe un repo "cliente-ia-platform" hermano de "greenfield-checkout"
    Y al clonarlo, contiene un README.md con secciones "Propósito", "Cómo contribuir", "Governance"
    Y contiene un subdirectorio "skills/" con al menos 2 skills versionadas
    Y contiene un subdirectorio "adr/" con ADR-001 (formato de skill) y ADR-002 (ubicación)

  Scenario: Una skill mínima cumple el formato (RN2)
    Dado el fichero "skills/slicing-assist.prompt.md" en el repo cliente-ia-platform
    Cuando se valida su frontmatter
    Entonces tiene id que coincide con "SKILL-YYYY-NNNN"
    Y tiene version semver
    Y tiene owner con email
    Y tiene applies_to con al menos un valor
    Y tiene last_validated con fecha ISO-8601
    Y el cuerpo contiene secciones "Inputs esperados", "Salida esperada", "Ejemplo"

  Scenario: El AGENTS.md del demo consume el catálogo (RN4)
    Dado que el demo tiene un AGENTS.md
    Cuando se inspecciona su sección "Skills del catálogo"
    Entonces referencia al menos 2 skills por path relativo y versión
    Y no duplica el contenido de las skills en el propio AGENTS.md

  Scenario: Skill rechazada por falta de evidencia (RN3)
    Dado un candidato a skill usado solo una vez por el operador
    Cuando se intenta abrir PR para incorporarlo al catálogo
    Entonces la PR es rechazada con motivo "RN3: requiere ≥2 ejecuciones reales documentadas"

  Scenario: Skill aceptada con evidencia (RN3)
    Dado un candidato a skill usado 2 veces en slices distintos con evidencia de PRs
    Cuando se abre PR para incorporarlo al catálogo
    Entonces la PR es aprobable
    Y al mergearse, queda en "skills/" con frontmatter completo

  Scenario: ADR-001 fija el formato de skill (RN7)
    Dado el fichero "adr/ADR-001-formato-skill.md" en el repo cliente-ia-platform
    Cuando se lee
    Entonces declara el frontmatter YAML obligatorio (RN2)
    Y declara el esqueleto del cuerpo (Inputs, Salida, Ejemplo)
    Y declara el proceso de cambio de formato

  Scenario: Skill sin owner se marca deprecated (RN5)
    Dada una skill cuyo owner se vacía
    Y han pasado 30 días sin nuevo owner asignado
    Cuando se ejecuta el check de governance
    Entonces la skill se marca con "deprecated: true" y "deprecated_reason"
    Y deja de cargarse desde AGENTS.md
```

## 7. Contratos (API / eventos / datos)

No aplica API HTTP — el catálogo es un repo git plano. Contratos relevantes:

- **Formato de skill** (fijado en ADR-001 del propio catálogo): frontmatter YAML obligatorio con `id`, `title`, `version` (semver), `owner`, `applies_to` (lista), `last_validated` (ISO-8601), `deprecated` (bool, default false), `deprecated_reason` (opcional).
- **Cuerpo de skill**: secciones obligatorias `Inputs esperados`, `Salida esperada`, `Ejemplo`.
- **Referencia desde AGENTS.md**: `cliente-ia-platform/skills/<slug>.prompt.md@<version>` por URL raíz del repo hermano (ej. `https://github.com/<org>/cliente-ia-platform/blob/v0.1.0/skills/slicing-assist.prompt.md`) o por submodule/git subtree si se decide en ADR-002.
- **Issues de slice** (Modelo B): título `Slice 3X — <descripción>`, body con campos `spec_origen: SPEC-2026-0045`, `reglas_cubiertas: [...]`, `escenarios_cubiertos: [...]`, `deferred: [...]`.

## 8. Consideraciones de seguridad, privacidad y cumplimiento

- **Datos sensibles**: las skills no deben contener credenciales, endpoints internos del cliente ni datos personales. Linter del catálogo (futuro) debe detectar patrones `sk-…`, `Bearer …`, GUIDs sospechosos, dominios `*.internal.*`.
- **Propiedad intelectual**: las skills producidas durante la demo son propiedad del proyecto demo (NTT DATA). Una skill basada en proceso interno del cliente real requeriría revisión de IP antes de promoción al catálogo.
- **Cumplimiento IA**: cada skill que oriente a un agente a generar código sujeto a regulación (GDPR, PCI-DSS, AI Act) debe declararlo en `applies_to` con el tag de la regulación. Se valida en revisión humana, no automatizable en este slice.
- **Auditoría**: el historial git del catálogo es la traza. No se requiere log adicional.

## 9. Plan de pruebas

- **Estructura**: tests manuales en revisión de PR de cada slice 3A-3C (existencia de directorios, frontmatter válido).
- **Linter de skills**: opcional para esta iteración. Si se materializa, sería un `tools/lint-skill.ps1` análogo a `tools/lint-spec.ps1`. Difiere hasta tener ≥3 skills en el catálogo.
- **Integración con AGENTS.md**: validar manualmente que un agente que lee AGENTS.md puede seguir las referencias y aplicar la skill (smoke test al iniciar slice 4 cualquiera).
- **Regresión metodológica**: tras el merge del slice 3C, validar que NO se ha vuelto a improvisar prompts en slice 4 (revisión PO/champion).

## 10. Riesgos y supuestos

**Supuestos**:

- Las skills versionadas en `.prompt.md` siguen siendo consumibles por el agente IDE (Copilot) sin tooling adicional. Validado parcialmente porque Copilot ya consume `AGENTS.md` y archivos `.instructions.md`.
- El demo unipersonal puede actuar como "champion IA" para fines de validación. Se documenta en la presentación que en cliente real este rol no recae en el operador.

**Riesgos**:

- **R1**: Bootstrapping rompe RN3 (≥2 usos reales). Mitigación: marcar las primeras skills como `bootstrap: true` con plan explícito de segundo uso en slice 4. Documentar como excepción en ADR-001.
- **R2**: Sobreingeniería del catálogo (anexar linter de skills, registro de versiones, etc.) bloquea el resto de la demo. Mitigación: hard cap de 4 slices (3A-3D), si no entra, queda como deuda.
- **R3**: Las skills extraídas resultan demasiado específicas del demo y no son reusables en cliente real. Mitigación: revisión PO al final del slice 3C — si las skills son demo-specific, se documenta como "ejemplo, no template" en el README del catálogo.
- **R4**: Modelo B de issues no se sigue rigurosamente (volver a Modelo A por ergonomía). Mitigación: plantilla `.github/ISSUE_TEMPLATE/slice.md` obligatoria desde slice 3A.

## 11. Trazabilidad

- **Cierra parcialmente** [GAP-2026-0018](../../../../VECI_Methodology/playbook/08-validacion-en-demo.md#gap-2026-0018): fases 1 y 2 del plan. La fase 3 (Caso D — agente autónomo) queda fuera de scope de esta spec.
- **Aplica** Modelo B de issues por slice [GAP-2026-0016](../../../../VECI_Methodology/playbook/08-validacion-en-demo.md#gap-2026-0016).
- **Requiere** gate de revisión humana ≥1 [GAP-2026-0015](../../../../VECI_Methodology/playbook/08-validacion-en-demo.md#gap-2026-0015) — confirmado en sesión 2026-06-30 que la demo sigue operando con `required_approving_review_count = 0` mientras sea unipersonal. Se subirá a `1` cuando entre el segundo perfil.
- **Referencia** playbook cap. 05 §3 (Catálogo común del cliente), cap. 05 §5 (Champions IA), anexo C (plantilla AGENTS.md).
- **Specs relacionadas**: SPEC-2026-0042, SPEC-2026-0043 (consumirán skills del catálogo desde slice 4 en adelante).
- **ADRs derivados**: ADR-002 (mecanismo de referencia entre el demo y el repo hermano `cliente-ia-platform`: URL absoluta vs submodule vs subtree) se redacta en slice 3A.

## 12. Historial de cambios

| Versión | Fecha | Autor | Cambio |
|---|---|---|---|
| 0.1.0 | 2026-06-30 | dpardora | Versión inicial. Status `draft`. |
| 0.2.0 | 2026-06-30 | dpardora | Decidido repo hermano (no subcarpeta). RN1, escenarios y plan de slicing 3A actualizados. ADR-002 cambia de "dónde" a "cómo se referencia desde el demo". |
| 0.3.0 | 2026-06-30 | dpardora | Status `approved`. Aprobada en modo unipersonal (review_count=0) según opción 2 documentada en sesión 2026-06-30. Habilita apertura de slice 3A. |

