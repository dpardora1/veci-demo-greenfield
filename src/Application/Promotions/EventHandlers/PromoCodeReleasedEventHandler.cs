using greenfield_checkout.Domain.Events;
using Microsoft.Extensions.Logging;

namespace greenfield_checkout.Application.Promotions.EventHandlers;

public sealed class PromoCodeReleasedEventHandler : INotificationHandler<PromoCodeReleasedEvent>
{
    private readonly ILogger<PromoCodeReleasedEventHandler> _logger;

    public PromoCodeReleasedEventHandler(ILogger<PromoCodeReleasedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PromoCodeReleasedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PromoCodeReleased: code={Code} user={UserId} reservation={ReservationId} at={OccurredAt:o}",
            notification.Code,
            notification.UserId,
            notification.ReservationId,
            notification.OccurredAt);
        return Task.CompletedTask;
    }
}
