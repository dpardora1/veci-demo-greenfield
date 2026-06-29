---
# Identificación
id: SPEC-2026-0042
title: Reserva de excursión opcional en checkout
slug: reserva-excursion-checkout
status: approved

# Ownership
owner_funcional: ana.perez@veci.example
owner_tecnico: carlos.lopez@nttdata.example

# Trazabilidad
work_item: AZDO-12345
related_specs: [SPEC-2026-0040]
related_adrs: [ADR-0007, ADR-0011]
superseded_by: null

# Metadatos
created: 2026-07-22
updated: 2026-07-28
tags: [touroperacion, checkout, excursiones]

# Trazabilidad IA
ai_assisted: true
ai_model: gpt-4.x
ai_use_id: AIUSE-2026-0007
---

# SPEC-2026-0042 — Reserva de excursión opcional en checkout

> Ejemplo de referencia que acompaña al [Anexo B — Plantilla de spec](../B-plantilla-spec.md).
> El dominio (touroperación) y los datos son ficticios; el cliente "VECI" referenciado es ilustrativo.

## 1. Contexto y problema

Hoy, durante el checkout de una reserva de paquete vacacional en la plataforma de VECI, el cliente final solo puede contratar el paquete base (hotel + vuelo + traslado). Las **excursiones opcionales** se ofrecen únicamente en mostrador en destino, lo que provoca:

- Pérdida de ingreso medio por reserva (estimado ~12% según informe interno VECI-RPT-2026-Q2).
- Sobrecarga del personal de destino.
- Mala experiencia para clientes que llegan sin saber qué excursiones están disponibles.

Negocio quiere ofrecer las excursiones del catálogo durante el checkout, con cupo en tiempo real y posibilidad de pago integrado.

El catálogo de excursiones ya existe en el sistema **MotorExcursiones** (ME, gestionado por el equipo Transversal). Esta spec cubre la integración desde **Checkout** (squad TTOO) hacia ME, y la persistencia de la pre-reserva mientras dura el flujo de pago.

## 2. Objetivo y no-objetivos

### Objetivo

Permitir que un cliente final añada una o varias excursiones opcionales a su reserva durante el checkout, con cupo bloqueado temporalmente (pre-reserva) hasta confirmar el pago, e incluyendo el importe de las excursiones en el total a cobrar.

### No-objetivos

- **NO** se implementa modificación de excursiones **post-confirmación** (cancelar/cambiar tras pagar). Esa funcionalidad queda para SPEC-2026-0050.
- **NO** se cubren paquetes con varios tramos (multi-destino). Solo destino único en esta versión.
- **NO** se integra con el motor de fidelización (puntos VECI Club). Queda para SPEC-2026-0055.
- **NO** se gestionan excursiones de proveedor externo no integrado en ME. Solo las del catálogo gestionado por ME.

## 3. Actores y stakeholders

| Actor | Rol en este flujo |
|---|---|
| Cliente final | Usuario que compra en la web/app de VECI. Añade/quita excursiones durante checkout. |
| Sistema Checkout | Front + BFF del flujo de pago (squad TTOO). |
| MotorExcursiones (ME) | Sistema transversal que gestiona catálogo, cupo y reservas de excursiones. |
| Pasarela de Pago (PP) | Servicio externo (Adyen) que procesa cobros. |
| Operador VECI | Recibe notificación de excursiones reservadas para coordinar destino. |
| Equipo de Soporte | Atiende incidencias si la pre-reserva expira o el pago falla. |

## 4. User stories

- Como **cliente final**, quiero **ver las excursiones disponibles en mi destino durante el checkout**, para **decidir si las añado antes de pagar**.
- Como **cliente final**, quiero que **el cupo de la excursión se bloquee mientras pago**, para **no perderla por una compra simultánea de otro cliente**.
- Como **cliente final**, quiero que **el total del checkout refleje el importe de las excursiones**, para **pagar todo en un único cobro**.
- Como **operador VECI**, quiero **recibir notificación inmediata cuando se confirme una excursión**, para **coordinar al proveedor de destino**.
- Como **soporte**, quiero **saber si una pre-reserva expiró sin pago**, para **liberar manualmente el cupo si fuera necesario**.

## 5. Reglas de negocio

1. **RN1 — Disponibilidad por destino**: solo se muestran excursiones cuyo `destino_id` coincide con el `destino_id` del paquete en checkout.
2. **RN2 — Cupo positivo**: una excursión se ofrece como disponible solo si `cupo_actual > 0` en el momento de la consulta.
3. **RN3 — Pre-reserva con TTL**: al añadir una excursión, el cupo queda bloqueado durante **15 minutos** (configurable por entorno, default 15). Pasado el TTL sin confirmación de pago, el cupo se libera automáticamente.
4. **RN4 — Una pre-reserva activa por (cliente, excursión)**: un mismo cliente no puede tener más de una pre-reserva activa para la misma excursión simultáneamente.
5. **RN5 — Suma al total**: el importe de la excursión se suma al total del checkout con la divisa de la reserva principal. Si la excursión está en divisa distinta, se convierte usando el tipo de cambio vigente al momento de la pre-reserva (referencia: SPEC-2026-0040, motor de divisas).
6. **RN6 — Cancelación durante checkout**: el cliente puede quitar una excursión del checkout antes de pagar; el cupo se libera inmediatamente.
7. **RN7 — Confirmación atómica con pago**: al confirmarse el pago en la pasarela, todas las pre-reservas activas del cliente para esta reserva se confirman atómicamente. Si una sola falla, **se reintenta** una vez; si vuelve a fallar, **se reembolsa** automáticamente el importe de las excursiones afectadas y se notifica al cliente. La reserva del paquete principal **no** se revierte.
8. **RN8 — Pago no confirmado**: si el cliente abandona el checkout o el pago se rechaza, las pre-reservas se liberan al expirar el TTL (no se libera anticipadamente para evitar *race conditions* con el callback de la pasarela).
9. **RN9 — Notificación al operador**: tras confirmación, se emite el evento `ExcursionReservaConfirmada` con (reserva_id, excursion_id, cliente_id, fecha_servicio, num_pasajeros, importe).
10. **RN10 — Auditoría**: cada transición de estado de pre-reserva (creada, confirmada, expirada, liberada manualmente, reembolsada) queda registrada con timestamp, actor (cliente_id o sistema) y motivo.

## 6. Criterios de aceptación (ejecutables)

```gherkin
Feature: Añadir excursión opcional en checkout

  Background:
    Given un cliente "C-001" con reserva "HTL-001" en estado "en_checkout"
    And la reserva "HTL-001" tiene destino "PARIS"
    And una excursión "EXC-PARIS-LOUVRE" con cupo 5 y precio 45 EUR
    And la reserva "HTL-001" está en divisa EUR

  # RN1, RN2
  Scenario: Listar excursiones del destino con cupo disponible
    When el cliente "C-001" consulta excursiones disponibles para "HTL-001"
    Then la respuesta incluye "EXC-PARIS-LOUVRE" con cupo 5 y precio 45 EUR
    And no incluye excursiones de otros destinos

  # RN1
  Scenario: No mostrar excursiones de otros destinos
    Given una excursión "EXC-ROMA-COLISEO" con destino "ROMA" y cupo 10
    When el cliente "C-001" consulta excursiones disponibles para "HTL-001"
    Then la respuesta NO incluye "EXC-ROMA-COLISEO"

  # RN2
  Scenario: Excursión sin cupo no aparece como disponible
    Given la excursión "EXC-PARIS-LOUVRE" tiene cupo 0
    When el cliente "C-001" consulta excursiones disponibles para "HTL-001"
    Then la respuesta NO incluye "EXC-PARIS-LOUVRE" como disponible

  # RN3, RN5
  Scenario: Añadir excursión con cupo disponible
    When el cliente "C-001" añade "EXC-PARIS-LOUVRE" al checkout "HTL-001"
    Then la operación es exitosa
    And se crea una pre-reserva con TTL de 15 minutos
    And el cupo de "EXC-PARIS-LOUVRE" pasa a 4
    And el total del checkout "HTL-001" se incrementa en 45 EUR
    And se emite el evento "ExcursionPreReserved" con (HTL-001, EXC-PARIS-LOUVRE, C-001, 15min)

  # RN2 — caso de carrera: cupo agotado entre consulta y add
  Scenario: Añadir excursión que se agotó entre consulta y add
    Given la excursión "EXC-PARIS-LOUVRE" tiene cupo 0
    When el cliente "C-001" intenta añadir "EXC-PARIS-LOUVRE" al checkout
    Then la operación falla con código "EXC-NO-AVAILABILITY"
    And no se modifica el total del checkout
    And no se emite ningún evento

  # RN4
  Scenario: Un cliente no puede tener dos pre-reservas activas para la misma excursión
    Given el cliente "C-001" ya tiene pre-reserva activa de "EXC-PARIS-LOUVRE" en checkout "HTL-001"
    When el cliente "C-001" intenta añadir "EXC-PARIS-LOUVRE" otra vez al mismo checkout
    Then la operación falla con código "EXC-DUPLICATE-PREORDER"
    And el cupo de "EXC-PARIS-LOUVRE" no cambia

  # RN6
  Scenario: Quitar excursión del checkout libera el cupo
    Given el cliente "C-001" añadió "EXC-PARIS-LOUVRE" al checkout (cupo ahora 4)
    When el cliente "C-001" quita "EXC-PARIS-LOUVRE" del checkout
    Then el cupo de "EXC-PARIS-LOUVRE" vuelve a 5
    And el total del checkout se decrementa en 45 EUR
    And la pre-reserva pasa a estado "liberada_por_cliente"

  # RN3, RN8
  Scenario: Pre-reserva expira sin confirmación de pago
    Given el cliente "C-001" añadió "EXC-PARIS-LOUVRE" al checkout hace 16 minutos
    And el cliente NO ha completado el pago
    When transcurren más de 15 minutos
    Then la pre-reserva pasa a estado "expirada"
    And el cupo de "EXC-PARIS-LOUVRE" vuelve a 5
    And se registra en auditoría la expiración con timestamp y motivo

  # RN7, RN9
  Scenario: Pago confirmado convierte pre-reserva en reserva confirmada
    Given el cliente "C-001" tiene pre-reserva activa de "EXC-PARIS-LOUVRE" en checkout "HTL-001"
    When la pasarela confirma el pago de "HTL-001"
    Then la pre-reserva pasa a estado "confirmada"
    And se emite el evento "ExcursionReservaConfirmada" con (HTL-001, EXC-PARIS-LOUVRE, C-001, fecha_servicio, 2, 45 EUR)
    And el cupo de "EXC-PARIS-LOUVRE" NO se restituye

  # RN7 — fallo de confirmación con reintento exitoso
  Scenario: Confirmación de pre-reserva falla pero reintento tiene éxito
    Given el cliente "C-001" tiene pre-reserva activa de "EXC-PARIS-LOUVRE"
    And la confirmación en ME fallará la primera vez con error transitorio
    When la pasarela confirma el pago de "HTL-001"
    Then el sistema reintenta la confirmación una vez
    And la pre-reserva pasa a estado "confirmada"
    And se emite "ExcursionReservaConfirmada"

  # RN7 — fallo persistente: reembolso parcial
  Scenario: Confirmación falla tras reintento, se reembolsa la excursión
    Given el cliente "C-001" pagó 245 EUR (200 paquete + 45 excursión)
    And la confirmación en ME falla persistentemente
    When transcurren ambos intentos de confirmación
    Then la reserva del paquete "HTL-001" permanece confirmada
    And la pre-reserva de "EXC-PARIS-LOUVRE" pasa a estado "fallida"
    And se emite reembolso de 45 EUR a través de la pasarela
    And se notifica al cliente con motivo "excursion-no-disponible-tras-pago"
    And se notifica a soporte con código "EXC-CONFIRM-FAILED"

  # RN5 — divisa distinta
  Scenario: Excursión en divisa distinta a la reserva
    Given la excursión "EXC-PARIS-LOUVRE" está en USD a 50 USD
    And la reserva "HTL-001" está en EUR
    And el tipo de cambio vigente es 1 USD = 0.92 EUR
    When el cliente "C-001" añade "EXC-PARIS-LOUVRE" al checkout
    Then el total del checkout se incrementa en 46 EUR
    And la pre-reserva queda registrada con (50 USD, tipo_cambio 0.92, 46 EUR)

  # RN10 — auditoría
  Scenario: Cada transición queda auditada
    Given el cliente "C-001" añade y quita "EXC-PARIS-LOUVRE" del checkout
    When se consulta la auditoría de la pre-reserva
    Then existen dos entradas: "creada" y "liberada_por_cliente"
    And cada entrada tiene timestamp, actor "C-001" y motivo
```

> Cada `Scenario` se mapea 1:1 con un test ejecutable nombrado `test_spec_2026_0042_<scenario_slug>`. Generación asistida por la skill [`mapear-spec-a-tests`](../E-prompts-y-skills.md#5-skill--mapear-spec-a-tests).

## 7. Contratos (API / eventos / datos)

### 7.1 APIs (extracto OpenAPI)

```yaml
# /contracts/checkout-excursiones.yaml (extracto)

paths:
  /checkouts/{checkoutId}/excursions/available:
    get:
      summary: Lista excursiones disponibles para el destino del checkout
      responses:
        "200":
          description: OK
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/AvailableExcursionsResponse"
        "404":
          description: Checkout no encontrado

  /checkouts/{checkoutId}/excursions:
    post:
      summary: Añade una pre-reserva de excursión al checkout
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: "#/components/schemas/AddExcursionRequest"
      responses:
        "201":
          description: Pre-reserva creada
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/PreReservationResponse"
        "409":
          description: |
            Conflicto. Códigos:
              - EXC-NO-AVAILABILITY
              - EXC-DUPLICATE-PREORDER

  /checkouts/{checkoutId}/excursions/{excursionId}:
    delete:
      summary: Libera una pre-reserva del checkout
      responses:
        "204":
          description: Pre-reserva liberada

components:
  schemas:
    AddExcursionRequest:
      type: object
      required: [excursionId, paxCount]
      properties:
        excursionId: { type: string }
        paxCount: { type: integer, minimum: 1, maximum: 20 }

    PreReservationResponse:
      type: object
      required: [preReservationId, expiresAt, amountCheckoutCurrency]
      properties:
        preReservationId: { type: string }
        expiresAt: { type: string, format: date-time }
        amountCheckoutCurrency:
          $ref: "#/components/schemas/MonetaryAmount"
        amountOriginalCurrency:
          $ref: "#/components/schemas/MonetaryAmount"
        exchangeRateUsed: { type: number, nullable: true }
```

### 7.2 Eventos

| Nombre | Productor | Consumidores | Cuándo |
|---|---|---|---|
| `ExcursionPreReserved` | Checkout | ME, Analítica | Al crearse pre-reserva. |
| `ExcursionReservaConfirmada` | Checkout | ME, NotificacionesOperador, Analítica | Tras pago confirmado y confirmación en ME OK. |
| `ExcursionPreReservaLiberada` | Checkout | ME, Analítica | Al liberarse (por cliente, expiración o fallo). |

Esquema (extracto):

```yaml
ExcursionReservaConfirmada:
  type: object
  required: [eventId, occurredAt, reservaId, excursionId, clienteId, fechaServicio, numPasajeros, importe]
  properties:
    eventId: { type: string, format: uuid }
    occurredAt: { type: string, format: date-time }
    reservaId: { type: string }
    excursionId: { type: string }
    clienteId: { type: string }
    fechaServicio: { type: string, format: date }
    numPasajeros: { type: integer, minimum: 1 }
    importe:
      $ref: "#/components/schemas/MonetaryAmount"
```

### 7.3 Modelo de datos

Nueva tabla `pre_reservas_excursion` en el esquema de Checkout:

| Columna | Tipo | Notas |
|---|---|---|
| id | uuid | PK |
| reserva_id | varchar | FK lógica a Reserva |
| cliente_id | varchar | |
| excursion_id | varchar | Referencia a ME |
| pax_count | int | |
| importe_original | decimal(10,2) | |
| divisa_original | char(3) | |
| importe_checkout | decimal(10,2) | |
| divisa_checkout | char(3) | |
| tipo_cambio | decimal(10,6) | nullable |
| estado | enum | `creada | confirmada | liberada_por_cliente | expirada | fallida` |
| creada_en | timestamptz | |
| expira_en | timestamptz | |
| actualizada_en | timestamptz | |

Índices: `(reserva_id, estado)`, `(expira_en) WHERE estado = 'creada'`.

Migración compatible hacia atrás: tabla nueva, sin impacto en datos existentes.

## 8. Consideraciones de seguridad, privacidad y cumplimiento

- **Datos personales tratados**: `cliente_id` (identificador interno, no PII directa). En eventos a operador se incluye nombre y email del titular (PII básica, mismo régimen que reserva principal).
- **Datos sensibles**: ninguno (no PCI: los datos de tarjeta los gestiona la pasarela).
- **Auth/Authz**:
  - Endpoints de checkout requieren sesión autenticada del cliente.
  - El cliente solo puede operar sobre su propia reserva (`reserva.cliente_id == sesión.cliente_id`).
  - Endpoints de soporte (consulta de pre-reservas) requieren rol `soporte:read`.
- **Auditoría**: todas las transiciones de estado se registran en `pre_reservas_excursion_audit` con retención 7 años (alineado con política VECI-POL-DAT-002).
- **Cumplimiento**:
  - GDPR: minimización aplicada; los eventos a operador no se persisten más allá de la prestación del servicio.
  - Directiva UE 2015/2302 (paquetes combinados): la excursión queda registrada como servicio adicional, no altera la naturaleza del paquete combinado original.
- **Inyección de prompts**: no aplica directamente; ningún endpoint de esta spec invoca a un LLM con input del cliente.

## 9. Plan de pruebas

- **Unit**: todas las reglas RN1–RN10 cubiertas por unit tests del dominio.
- **Contract**: contract test entre Checkout y ME para `cupo.consultar`, `preReserva.crear`, `preReserva.confirmar`, `preReserva.liberar`.
- **Integration**: con instancia ME en modo *contract-fake* + base de datos real.
- **E2E**: flujo completo "consultar → añadir → pagar → confirmar" sobre preview environment + ME real en sandbox.
- **No funcional**:
  - Latencia P95 < 300 ms para `GET /excursions/available`.
  - Concurrencia: 50 clientes intentando añadir la última unidad de cupo → solo 1 lo consigue, los demás reciben `EXC-NO-AVAILABILITY` correctamente.
  - Carga: 200 RPS sostenidos en preview environment.
- **Datos de prueba**: dataset sintético en `/tests/fixtures/excursiones.json` con catálogos por destino. No se usan datos reales de cliente.

## 10. Riesgos y supuestos

| ID | Riesgo | Impacto | Probabilidad | Mitigación |
|---|---|---|---|---|
| R1 | ME no soporta el contrato `preReserva.confirmar` con reintento idempotente | A | M | Validar contrato con squad Transversal en spike (1 día). Si no soporta, se añade *outbox* en Checkout. |
| R2 | Cupo en ME no es transaccional con el cobro de Checkout (consistencia eventual) | A | A | RN7 cubre el caso: reembolso parcial + notificación. Aceptado por negocio. |
| R3 | Tipos de cambio cachean valores obsoletos | M | M | TTL del cache 5 min + fallback a llamada síncrona. |
| R4 | Cliente abandona checkout pero la sesión sigue abierta, cupo bloqueado 15 min | B | A | TTL forzado; auditoría visible para soporte. |
| R5 | Doble click en "añadir" → duplica pre-reserva | M | A | RN4 cubre por dominio; front además aplica debounce. |
| R6 | Evento `ExcursionReservaConfirmada` se pierde | A | B | Outbox pattern + retry; ME es idempotente por `eventId`. |

| ID | Supuesto | Si no se cumple... |
|---|---|---|
| S1 | ME expone API REST documentada en su OpenAPI v3.2 | Bloquea desarrollo; escalado a equipo Transversal. |
| S2 | Pasarela Adyen emite callback fiable < 30s tras autorización | Aumenta riesgo de race con expiración TTL; revisar TTL. |
| S3 | El motor de divisas (SPEC-2026-0040) está disponible antes del kickoff | Mock temporal con tipos fijos hasta que esté listo. |
| S4 | Operador VECI tiene canal de notificación email configurado | Coordinar con OpsVECI antes de releasing. |

## 11. Trazabilidad

- **Origen**: work item [AZDO-12345](https://dev.azure.com/veci/_workitems/edit/12345)
- **Especificaciones relacionadas**:
  - [SPEC-2026-0040](../specs/SPEC-2026-0040-motor-divisas.md) — Motor de divisas (consumida en RN5).
- **ADRs relacionadas**:
  - [ADR-0007](../adr/ADR-0007-hexagonal-checkout.md) — Hexagonal Architecture en módulo de checkout.
  - [ADR-0011](../adr/ADR-0011-outbox-pattern-eventos.md) — Outbox pattern para eventos de dominio.
- **PRs de implementación**:
  - PR #1234 — Modelo de dominio + tests unit (RN1–RN6).
  - PR #1241 — Integración con ME + contract tests.
  - PR #1255 — Confirmación atómica y reembolso (RN7).
  - PR #1260 — E2E + preview.
- **Despliegue a PRO**: pendiente (release `R-2026.08.05`).

## 12. Historial de cambios

| Fecha | Estado | Autor | Resumen | PR |
|---|---|---|---|---|
| 2026-07-22 | draft | ana.perez (asistido por IA — skill `generar-spec-desde-workitem`) | Creación inicial desde AZDO-12345. Scenarios principales. | #1220 |
| 2026-07-24 | review | ana.perez | Completadas secciones 7, 8, 9 tras workshop con Transversal. | #1227 |
| 2026-07-25 | review | carlos.lopez | Añadidos RN7 (reintento+reembolso) y scenarios asociados tras descubrir caso con ME. | #1228 |
| 2026-07-26 | review | ana.perez + carlos.lopez | Revisión cruzada funcional+técnica. Aclaración RN3 (TTL configurable). | #1230 |
| 2026-07-28 | approved | ana.perez (funcional) + carlos.lopez (técnico) | Aprobación tras firma de ambos owners. CI verde. | #1230 |
