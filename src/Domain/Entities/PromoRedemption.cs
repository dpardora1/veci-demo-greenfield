namespace greenfield_checkout.Domain.Entities;

public class PromoRedemption : BaseAuditableEntity
{
    public string Code { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string? ReservationId { get; set; }
    public RedemptionResult Result { get; set; }
    public PromoCodeRejectReason? Reason { get; set; }
    public decimal? AmountDiscounted { get; set; }
}
