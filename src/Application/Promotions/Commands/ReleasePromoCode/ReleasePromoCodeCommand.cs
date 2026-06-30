using greenfield_checkout.Application.Common.Interfaces;
using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Application.Promotions.Common.Models;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using greenfield_checkout.Domain.Events;

namespace greenfield_checkout.Application.Promotions.Commands.ReleasePromoCode;

/// <summary>
/// SPEC-2026-0043 slice 2B — releases the currently applied promo code on a reservation
/// (DELETE /api/PromoCodes/{reservationId}). By RN8 (stacking_policy = exclusive) there
/// is at most one Applied redemption per (reservationId, userId) at any time, so the
/// command does not need the code in the payload.
/// </summary>
public sealed record ReleasePromoCodeCommand : IRequest<ReleasePromoCodeResult>
{
    public string ReservationId { get; init; } = default!;
}

public sealed class ReleasePromoCodeCommandHandler : IRequestHandler<ReleasePromoCodeCommand, ReleasePromoCodeResult>
{
    private readonly IPromoCodeRepository _repository;
    private readonly IUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReleasePromoCodeCommandHandler(
        IPromoCodeRepository repository,
        IUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ReleasePromoCodeResult> Handle(ReleasePromoCodeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? "anonymous";

        var active = await _repository.GetActiveAppliedRedemptionAsync(
            request.ReservationId, userId, cancellationToken);
        if (active is null)
            return ReleasePromoCodeResult.NotFound();

        var promo = await _repository.GetAsync(active.Code, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Inconsistent state: redemption {active.Id} references missing PromoCode '{active.Code}'.");

        promo.Release();
        await _repository.SaveAsync(promo, cancellationToken);

        var released = new PromoRedemption
        {
            Code = active.Code,
            UserId = userId,
            ReservationId = request.ReservationId,
            Result = RedemptionResult.Released
        };
        released.AddDomainEvent(new PromoCodeReleasedEvent(
            active.Code, userId, request.ReservationId, _timeProvider.GetUtcNow()));
        await _repository.AddRedemptionAsync(released, cancellationToken);

        return ReleasePromoCodeResult.Success(active.Code);
    }
}
