using greenfield_checkout.Domain.Enums;

namespace greenfield_checkout.Application.Promotions.Common.Models;

/// <summary>
/// Result DTO returned by ApplyPromoCode and consumed by the Web layer.
/// Translates domain rejection reasons to API-facing error codes (SPEC-2026-0043 §6).
/// </summary>
public sealed record ApplyPromoCodeResult(
    bool Success,
    decimal Subtotal,
    decimal Discount,
    decimal FinalPrice,
    string? ErrorCode)
{
    public static ApplyPromoCodeResult Applied(decimal subtotal, decimal discount) =>
        new(true, subtotal, discount, subtotal - discount, null);

    public static ApplyPromoCodeResult Rejected(decimal subtotal, string errorCode) =>
        new(false, subtotal, 0m, subtotal, errorCode);

    public static string ErrorCodeFor(PromoCodeRejectReason reason) => reason switch
    {
        PromoCodeRejectReason.NotFound          => "PROMO_NOT_FOUND",
        PromoCodeRejectReason.Expired           => "PROMO_EXPIRED",
        PromoCodeRejectReason.Exhausted         => "PROMO_EXHAUSTED",
        PromoCodeRejectReason.NotApplicable     => "PROMO_NOT_APPLICABLE",
        PromoCodeRejectReason.AlreadyUsed       => "PROMO_ALREADY_USED",
        PromoCodeRejectReason.ValidationTimeout => "PROMO_VALIDATION_TIMEOUT",
        _ => "PROMO_UNKNOWN"
    };
}
