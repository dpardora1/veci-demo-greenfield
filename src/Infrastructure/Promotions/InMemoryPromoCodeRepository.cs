using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;

namespace greenfield_checkout.Infrastructure.Promotions;

/// <summary>
/// In-memory repository for SPEC-2026-0043 slice 1. Demo-only.
/// Slice 2+ will replace this with EF Core + PostgreSQL persistence and a migration.
/// Seeded with a fixed catalog matching the Gherkin Background of the spec.
/// </summary>
public sealed class InMemoryPromoCodeRepository : IPromoCodeRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, PromoCode> _codes;
    private readonly List<PromoRedemption> _redemptions = new();
    private int _redemptionId;

    public InMemoryPromoCodeRepository()
    {
        _codes = new Dictionary<string, PromoCode>(StringComparer.OrdinalIgnoreCase)
        {
            ["VERANO20"] = new PromoCode(
                code: "VERANO20",
                type: PromoCodeType.Percentage,
                value: 20m,
                validFrom: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                validTo:   new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
                maxTotalRedemptions: 10_000),

            ["FIJO50"] = new PromoCode(
                code: "FIJO50",
                type: PromoCodeType.Fixed,
                value: 50m,
                validFrom: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                validTo:   new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
                maxTotalRedemptions: 10_000),

            ["MEGA50"] = new PromoCode(
                code: "MEGA50",
                type: PromoCodeType.Percentage,
                value: 50m,
                validFrom: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                validTo:   new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
                maxTotalRedemptions: 10_000,
                maxDiscount: 30m),

            ["PRIMAVERA"] = new PromoCode(
                code: "PRIMAVERA",
                type: PromoCodeType.Percentage,
                value: 10m,
                validFrom: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                validTo:   new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero),
                maxTotalRedemptions: 1_000),
        };

        // Seed the exhausted code (RN2) by pre-consuming it up to the cap.
        var agotado = new PromoCode(
            code: "AGOTADO",
            type: PromoCodeType.Percentage,
            value: 10m,
            validFrom: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            validTo:   new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 1);
        agotado.Consume();
        _codes["AGOTADO"] = agotado;
    }

    public Task<PromoCode?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_codes.TryGetValue(code, out var promo) ? promo : null);
        }
    }

    public Task SaveAsync(PromoCode promoCode, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _codes[promoCode.Code] = promoCode;
        }
        return Task.CompletedTask;
    }

    public Task AddRedemptionAsync(PromoRedemption redemption, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            redemption.Id = ++_redemptionId;
            redemption.Created = DateTimeOffset.UtcNow;
            _redemptions.Add(redemption);
        }
        return Task.CompletedTask;
    }

    internal IReadOnlyList<PromoRedemption> SnapshotRedemptions()
    {
        lock (_lock)
        {
            return _redemptions.ToList();
        }
    }
}
