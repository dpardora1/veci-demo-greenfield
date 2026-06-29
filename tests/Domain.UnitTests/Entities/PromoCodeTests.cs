using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using NUnit.Framework;
using Shouldly;

namespace greenfield_checkout.Domain.UnitTests.Entities;

[TestFixture]
public class PromoCodeTests
{
    // Defaults chosen so each test exercises one rule in isolation.
    private static readonly DateTimeOffset DefaultFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DefaultTo   = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset InsideWindow = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Spec_SPEC_2026_0043_RN1_Evaluate_OutsideValidityWindow_RejectsAsExpired()
    {
        var promo = new PromoCode("PRIMAVERA", PromoCodeType.Percentage, 10m,
            validFrom: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            validTo:   new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 1000);

        var afterWindow = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var result = promo.Evaluate(subtotal: 100m, now: afterWindow);

        result.Success.ShouldBeFalse();
        result.Reason.ShouldBe(PromoCodeRejectReason.Expired);
        result.Discount.ShouldBe(0m);
    }

    [Test]
    public void Spec_SPEC_2026_0043_RN2_Evaluate_WhenGlobalCapReached_RejectsAsExhausted()
    {
        var promo = new PromoCode("AGOTADO", PromoCodeType.Percentage, 10m, DefaultFrom, DefaultTo, maxTotalRedemptions: 1);
        promo.Consume();

        var result = promo.Evaluate(100m, InsideWindow);

        result.Success.ShouldBeFalse();
        result.Reason.ShouldBe(PromoCodeRejectReason.Exhausted);
    }

    [Test]
    public void Spec_SPEC_2026_0043_RN5_Evaluate_PercentageCode_ReturnsCorrectDiscount()
    {
        var promo = new PromoCode("VERANO20", PromoCodeType.Percentage, 20m, DefaultFrom, DefaultTo, maxTotalRedemptions: 10_000);

        var result = promo.Evaluate(subtotal: 250m, now: InsideWindow);

        result.Success.ShouldBeTrue();
        result.Discount.ShouldBe(50m); // 20% of 250
    }

    [Test]
    public void Spec_SPEC_2026_0043_RN6_Evaluate_PercentageWithCap_AppliesMaxDiscount()
    {
        // MEGA50: 50% but capped at 30€. On a 100€ subtotal, raw discount is 50€,
        // so the cap must bring it down to 30€.
        var promo = new PromoCode("MEGA50", PromoCodeType.Percentage, 50m, DefaultFrom, DefaultTo,
            maxTotalRedemptions: 10_000, maxDiscount: 30m);

        var result = promo.Evaluate(subtotal: 100m, now: InsideWindow);

        result.Success.ShouldBeTrue();
        result.Discount.ShouldBe(30m);
    }
}
