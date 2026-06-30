using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using greenfield_checkout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace greenfield_checkout.Infrastructure.Promotions;

/// <summary>
/// EF Core implementation of <see cref="IPromoCodeRepository"/> over the shared
/// <see cref="ApplicationDbContext"/> (PostgreSQL via Aspire). Replaces the slice 1
/// in-memory repository as part of SPEC-2026-0043 slice 2A.
/// </summary>
public sealed class EfPromoCodeRepository : IPromoCodeRepository
{
    private readonly ApplicationDbContext _context;

    public EfPromoCodeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PromoCode?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.PromoCodes
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    public async Task SaveAsync(PromoCode promoCode, CancellationToken cancellationToken = default)
    {
        var existing = await _context.PromoCodes
            .FirstOrDefaultAsync(p => p.Code == promoCode.Code, cancellationToken);

        if (existing is null)
        {
            await _context.PromoCodes.AddAsync(promoCode, cancellationToken);
        }
        else if (!ReferenceEquals(existing, promoCode))
        {
            _context.Entry(existing).CurrentValues.SetValues(promoCode);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRedemptionAsync(PromoRedemption redemption, CancellationToken cancellationToken = default)
    {
        await _context.PromoRedemptions.AddAsync(redemption, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PromoRedemption?> GetActiveAppliedRedemptionAsync(string reservationId, string code, string userId, CancellationToken cancellationToken = default)
    {
        var last = await _context.PromoRedemptions
            .Where(r => r.ReservationId == reservationId
                     && r.Code == code
                     && r.UserId == userId
                     && (r.Result == RedemptionResult.Applied || r.Result == RedemptionResult.Released))
            .OrderByDescending(r => r.Created)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return last is { Result: RedemptionResult.Applied } ? last : null;
    }

    public async Task<PromoRedemption?> GetActiveAppliedRedemptionAsync(string reservationId, string userId, CancellationToken cancellationToken = default)
    {
        var last = await _context.PromoRedemptions
            .Where(r => r.ReservationId == reservationId
                     && r.UserId == userId
                     && (r.Result == RedemptionResult.Applied || r.Result == RedemptionResult.Released))
            .OrderByDescending(r => r.Created)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return last is { Result: RedemptionResult.Applied } ? last : null;
    }
}
