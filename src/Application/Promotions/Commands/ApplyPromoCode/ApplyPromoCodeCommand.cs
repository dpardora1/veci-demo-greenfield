using greenfield_checkout.Application.Common.Interfaces;
using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Application.Promotions.Common.Models;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;

namespace greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;

/// <summary>
/// SPEC-2026-0043 — Apply a promotional code to a reservation in pre-reservation state.
/// Slice 1: validates RN1/RN2/RN5/RN6 and emits a redemption trace (RN10 partial).
/// Slice 2+ pending: RN3 max_per_user, RN4 destinations, RN7 tips, RN8 stacking, RN9 slot, idempotency.
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

        var promo = await _repository.GetAsync(request.Code, cancellationToken);
        if (promo is null)
        {
            await _repository.AddRedemptionAsync(new PromoRedemption
            {
                Code = request.Code,
                UserId = userId,
                ReservationId = request.ReservationId,
                Result = RedemptionResult.Rejected,
                Reason = PromoCodeRejectReason.NotFound
            }, cancellationToken);

            return ApplyPromoCodeResult.Rejected(request.Subtotal, ApplyPromoCodeResult.ErrorCodeFor(PromoCodeRejectReason.NotFound));
        }

        var evaluation = promo.Evaluate(request.Subtotal, now);
        if (!evaluation.Success)
        {
            await _repository.AddRedemptionAsync(new PromoRedemption
            {
                Code = promo.Code,
                UserId = userId,
                ReservationId = request.ReservationId,
                Result = RedemptionResult.Rejected,
                Reason = evaluation.Reason
            }, cancellationToken);

            return ApplyPromoCodeResult.Rejected(request.Subtotal, ApplyPromoCodeResult.ErrorCodeFor(evaluation.Reason!.Value));
        }

        promo.Consume();
        await _repository.SaveAsync(promo, cancellationToken);
        await _repository.AddRedemptionAsync(new PromoRedemption
        {
            Code = promo.Code,
            UserId = userId,
            ReservationId = request.ReservationId,
            Result = RedemptionResult.Applied,
            AmountDiscounted = evaluation.Discount
        }, cancellationToken);

        return ApplyPromoCodeResult.Applied(request.Subtotal, evaluation.Discount);
    }
}
