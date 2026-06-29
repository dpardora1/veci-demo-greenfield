---
# Identificación
id: SPEC-2026-0043
title: Aplicar código promocional al checkout de excursiones
slug: aplicar-codigo-promocional
status: approved

# Ownership
owner_funcional: ana.perez@veci.example
owner_tecnico: carlos.lopez@nttdata.example

# Trazabilidad
work_item: dpardora1/veci-demo-greenfield#1
related_specs: [SPEC-2026-0042]
related_adrs: []
superseded_by: null

# Metadatos
created: 2026-06-29
updated: 2026-06-29
tags: [touroperacion, checkout, promociones]

# Trazabilidad IA
ai_assisted: true
ai_model: claude-opus-4.7
ai_use_id: AIUSE-2026-0008
---

# SPEC-2026-0043 — Aplicar código promocional al checkout de excursiones

## 1. Contexto y problema

Hoy el flujo de checkout de excursiones opcionales (`SPEC-2026-0042`) permite reservar pero **no admite descuentos**. Negocio ejecuta campañas con códigos promocionales repartidos por canales (email, partners) cuya redención hoy es **manual**: el agente de call center ajusta el precio a mano, lo que genera fricción y errores de reconciliación contable.

Se requiere automatizar la **validación y aplicación** del código durante el checkout, manteniendo trazabilidad para auditoría legal y reclamaciones.

## 2. Objetivo y no-objetivos

### Objetivo

- Permitir al cliente introducir un código promocional durante el checkout y ver el descuento aplicado **antes** de confirmar la reserva.
- Trazar cada uso de código (válido o inválido) para auditoría.
- No degradar el tiempo de respuesta del checkout (<300 ms p95 para validar el código).

### No-objetivo

- Gestión administrativa de códigos (alta, baja, edición). Va en spec aparte (futura `SPEC-2026-0044`).
- Notificaciones marketing del uso del código.
- Reglas de pricing más allá del descuento (fidelización, upsell, etc.).

## 3. Actores y stakeholders

- **Cliente final**: introduce y ve el código.
- **PO touroperación**: dueña funcional.
- **Equipo financiero**: consume las trazas para reconciliación.
- **Compliance**: revisa cumplimiento de UE 2015/2302 (paquete dinámico) y GDPR (trazas).

## 4. User stories

- **US1**: Como cliente, quiero introducir un código durante el checkout para que se me aplique el descuento.
- **US2**: Como cliente, quiero ver claramente cuánto ahorro y el precio final antes de confirmar.
- **US3**: Como cliente, quiero poder quitar el código si me equivoco.
- **US4**: Como equipo financiero, quiero un evento por cada redención para conciliar facturación.

## 5. Reglas de negocio

| ID | Regla |
|---|---|
| RN1 | Un código solo es aplicable si está **vigente** (rango fecha actual ∈ [`valid_from`, `valid_to`]). |
| RN2 | Un código tiene un **límite global** de canjeos (`max_total_redemptions`). Al alcanzarse, queda agotado. |
| RN3 | Un código tiene un **límite por usuario** (`max_per_user`, default 1). |
| RN4 | Un código puede restringir **destinos** y/o **tipos de excursión**. Si la reserva no encaja, no aplica. |
| RN5 | El descuento puede ser **porcentual** (`percentage`) o **importe fijo** (`fixed_amount`), nunca ambos. |
| RN6 | Si es porcentual, puede tener **tope absoluto** (`max_discount_amount`). |
| RN7 | El descuento se aplica sobre el **subtotal de excursiones** (incluidas propinas opcionales del guía), nunca sobre tasas ni seguros obligatorios. Si el código declara `exclude_optional_tips`, se excluyen las propinas del cálculo. |
| RN8 | Códigos del mismo tipo no se apilan; políticas de mezcla entre tipos definidas en `stacking_policy` (`exclusive` por defecto). |
| RN9 | Un código consumido en una pre-reserva **bloquea** uno de los slots por 15 min (alineado con `SPEC-2026-0042` RN3). Si la pre-reserva expira, se libera. |
| RN10 | Toda redención (incluso fallida) deja **traza inmutable** con `user_id`, `code`, `result`, `timestamp`, `reservation_id?`. |

## 6. Criterios de aceptación (ejecutables)

```gherkin
Feature: Aplicar código promocional al checkout
  Como cliente VECI en checkout
  Quiero aplicar un código promocional
  Para obtener el descuento correspondiente antes de confirmar

  Background:
    Given existe la reserva en estado "pre-reservada" con subtotal 200.00 EUR
    And el cliente está autenticado como "user-123"

  Scenario: Código porcentual válido aplicado correctamente
    Given existe el código "VERANO20" con descuento 20% y vigencia activa
    When el cliente introduce el código "VERANO20"
    Then la respuesta es 200 OK
    And el descuento mostrado es 40.00 EUR
    And el precio final es 160.00 EUR
    And se publica un evento "PromoCodeRedeemed" con code "VERANO20" y result "applied"

  Scenario: Código fijo válido con tope superior al subtotal
    Given existe el código "FIJO50" con descuento fijo 50.00 EUR
    When el cliente introduce el código "FIJO50"
    Then la respuesta es 200 OK
    And el descuento aplicado es 50.00 EUR
    And el precio final es 150.00 EUR

  Scenario: Código caducado
    Given existe el código "PRIMAVERA" con vigencia hasta 2026-05-31
    When el cliente introduce el código "PRIMAVERA"
    Then la respuesta es 422 Unprocessable Entity
    And el código de error es "PROMO_EXPIRED"
    And el precio final sigue siendo 200.00 EUR
    And se publica un evento "PromoCodeRedeemed" con result "rejected" y reason "expired"

  Scenario: Código agotado
    Given existe el código "AGOTADO" con max_total_redemptions = 100 y total_redemptions = 100
    When el cliente introduce el código "AGOTADO"
    Then la respuesta es 422 Unprocessable Entity
    And el código de error es "PROMO_EXHAUSTED"

  Scenario: Código no aplicable al destino
    Given existe el código "CANARIAS10" restringido al destino "Canarias"
    And la reserva es para el destino "Baleares"
    When el cliente introduce el código "CANARIAS10"
    Then la respuesta es 422 Unprocessable Entity
    And el código de error es "PROMO_NOT_APPLICABLE"

  Scenario: Usuario ya redimió el código antes
    Given existe el código "BIENVENIDA" con max_per_user = 1
    And el cliente "user-123" ya redimió "BIENVENIDA" en la reserva R-9000
    When el cliente introduce el código "BIENVENIDA"
    Then la respuesta es 422 Unprocessable Entity
    And el código de error es "PROMO_ALREADY_USED"

  Scenario: Eliminar código aplicado
    Given el cliente había aplicado "VERANO20" con éxito
    When el cliente quita el código de la reserva
    Then la respuesta es 200 OK
    And el precio final vuelve a 200.00 EUR
    And se publica un evento "PromoCodeReleased" con code "VERANO20"

  Scenario: Código inexistente
    When el cliente introduce el código "NOEXISTE"
    Then la respuesta es 422 Unprocessable Entity
    And el código de error es "PROMO_NOT_FOUND"
    And el log no contiene el valor "NOEXISTE" en claro (PII redaction)

  Scenario: Timeout en validación (degradación elegante)
    Given el servicio de promociones tarda más de 300 ms en responder
    When el cliente introduce el código "VERANO20"
    Then la respuesta es 503 Service Unavailable
    And el código de error es "PROMO_VALIDATION_TIMEOUT"
    And el precio final sigue siendo 200.00 EUR

  Scenario: Tope de descuento porcentual
    Given existe el código "MEGA50" con descuento 50% y max_discount_amount = 30.00 EUR
    When el cliente introduce el código "MEGA50"
    Then el descuento aplicado es 30.00 EUR (tope)
    And el precio final es 170.00 EUR
```

## 7. Contratos (API / eventos / datos)

### 7.1 API REST

```yaml
# excerpt OpenAPI 3.1
paths:
  /reservations/{reservationId}/promo-code:
    put:
      operationId: applyPromoCode
      summary: Aplica un código promocional a una reserva en pre-reserva.
      description: |
        Idempotente por (reservationId, code) dentro de la ventana de pre-reserva (15 min).
        Un reintento con el mismo cuerpo no genera un nuevo PromoCodeRedeemed ni incrementa total_redemptions más de una vez.
      parameters:
        - name: reservationId
          in: path
          required: true
          schema: { type: string }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [code]
              properties:
                code:
                  type: string
                  minLength: 3
                  maxLength: 32
                  pattern: "^[A-Z0-9_-]+$"
      responses:
        "200":
          description: Código aplicado, devuelve precio recalculado.
          content:
            application/json:
              schema: { $ref: "#/components/schemas/ReservationPricing" }
        "422":
          description: Código no aplicable. Body con code y reason.
        "503":
          description: Timeout en validación.
    delete:
      operationId: removePromoCode
      summary: Quita el código aplicado a la reserva.
      parameters:
        - { name: reservationId, in: path, required: true, schema: { type: string } }
      responses:
        "200": { description: Código eliminado, precio recalculado. }
```

### 7.2 Eventos de dominio

| Evento | Cuándo | Carga relevante |
|---|---|---|
| `PromoCodeRedeemed` | Validación finalizada (aplicada o rechazada) | `reservationId`, `userId`, `code`, `result`, `reason?`, `amountDiscounted?`, `timestamp` |
| `PromoCodeReleased` | Cliente quita código aplicado | `reservationId`, `userId`, `code`, `timestamp` |

### 7.3 Modelo de datos (Promotions BC, esquema lógico)

```
promo_code(
  code              TEXT PRIMARY KEY,
  type              TEXT,                          -- 'percentage' | 'fixed'
  value             NUMERIC(10,2),
  max_discount      NUMERIC(10,2) NULL,
  valid_from        TIMESTAMPTZ,
  valid_to          TIMESTAMPTZ,
  max_total_redemptions INTEGER,
  total_redemptions     INTEGER DEFAULT 0,
  max_per_user      INTEGER DEFAULT 1,
  destinations      TEXT[] NULL,
  excursion_types   TEXT[] NULL,
  stacking_policy   TEXT DEFAULT 'exclusive'
)

promo_redemption(
  id                BIGSERIAL PRIMARY KEY,
  code              TEXT REFERENCES promo_code(code),
  user_id           TEXT,
  reservation_id    TEXT NULL,
  result            TEXT,                          -- 'applied' | 'rejected' | 'released'
  reason            TEXT NULL,
  amount_discounted NUMERIC(10,2) NULL,
  created_at        TIMESTAMPTZ DEFAULT now()
)
```

## 8. Consideraciones de seguridad, privacidad y cumplimiento

- **Rate limiting**: máx. 10 intentos por usuario y reserva en 5 min, para evitar fuerza bruta sobre el espacio de códigos.
- **PII**: `user_id` se almacena hasheado o pseudonimizado en `promo_redemption` salvo requisito contable explícito (decisión pendiente en ADR-0014).
- **Logs**: los códigos rechazados no se loguean en claro (riesgo de filtración de inventario promocional).
- **Cumplimiento**: las redenciones son **traza inmutable** para reclamaciones (Directiva UE 2015/2302 sobre paquetes turísticos).
- **GDPR**: la traza tiene retención de 6 años (justificada por reclamación legal). Política referenciada en ADR pendiente.

## 9. Plan de pruebas

| Nivel | Qué se prueba | Herramienta |
|---|---|---|
| Unit | Reglas RN1–RN10 sobre `PromoCode` aggregate | NUnit + Shouldly |
| Application | Comandos `ApplyPromoCode` / `RemovePromoCode` con repos en memoria | NUnit + FluentValidation |
| Integración | Endpoints `/promo-code` con EF Core + PostgreSQL Testcontainers | Application.FunctionalTests |
| Contrato | OpenAPI snapshot vs implementación | Spectral en CI |
| E2E SPA | Flujo cliente: aplicar / quitar código | Playwright |
| Performance | p95 < 300 ms para `PUT /promo-code` con 50 RPS | k6 (off-CI, scheduled) |
| Seguridad | Rate limit por usuario; PII redaction en logs | Tests dedicados |

## 10. Riesgos y supuestos

| Tipo | Descripción | Mitigación |
|---|---|---|
| Riesgo | Concurrencia en `total_redemptions` puede sobrepasar el tope | `UPDATE ... WHERE total_redemptions < max_total_redemptions` atómico; ver ADR pendiente |
| Riesgo | Códigos brute-forceables si son cortos | Rate limit + códigos de mínimo 6 chars en política administrativa (otra spec) |
| Riesgo | Cambio del precio base mientras el código está aplicado | Recalcular siempre al confirmar reserva |
| Supuesto | Existe un servicio/módulo `Promotions` o se creará en este sprint | Sí, se crea como módulo nuevo dentro de Application |
| Supuesto | El catálogo de promociones se carga vía seed inicial para el demo | Sí |

## 11. Trazabilidad

| Elemento | Referencia |
|---|---|
| Work item | [dpardora1/veci-demo-greenfield#1](https://github.com/dpardora1/veci-demo-greenfield/issues/1) |
| Spec relacionada | `SPEC-2026-0042` — Reserva de excursión opcional en checkout |
| ADRs pendientes | retención PII en redenciones, control de concurrencia atómica en canjeos |
| Tests | `tests/Application.FunctionalTests/PromoCodes/*` (a crear), `tests/Domain.UnitTests/Promotions/*` (a crear) |
| Implementación | `src/Application/Promotions/*`, `src/Domain/Promotions/*` (a crear) |

## 12. Historial de cambios

| Fecha | Autor | Cambio |
|---|---|---|
| 2026-06-29 | claude-opus-4.7 (AI) + dpardora (humano) | Versión inicial `draft` generada desde Issue #1 en ejecución del caso C1 del playbook. |
| 2026-06-29 | ana.perez + carlos.lopez | Cambios derivados de revisión (RN7 propinas, idempotencia PUT). Status `draft` → `review` → `approved`. Ver `reviews/SPEC-2026-0043-review.md`. |
| 2026-06-29 | dpardora | Migrado work item simulado `AZDO-12346` a Issue real `#1` en GitHub. Refs actualizadas. |
