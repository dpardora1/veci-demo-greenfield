using greenfield_checkout.Application.FunctionalTests.Infrastructure;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Application.Promotions.Commands.ReleasePromoCode;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace greenfield_checkout.Application.FunctionalTests.Promotions.Commands;

/// <summary>
/// SPEC-2026-0043 slice 2B — DELETE flow. Releasing an Applied code returns the slot
/// to the global pool, emits PromoCodeReleasedEvent and persists a Released trace.
/// Re-applying afterwards is allowed and consumes a new slot.
/// </summary>
public class ReleasePromoCodeTests : TestBase
{
    private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset YearEnd = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    [Test]
    public async Task Spec_SPEC_2026_0043_DELETE_Releases_Active_Code_And_Restores_Slot()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "VERANO20",
            type: PromoCodeType.Percentage,
            value: 20m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 10_000));

        await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-3001",
            Code = "VERANO20",
            Subtotal = 200m
        });

        var release = await TestApp.SendAsync(new ReleasePromoCodeCommand
        {
            ReservationId = "R-3001"
        });

        release.Released.ShouldBeTrue();
        release.Code.ShouldBe("VERANO20");

        var promo = await TestApp.FindAsync<PromoCode>("VERANO20");
        promo!.TotalRedemptions.ShouldBe(0);

        var redemptions = await ListRedemptionsAsync();
        redemptions.Count.ShouldBe(2);
        redemptions.Select(r => r.Result).ShouldContain(RedemptionResult.Applied);
        redemptions.Select(r => r.Result).ShouldContain(RedemptionResult.Released);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_DELETE_Returns_NotFound_When_No_Code_Applied()
    {
        await TestApp.RunAsDefaultUserAsync();

        var release = await TestApp.SendAsync(new ReleasePromoCodeCommand
        {
            ReservationId = "R-3002"
        });

        release.Released.ShouldBeFalse();
        release.Code.ShouldBeNull();
        (await TestApp.CountAsync<PromoRedemption>()).ShouldBe(0);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_DELETE_Allows_Re_Apply_After_Release()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "VERANO20",
            type: PromoCodeType.Percentage,
            value: 20m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 10_000));

        var apply = new ApplyPromoCodeCommand
        {
            ReservationId = "R-3003",
            Code = "VERANO20",
            Subtotal = 200m
        };

        await TestApp.SendAsync(apply);
        await TestApp.SendAsync(new ReleasePromoCodeCommand { ReservationId = "R-3003" });
        var reapply = await TestApp.SendAsync(apply);

        reapply.Success.ShouldBeTrue();
        reapply.Discount.ShouldBe(40m);

        var promo = await TestApp.FindAsync<PromoCode>("VERANO20");
        promo!.TotalRedemptions.ShouldBe(1);

        var redemptions = await ListRedemptionsAsync();
        redemptions.Count.ShouldBe(3);
    }

    private static async Task<List<PromoRedemption>> ListRedemptionsAsync()
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<greenfield_checkout.Infrastructure.Data.ApplicationDbContext>();
        return await context.PromoRedemptions.OrderBy(r => r.Id).ToListAsync();
    }
}
