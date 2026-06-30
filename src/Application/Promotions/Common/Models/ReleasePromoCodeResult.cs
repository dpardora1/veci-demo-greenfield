namespace greenfield_checkout.Application.Promotions.Common.Models;

/// <summary>
/// SPEC-2026-0043 §7.1 DELETE — outcome of releasing a promo code from a reservation.
/// </summary>
public sealed record ReleasePromoCodeResult(bool Released, string? Code)
{
    public static ReleasePromoCodeResult Success(string code) => new(true, code);
    public static ReleasePromoCodeResult NotFound() => new(false, null);
}
