# PR Review — SPEC-2026-0043 (simulado)

> Trazabilidad de la revisión funcional y técnica del PR que aprobó la spec.
> En cliente real estos comentarios viven en el PR de Azure DevOps; aquí los materializamos en MD.

## PR

- **Título**: `SPEC-2026-0043 / AZDO-12346 - Aplicar codigo promocional`
- **Rama**: `feature/SPEC-2026-0043-aplicar-codigo-promocional`
- **Base**: `main`
- **Estado**: aprobado y mergeado

## Revisores

| Rol | Persona | Resultado |
|---|---|---|
| Owner funcional | @ana.perez@veci.example | Aprobado tras 1 round de cambios |
| Owner técnico | @carlos.lopez@nttdata.example | Aprobado tras 1 round de cambios |
| Champion IA | @dpardora@nttdata.example | Aprobado (revisión de trazabilidad IA) |

## Hilos resueltos

### Thread 1 — RN7 ambigua sobre conceptos no descontables
- **Autor**: Ana Pérez
- **Comentario**: "RN7 dice que el descuento no se aplica sobre tasas ni seguros obligatorios. ¿Y las propinas opcionales del guía? Estamos vendiéndolas como opción en otras specs, conviene fijar postura."
- **Acción**: Añadida nota a RN7 aclarando que propinas opcionales se incluyen en el subtotal descontable salvo que la promoción declare `exclude_optional_tips`. Cambio versionado.
- **Estado**: resolved.

### Thread 2 — Idempotencia del PUT
- **Autor**: Carlos López
- **Comentario**: "El endpoint `PUT /reservations/{id}/promo-code` debería ser idempotente. Si el cliente reenvía el mismo código por reintento de red, no debe disparar dos `PromoCodeRedeemed` ni doble cargo en el contador."
- **Acción**: Añadida nota en §7.1 sobre idempotencia por `(reservationId, code)` durante la ventana de pre-reserva. Cambio versionado.
- **Estado**: resolved.

### Thread 3 — Falta ADR de concurrencia
- **Autor**: Carlos López
- **Comentario**: "RN2 y el riesgo de concurrencia merecen un ADR antes de implementar. Anótalo como bloqueante para pasar a `implementing`."
- **Acción**: Marcado en §10 como ADR pendiente. No bloquea `approved` (la spec es contrato funcional); sí bloquea `implementing`.
- **Estado**: resolved con acción pendiente.

## Transiciones de estado

| Fecha | De | A | Quien |
|---|---|---|---|
| 2026-06-29 10:15 | (nuevo) | `draft` | autor (AI + dpardora) |
| 2026-06-29 11:00 | `draft` | `review` | autor |
| 2026-06-29 11:40 | `review` | `review` | (revisión, 3 hilos) |
| 2026-06-29 11:55 | `review` | `approved` | owner funcional + owner técnico |

## Checks de CI

- [x] Linter de spec: PASS (frontmatter válido, secciones obligatorias presentes, ≥1 `Scenario` por `Feature`).
- [x] Título de PR cumple patrón `SPEC-XXXX / AZDO-YYYYY - <desc>`.
- [x] Spec enlaza work item; work item enlaza spec.
- [ ] **Pendiente**: ADR de concurrencia (bloquea `implementing`, no `approved`).

> Nota: en este demo no hay pipeline real corriendo el linter — la verificación se hizo manualmente. Ver gap GAP-2026-0001 en `playbook/08-validacion-en-demo.md` §9.
