using greenfield_checkout.Domain.Entities;

namespace greenfield_checkout.Application.Promotions.Common;

public interface IPromoCodeRepository
{
    Task<PromoCode?> GetAsync(string code, CancellationToken cancellationToken = default);
    Task SaveAsync(PromoCode promoCode, CancellationToken cancellationToken = default);
    Task AddRedemptionAsync(PromoRedemption redemption, CancellationToken cancellationToken = default);
}
