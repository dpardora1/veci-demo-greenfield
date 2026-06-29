# SPEC-2026-0043 / #1 — Slice 1: aplicar código promocional

Implementa el primer slice vertical del checkout de códigos promocionales, validando el flujo
spec → tests → código → CI sobre el repo real.

## Reglas cubiertas en este slice (SPEC-2026-0043 §4)

| Regla | Descripción | Implementación |
|-------|-------------|----------------|
| RN1   | Código vigente entre `valid_from` y `valid_to` (UTC) | `PromoCode.Evaluate` + test `RN1` |
| RN2   | `total_redemptions < max_total_redemptions` | `PromoCode.Evaluate` + `Consume()` + test `RN2` |
| RN5   | Un único `type` y `value` por código (invariante de construcción) | constructor de `PromoCode` (lanza si %∉(0,100]) + test `RN5` |
| RN6   | Si `type=Percentage` y existe `max_discount`, el descuento se tope | `PromoCode.Evaluate` rama % + test `RN6` |

## Escenarios Gherkin verificados

Cada test de `ApplyPromoCodeCommandHandlerTests` mapea 1:1 con un escenario de la spec §5:

- `Spec_SPEC_2026_0043_Scenario_codigo_porcentual_valido_aplicado` — happy path con `VERANO20`
- `Spec_SPEC_2026_0043_Scenario_codigo_caducado` — devuelve `PROMO_EXPIRED`
- `Spec_SPEC_2026_0043_Scenario_codigo_agotado` — devuelve `PROMO_EXHAUSTED`

## Fuera de scope (siguiente slice)

Documentado aquí para que el reviewer no espere encontrar estas piezas todavía:

- RN3 `max_per_user` (requiere repositorio de redemptions por user)
- RN4 destinos/tipos de excursión (depende de catálogo de excursiones)
- RN7 exclusión de propinas en el subtotal
- RN8 política de stacking (no aplicable a un solo código a la vez)
- RN9 ventana de 15 min de pre-reserva (depende del módulo de reservas)
- RN10 traza inmutable — sólo persistencia in-memory; el sink final será EF Core
- Endpoint `DELETE /promo-code` (libera el código y emite `PromoCodeReleased`)
- Idempotencia por `(reservationId, code)` en ventana de 15 min
- Rate limiting / redacción de PII en logs
- UI React, functional tests con Testcontainers, persistencia EF Core + migración

## Cambios en infraestructura

- **`Directory.Build.props`**: se han movido `NU1902` y `NU1903` a `WarningsNotAsErrors`.
  El template trataba todos los warnings como error y los audit findings transitivos de
  `MessagePack`, `OpenTelemetry.*` y `System.Security.Cryptography.Xml` rompían el build
  sin que el slice los introdujera. Quedan visibles como warnings; se tratarán por
  vía de actualizaciones de dependencias (gap separado).

- **`InMemoryPromoCodeRepository`**: implementación in-memory registrada como singleton.
  Será reemplazada por persistencia EF Core + PostgreSQL en el siguiente slice; el
  contrato `IPromoCodeRepository` está pensado para que esa sustitución no requiera
  cambios en `Application` ni en los tests.

- **Endpoint anónimo**: `PromoCodes` no añade `RequireAuthorization()` en este slice
  para facilitar el smoke test manual. El siguiente slice lo reactiva una vez se
  conecta el front-end con la cookie de Identity.

## CI

Se añade el workflow `.github/workflows/build-and-test.yml` (`Build and unit tests`),
que se ejecuta en PR y push a `main` cuando se tocan `src/**`, `tests/**`,
`*.csproj`, `*.slnx`, `Directory.Build.props` o `Directory.Packages.props`.

Tras el merge habrá que actualizar la branch protection para exigir este nuevo check
junto con `Lint specs` (script `configure-github-protection.ps1` en el repo de la
metodología).

## Tests

```text
Domain.UnitTests        — 10/10 passed (4 nuevos)
Application.UnitTests   — 11/11 passed (3 nuevos)
```

Cierra: dpardora1/veci-demo-greenfield#1
