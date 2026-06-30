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

    /// <summary>
    /// SPEC-2026-0043 slice 2C — RN3 max_per_user. Counts Applied redemptions for
    /// (code, userId) that have NOT been cancelled by a later Released trace on the
    /// same reservation. A user that released a code can apply it again on a new
    /// reservation without consuming the per-user cap.
    /// </summary>
    Task<int> CountActiveAppliedByUserAsync(string code, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// SPEC-2026-0043 §8 — counts every redemption trace (Applied + Rejected + Released)
    /// for (reservationId, userId) created after <paramref name="since"/>. Used to enforce
    /// the brute-force protection of 10 attempts per (user, reservation) per 5 min.
    /// </summary>
    Task<int> CountAttemptsInWindowAsync(string reservationId, string userId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
