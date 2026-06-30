using greenfield_checkout.Domain.Events;
using Microsoft.Extensions.Logging;

namespace greenfield_checkout.Application.Promotions.EventHandlers;

/// <summary>
/// SPEC-2026-0043 §7.2 — bridges domain notification to logging/observability.
/// Slice 2B keeps it as a structured log entry; future slices may publish to
/// a message bus for the finance reconciliation pipeline (US4).
/// </summary>
public sealed class PromoCodeRedeemedEventHandler : INotificationHandler<PromoCodeRedeemedEvent>
{
    private readonly ILogger<PromoCodeRedeemedEventHandler> _logger;

    public PromoCodeRedeemedEventHandler(ILogger<PromoCodeRedeemedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PromoCodeRedeemedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PromoCodeRedeemed: code={Code} user={UserId} reservation={ReservationId} result={Result} reason={Reason} amount={Amount} at={Timestamp:o}",
            notification.Code,
            notification.UserId,
            notification.ReservationId,
            notification.Result,
            notification.Reason,
            notification.AmountDiscounted,
            notification.Timestamp);
        return Task.CompletedTask;
    }
}
