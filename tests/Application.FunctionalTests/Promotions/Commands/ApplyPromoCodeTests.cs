using greenfield_checkout.Application.FunctionalTests.Infrastructure;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Application.Promotions.Common.Models;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace greenfield_checkout.Application.FunctionalTests.Promotions.Commands;

/// <summary>
/// SPEC-2026-0043 slice 2A — exercises the slice 1 scenarios end-to-end against
/// the real PostgreSQL provisioned by the test Aspire host. Validates that the
/// in-memory → EF Core migration preserves behaviour:
///   - RN1 (validity window) via PRIMAVERA expired
///   - RN2 (global redemption cap) via AGOTADO exhausted
///   - RN5/RN6 (percentage discount with cap) via VERANO20 happy path
/// Also asserts RN10 (immutable trace) by inspecting PromoRedemptions after each command.
/// </summary>
public class ApplyPromoCodeTests : TestBase
{
    private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset YearEnd = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_PercentageHappyPath_AppliesAndPersistsRedemption()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "VERANO20",
            type: PromoCodeType.Percentage,
            value: 20m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 10_000));

        var result = await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-1001",
            Code = "VERANO20",
            Subtotal = 200m
        });

        result.Success.ShouldBeTrue();
        result.Discount.ShouldBe(40m);
        result.FinalPrice.ShouldBe(160m);

        var redemption = await SingleRedemptionAsync();
        redemption.Code.ShouldBe("VERANO20");
        redemption.UserId.ShouldBe(userId);
        redemption.ReservationId.ShouldBe("R-1001");
        redemption.Result.ShouldBe(RedemptionResult.Applied);
        redemption.Reason.ShouldBeNull();
        redemption.AmountDiscounted.ShouldBe(40m);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_ExpiredCode_RejectsWithReasonExpired()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new PromoCode(
            code: "PRIMAVERA",
            type: PromoCodeType.Percentage,
            value: 10m,
            validFrom: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            validTo: new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 1_000));

        var result = await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-1002",
            Code = "PRIMAVERA",
            Subtotal = 200m
        });

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("PROMO_EXPIRED");
        result.FinalPrice.ShouldBe(200m);

        var redemption = await SingleRedemptionAsync();
        redemption.Result.ShouldBe(RedemptionResult.Rejected);
        redemption.Reason.ShouldBe(PromoCodeRejectReason.Expired);
        redemption.AmountDiscounted.ShouldBeNull();
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_ExhaustedCode_RejectsWithReasonExhausted()
    {
        await TestApp.RunAsDefaultUserAsync();

        var agotado = new PromoCode(
            code: "AGOTADO",
            type: PromoCodeType.Percentage,
            value: 10m,
            validFrom: YearStart,
            validTo: YearEnd,
            maxTotalRedemptions: 1);
        agotado.Consume();
        await TestApp.AddAsync(agotado);

        var result = await TestApp.SendAsync(new ApplyPromoCodeCommand
        {
            ReservationId = "R-1003",
            Code = "AGOTADO",
            Subtotal = 200m
        });

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("PROMO_EXHAUSTED");

        var redemption = await SingleRedemptionAsync();
        redemption.Result.ShouldBe(RedemptionResult.Rejected);
        redemption.Reason.ShouldBe(PromoCodeRejectReason.Exhausted);
    }

    private static async Task<PromoRedemption> SingleRedemptionAsync()
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<greenfield_checkout.Infrastructure.Data.ApplicationDbContext>();
        return await context.PromoRedemptions.SingleAsync();
    }
}
