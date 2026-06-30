using greenfield_checkout.Domain.Entities;

namespace greenfield_checkout.Application.Promotions.Common;

public interface IPromoCodeRepository
{
    Task<PromoCode?> GetAsync(string code, CancellationToken cancellationToken = default);
    Task SaveAsync(PromoCode promoCode, CancellationToken cancellationToken = default);
    Task AddRedemptionAsync(PromoRedemption redemption, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Applied redemption for (reservationId, code, userId) when no later
    /// Released trace cancels it. Used by ApplyPromoCode to satisfy idempotency
    /// (SPEC-2026-0043 §7.1 PUT note) within the 15-min pre-reservation window.
    /// </summary>
    Task<PromoRedemption?> GetActiveAppliedRedemptionAsync(string reservationId, string code, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the currently Applied redemption for (reservationId, userId) regardless of
    /// code, or null when none is active. Used by ReleasePromoCode (DELETE) which by RN8
    /// (stacking_policy = exclusive) assumes at most one active code per reservation.
    /// </summary>
    Task<PromoRedemption?> GetActiveAppliedRedemptionAsync(string reservationId, string userId, CancellationToken cancellationToken = default);
}
