using greenfield_checkout.Application.FunctionalTests.Infrastructure;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;

namespace greenfield_checkout.Application.FunctionalTests.Promotions.Commands;

/// <summary>
/// SPEC-2026-0043 slice 2B — PUT idempotency by (reservationId, code, userId) within
/// the pre-reservation window. A retry must not double-consume the global slot nor
/// duplicate the redemption trace.
/// </summary>
public class ApplyPromoCodeIdempotencyTests : TestBase
{
    private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset YearEnd = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    [Test]
    public async Task Spec_SPEC_2026_0043_PUT_Idempotent_Same_Reservation_Code_User()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "VERANO20",
            type: PromoCodeType.Percentage,
            value: 20m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 10_000));

        var command = new ApplyPromoCodeCommand
        {
            ReservationId = "R-2001",
            Code = "VERANO20",
            Subtotal = 200m
        };

        var first = await TestApp.SendAsync(command);
        var second = await TestApp.SendAsync(command);

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeTrue();
        first.Discount.ShouldBe(second.Discount);
        first.FinalPrice.ShouldBe(second.FinalPrice);

        var redemptionCount = await TestApp.CountAsync<PromoRedemption>();
        redemptionCount.ShouldBe(1);

        var promo = await TestApp.FindAsync<PromoCode>("VERANO20");
        promo!.TotalRedemptions.ShouldBe(1);
    }
}
