using greenfield_checkout.Application.FunctionalTests.Infrastructure;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Domain.Entities;

namespace greenfield_checkout.Application.FunctionalTests.Promotions.Commands;

/// <summary>
/// SPEC-2026-0043 §8 — brute-force protection: at most 10 attempts per (user, reservation)
/// within a 5-min window. The 11th attempt is rejected with PROMO_RATE_LIMITED regardless
/// of whether the code itself would have been valid.
/// </summary>
public class ApplyPromoCodeRateLimitTests : TestBase
{
    [Test]
    public async Task Spec_SPEC_2026_0043_Anti_Bruteforce_Eleventh_Attempt_Returns_PROMO_RATE_LIMITED()
    {
        await TestApp.RunAsDefaultUserAsync();

        var probe = new ApplyPromoCodeCommand
        {
            ReservationId = "R-5001",
            Code = "NOEXISTE",
            Subtotal = 100m
        };

        for (var i = 0; i < ApplyPromoCodeCommandHandler.RateLimitMaxAttempts; i++)
        {
            var attempt = await TestApp.SendAsync(probe);
            attempt.ErrorCode.ShouldBe("PROMO_NOT_FOUND");
        }

        var limited = await TestApp.SendAsync(probe);

        limited.Success.ShouldBeFalse();
        limited.ErrorCode.ShouldBe("PROMO_RATE_LIMITED");

        // All 11 attempts left an audit trace (RN10).
        var traces = await TestApp.CountAsync<PromoRedemption>();
        traces.ShouldBe(ApplyPromoCodeCommandHandler.RateLimitMaxAttempts + 1);
    }
}
