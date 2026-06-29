namespace greenfield_checkout.Domain.Events;

public sealed class PromoCodeRedeemedEvent : BaseEvent
{
    public string Code { get; }
    public string UserId { get; }
    public string? ReservationId { get; }
    public RedemptionResult Result { get; }
    public PromoCodeRejectReason? Reason { get; }
    public decimal? AmountDiscounted { get; }
    public DateTimeOffset Timestamp { get; }

    public PromoCodeRedeemedEvent(
        string code,
        string userId,
        string? reservationId,
        RedemptionResult result,
        PromoCodeRejectReason? reason,
        decimal? amountDiscounted,
        DateTimeOffset timestamp)
    {
        Code = code;
        UserId = userId;
        ReservationId = reservationId;
        Result = result;
        Reason = reason;
        AmountDiscounted = amountDiscounted;
        Timestamp = timestamp;
    }
}
