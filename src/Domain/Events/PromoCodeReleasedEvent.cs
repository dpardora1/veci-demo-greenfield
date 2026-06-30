namespace greenfield_checkout.Domain.Events;

/// <summary>
/// SPEC-2026-0043 §7.2 — published when a previously applied promotional code is
/// released by the customer (DELETE on the reservation). The trace returns the slot
/// in the global redemption pool (RN2 / RN9).
/// </summary>
public sealed class PromoCodeReleasedEvent : BaseEvent
{
    public PromoCodeReleasedEvent(string code, string userId, string reservationId, DateTimeOffset occurredAt)
    {
        Code = code;
        UserId = userId;
        ReservationId = reservationId;
        OccurredAt = occurredAt;
    }

    public string Code { get; }
    public string UserId { get; }
    public string ReservationId { get; }
    public DateTimeOffset OccurredAt { get; }
}
