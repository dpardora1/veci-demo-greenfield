using greenfield_checkout.Application.Common.Interfaces;
using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Application.Promotions.Common.Models;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using greenfield_checkout.Domain.Events;

namespace greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;

/// <summary>
/// SPEC-2026-0043 — Apply a promotional code to a reservation in pre-reservation state.
/// Slice 1: validates RN1/RN2/RN5/RN6 and emits a redemption trace (RN10 partial).
/// Slice 2A: persisted via EF Core + PostgreSQL.
/// Slice 2B: idempotent by (reservationId, code, userId) within the pre-reservation
/// window (SPEC §7.1 PUT note), and publishes PromoCodeRedeemedEvent on every outcome
/// (RN10 full: applied and rejected redemptions both notify downstream consumers).
/// Slice 2+ pending: RN3 max_per_user, RN4 destinations, RN7 tips, RN8 stacking, RN9 slot, rate limit.
/// </summary>
public sealed record ApplyPromoCodeCommand : IRequest<ApplyPromoCodeResult>
{
    public string ReservationId { get; init; } = default!;
    public string Code { get; init; } = default!;
    public decimal Subtotal { get; init; }
}

public sealed class ApplyPromoCodeCommandHandler : IRequestHandler<ApplyPromoCodeCommand, ApplyPromoCodeResult>
{
    private readonly IPromoCodeRepository _repository;
    private readonly IUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ApplyPromoCodeCommandHandler(
        IPromoCodeRepository repository,
        IUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApplyPromoCodeResult> Handle(ApplyPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? "anonymous";
        var now = _timeProvider.GetUtcNow();

        // Slice 2B — idempotency: a retry with the same (reservation, code, user) that
        // is still Applied returns the prior result without consuming a new slot or
        // emitting a duplicate trace.
        var alreadyApplied = await _repository.GetActiveAppliedRedemptionAsync(
            request.ReservationId, request.Code, userId, cancellationToken);
        if (alreadyApplied is not null)
        {
            return ApplyPromoCodeResult.Applied(request.Subtotal, alreadyApplied.AmountDiscounted ?? 0m);
        }

        var promo = await _repository.GetAsync(request.Code, cancellationToken);
        if (promo is null)
        {
            await PersistRejectionAsync(
                request.Code, userId, request.ReservationId, PromoCodeRejectReason.NotFound, now, cancellationToken);
            return ApplyPromoCodeResult.Rejected(request.Subtotal, ApplyPromoCodeResult.ErrorCodeFor(PromoCodeRejectReason.NotFound));
        }

        var evaluation = promo.Evaluate(request.Subtotal, now);
        if (!evaluation.Success)
        {
            await PersistRejectionAsync(
                promo.Code, userId, request.ReservationId, evaluation.Reason!.Value, now, cancellationToken);
            return ApplyPromoCodeResult.Rejected(request.Subtotal, ApplyPromoCodeResult.ErrorCodeFor(evaluation.Reason!.Value));
        }

        promo.Consume();
        await _repository.SaveAsync(promo, cancellationToken);

        var applied = new PromoRedemption
        {
            Code = promo.Code,
            UserId = userId,
            ReservationId = request.ReservationId,
            Result = RedemptionResult.Applied,
            AmountDiscounted = evaluation.Discount
        };
        applied.AddDomainEvent(new PromoCodeRedeemedEvent(
            promo.Code, userId, request.ReservationId,
            RedemptionResult.Applied, reason: null, amountDiscounted: evaluation.Discount, timestamp: now));
        await _repository.AddRedemptionAsync(applied, cancellationToken);

        return ApplyPromoCodeResult.Applied(request.Subtotal, evaluation.Discount);
    }

    private async Task PersistRejectionAsync(
        string code,
        string userId,
        string reservationId,
        PromoCodeRejectReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rejected = new PromoRedemption
        {
            Code = code,
            UserId = userId,
            ReservationId = reservationId,
            Result = RedemptionResult.Rejected,
            Reason = reason
        };
        rejected.AddDomainEvent(new PromoCodeRedeemedEvent(
            code, userId, reservationId,
            RedemptionResult.Rejected, reason, amountDiscounted: null, timestamp: now));
        await _repository.AddRedemptionAsync(rejected, cancellationToken);
    }
}
