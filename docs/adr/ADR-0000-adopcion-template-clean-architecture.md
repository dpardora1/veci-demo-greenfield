---
id: ADR-0000
title: Adopción del template "jasontaylordev/CleanArchitecture" v10.8.0 como base del proyecto
status: accepted
deciders: ["@dpardora1"]
date: 2026-06-29
related_specs: []
related_adrs: []
supersedes: null
superseded_by: null
tags: [arquitectura, greenfield, template, foundation]
ai_assisted: false
ai_model: null
---

# ADR-0000 — Adopción del template "jasontaylordev/CleanArchitecture" v10.8.0 como base del proyecto

## Contexto y problema

`veci-demo-greenfield` arranca como repositorio greenfield para validar el playbook VECI sobre un caso AI-native realista. Construir desde un scaffold mínimo (`dotnet new web`) obligaría a reinventar layering, DI, testing, CI, observabilidad y orquestación local antes de poder demostrar un slice vertical. Esto contradice la propuesta de valor del playbook (velocidad con calidad medible).

A la vez, adoptar un template sin auditar las decisiones que arrastra es exactamente lo que `GAP-2026-0011` del playbook (cap. 08) identifica como riesgo: el 60-70 % del trabajo arquitectónico queda decidido implícitamente.

Esta ADR cierra esa brecha: documenta de forma explícita las decisiones que el squad **hereda** del template, y obliga al cliente (o a la práctica, en este caso) a firmar o reemplazar cada una antes del slice 1.

## Decisión

Adoptamos **`jasontaylordev/CleanArchitecture`** versión **10.8.0** (<https://github.com/jasontaylordev/CleanArchitecture>) como base del proyecto, con el conjunto de decisiones heredadas detallado más abajo aceptado tal cual salvo en los puntos explícitamente marcados.

## Decisiones heredadas (checklist)

### Backend / dominio
- [x] **Aceptada** — Estilo arquitectónico: Clean Architecture en 4 capas (`Domain`, `Application`, `Infrastructure`, `Web`) + carpeta `tests/`.
- [x] **Aceptada** — ORM y motor de persistencia: EF Core 10 sobre PostgreSQL (provisionado vía Aspire en local).
- [x] **Aceptada** — Bus interno / mediador: **MediatR 14.1.0** para handlers de comandos y queries (`IRequestHandler`).
- [x] **Aceptada** — Validación: **FluentValidation 12.1.1** con pipeline behavior de MediatR (`ValidationBehaviour`).
- [x] **Aceptada** — Mapeo: AutoMapper del template, configurado por convención en cada `Application/<Feature>`.
- [x] **Aceptada** — Estilo de endpoints: Minimal APIs agrupadas por `IEndpointGroup` descubierto por reflection en `Web/Infrastructure`.
- [x] **Aceptada** — Auditoría de entidades: `BaseAuditableEntity` con `Created`, `CreatedBy`, `LastModified`, `LastModifiedBy` poblados por `AuditableEntityInterceptor`.
- [x] **Aceptada** — Domain events: in-process vía `DispatchDomainEventsInterceptor` en `SaveChangesAsync`. Si más adelante se necesita garantía transaccional con bus externo, ADR derivada con outbox pattern.

### Autenticación y autorización
- [x] **Aceptada** — Mecanismo: ASP.NET Identity con cookies (`AddIdentityCookies()`).
- [x] **Aceptada** — Gestión de usuarios: tabla `AspNetUsers` propia (Identity por defecto). Para un cliente real con SSO corporativo, esto exigiría ADR derivada.
- [x] **Aceptada (con caveat)** — Roles y permisos: claims-based con políticas declaradas en `Web/Infrastructure/ApiExceptionFilterAttribute` y atributos. **Caveat**: el demo no ejercita policy authorization de forma profunda; cualquier feature que toque autorización fina requerirá ADR específica.

### Frontend
- [x] **Aceptada** — Framework: React 19 con TypeScript.
- [x] **Aceptada** — Build tool: Vite.
- [ ] **A revisar** — Sistema de estilos: el template trae CSS plano. Para el demo no se aborda UI; si se añade capa visual, ADR derivada (Tailwind / MUI / etc.).
- [ ] **A revisar** — Cliente HTTP / fetching: pendiente cuando se implemente front real (TanStack Query o equivalente, probablemente).

### Infraestructura de desarrollo
- [x] **Aceptada** — Orquestación local: **.NET Aspire 13.2.0** (`src/AppHost`).
- [x] **Aceptada** — Gestión de paquetes: central package management (`Directory.Packages.props`) + SLNX (`greenfield-checkout.slnx`).
- [x] **Aceptada (con caveat)** — Política de warnings: `TreatWarningsAsErrors=true`. **Caveat resuelto en `GAP-2026-0009`**: `Directory.Build.props` extiende `WarningsNotAsErrors` con `NU1902;NU1903` para no bloquear builds por audit de NuGet transitivo mientras no haya pista de Dependabot/Renovate.

### Testing
- [x] **Aceptada** — Unit tests: **NUnit 4.5.1**.
- [x] **Aceptada** — Assertion library: **Shouldly 4.3.0**.
- [x] **Aceptada** — Mocking: **Moq 4.20.72**.
- [x] **Aceptada** — Tests funcionales / integración: **Respawn** para reset de DB entre tests (no ejercitado en slice 1; usado `InMemoryPromoCodeRepository`).
- [ ] **A revisar** — Tests E2E: el template no incluye. Si el demo crece a front + back integrado, ADR derivada (Playwright probablemente).

### CI/CD
- [x] **Aceptada** — Plataforma: **GitHub Actions**.
- [x] **Aceptada** — Branch protection: `main` requiere checks `Lint specs` + `Build and unit tests` + conversation resolution + no force-push (configurado por `scripts/configure-github-protection.ps1`).
- [ ] **A reemplazar antes de slice 2** — Política de actualizaciones de dependencias: **pendiente** habilitar Dependabot o Renovate. Bloqueante para considerar cerrado `GAP-2026-0009`.

### Observabilidad
- [x] **Aceptada (con caveat)** — Logging: estructurado por defecto del template (`Microsoft.Extensions.Logging`). Para un cliente con stack centralizado, ADR derivada (Serilog + sink).
- [ ] **A revisar** — Telemetría: el template trae OpenTelemetry en Aspire. Para producción real requiere ADR sobre backend (Application Insights, Datadog, Tempo/Loki, etc.).
- [ ] **A revisar** — Métricas y trazas: idem.

## Alternativas consideradas

### A — Construir desde scaffold mínimo (`dotnet new web` + `npm create vite`)
- Pros: cada decisión arquitectónica es explícita y auditada con su propia ADR desde el día 1.
- Contras: cuesta semanas de trabajo de fontanería que no aporta valor al demo; el agente IA no tiene patrones consolidados sobre los que generar; se reinventan convenciones bien resueltas por la comunidad.

### B — Adoptar `jasontaylordev/CleanArchitecture` tal cual sin esta ADR
- Pros: arranque inmediato.
- Contras: las 14+ decisiones heredadas viven implícitas; futuras auditorías de seguridad o cambios mayores no pueden trazar el "por qué" hasta ninguna parte; reproduce exactamente el riesgo de `GAP-2026-0011`.

### C — Adoptar el template **con esta ADR-0000 explícita y checklist firmado** *(la elegida)*
- Pros: arranque rápido + trazabilidad completa de decisiones; el template deja de ser una caja negra; cualquier desviación futura sabe contra qué se está desviando.
- Contras: añade el tiempo de redactar y firmar esta ADR. Coste despreciable comparado con A y mucho menor que el coste futuro de B.

## Consecuencias

### Positivas
- El squad y el agente IA arrancan con convenciones claras y compartidas desde el commit inicial.
- Las decisiones implícitas del template quedan **explícitas y auditadas**.
- Cualquier ADR futura del repo se puede situar en este marco (es la `ADR-0000`, todas las demás viven dentro).
- El demo deja de ser un riesgo metodológico: ahora muestra honestamente qué arrastra el template y qué construye el squad por encima.

### Negativas / coste
- Cualquier actualización mayor de `jasontaylordev/CleanArchitecture` (paso a v11.x, por ejemplo) obliga a revisar este checklist y abrir `ADR-NNNN — Actualización del template a vX.Y.Z` con `supersedes: ADR-0000`.
- Los ítems marcados "A reemplazar" o "A revisar" generan deuda explícita que el equipo debe cerrar antes del slice 2 (Dependabot/Renovate es el caso bloqueante real).

### Neutras
- El número `ADR-0000` queda reservado para esta decisión en este repo.

## Cumplimiento e impacto

- **Seguridad**: el bloque "Autenticación y autorización" está marcado "Aceptada (con caveat)". En un repo de cliente real, esta ADR no se firma sin el visto bueno del responsable de seguridad. En este demo, asume el riesgo `@dpardora1` con conocimiento del caveat.
- **Privacidad / GDPR**: PostgreSQL local en Aspire; no hay datos personales reales en el demo. Para producción de un cliente, ADR derivada sobre residencia.
- **Cumplimiento regulatorio**: no aplica al demo. Aplicaría a cualquier instanciación en cliente.
- **Operación**: Aspire fija cómo el equipo de SRE recibe el proyecto. Aceptado para el demo; en cliente puede no aplicar.
- **Coste**: licencia del template MIT; todas las dependencias open source compatibles.

## Plan de implementación

1. ✅ Esta ADR se firma como `accepted` con fecha 2026-06-29.
2. ✅ Primer commit del repo (anterior) referencia las decisiones del template; este commit añade la ADR retroactivamente para cerrar `GAP-2026-0011`.
3. **Pendiente (deuda registrada)**:
   - Habilitar Dependabot o Renovate (bloqueante para slice 2).
   - Si se introduce capa visual real, abrir ADR sobre sistema de estilos y cliente HTTP.
   - Si se introduce SSO corporativo, abrir ADR que `supersedes` la decisión de Identity con cookies.

## Validación

- Tras el slice 2, revisar si alguna de las decisiones marcadas "Aceptada" ha sido fuente de fricción no anticipada.
- En la conversación de cierre del demo (cap. 08 del playbook), confirmar que ningún hallazgo (`GAP-2026-NNNN`) nace de una decisión heredada **no documentada** en esta ADR. Si nace, la ADR es incompleta y se revisa.

## Referencias

- Template upstream: <https://github.com/jasontaylordev/CleanArchitecture>
- Versión base: `v10.8.0`
- Licencia: MIT
- Playbook VECI:
  - [anexo I — plantilla ADR-0000](https://github.com/pluaalentt/VECI_Methodology/blob/main/playbook/anexos/I-adr-template-base.md)
  - [anexo D — plantilla ADR general](https://github.com/pluaalentt/VECI_Methodology/blob/main/playbook/anexos/D-plantilla-adr.md)
  - [cap. 08 §9 — GAP-2026-0011](https://github.com/pluaalentt/VECI_Methodology/blob/main/playbook/08-validacion-en-demo.md)
- Hallazgos relacionados del demo:
  - `GAP-2026-0009` — `TreatWarningsAsErrors` + audit NuGet (mitigado en `Directory.Build.props`).
  - `GAP-2026-0010` — script de branch protection con binding posicional (open).
