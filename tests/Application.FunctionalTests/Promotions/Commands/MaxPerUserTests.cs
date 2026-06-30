using greenfield_checkout.Application.FunctionalTests.Infrastructure;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;

namespace greenfield_checkout.Application.FunctionalTests.Promotions.Commands;

/// <summary>
/// SPEC-2026-0043 slice 2C — RN3 max_per_user enforcement. BIENVENIDA is restricted to
/// 1 use per customer; a second reservation by the same user must be rejected with
/// PROMO_ALREADY_USED. A release on the first reservation frees the slot.
/// </summary>
public class MaxPerUserTests : TestBase
{
    private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset YearEnd = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_Usuario_ya_redimio_returns_PROMO_ALREADY_USED()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "BIENVENIDA",
            type: PromoCodeType.Percentage,
            value: 15m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 10_000,
            maxPerUser: 1));

        var first = await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-4001",
            Code = "BIENVENIDA",
            Subtotal = 200m
        });

        var second = await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-4002",
            Code = "BIENVENIDA",
            Subtotal = 200m
        });

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeFalse();
        second.ErrorCode.ShouldBe("PROMO_ALREADY_USED");

        // Only the first apply consumed a global slot; the rejected attempt did not.
        var promo = await TestApp.FindAsync<PromoCode>("BIENVENIDA");
        promo!.TotalRedemptions.ShouldBe(1);
    }
}
