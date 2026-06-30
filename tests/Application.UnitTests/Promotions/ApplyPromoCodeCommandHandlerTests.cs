using greenfield_checkout.Application.Common.Interfaces;
using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Application.Promotions.Common;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace greenfield_checkout.Application.UnitTests.Promotions;

/// <summary>
/// Each test name maps 1:1 to a Gherkin scenario in SPEC-2026-0043 §5.
/// </summary>
[TestFixture]
public class ApplyPromoCodeCommandHandlerTests
{
    private Mock<IPromoCodeRepository> _repo = null!;
    private Mock<IUser> _user = null!;
    private FixedTimeProvider _time = null!;
    private ApplyPromoCodeCommandHandler _sut = null!;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<IPromoCodeRepository>(MockBehavior.Strict);
        _repo.Setup(r => r.SaveAsync(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddRedemptionAsync(It.IsAny<PromoRedemption>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        // Slice 2B: idempotency probe defaults to "no prior Applied redemption" for every test
        // that does not opt-in. Individual tests can override this setup to simulate a retry.
        _repo.Setup(r => r.GetActiveAppliedRedemptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PromoRedemption?)null);

        _user = new Mock<IUser>();
        _user.SetupGet(u => u.Id).Returns("user-001");

        _time = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

        _sut = new ApplyPromoCodeCommandHandler(_repo.Object, _user.Object, _time);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_codigo_porcentual_valido_aplicado_returns_discounted_total()
    {
        var promo = new PromoCode("VERANO20", PromoCodeType.Percentage, 20m,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 10_000);
        _repo.Setup(r => r.GetAsync("VERANO20", It.IsAny<CancellationToken>()))
             .ReturnsAsync(promo);

        var result = await _sut.Handle(new ApplyPromoCodeCommand
        {
            ReservationId = "res-1",
            Code = "VERANO20",
            Subtotal = 250m
        }, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Discount.ShouldBe(50m);
        result.FinalPrice.ShouldBe(200m);
        result.ErrorCode.ShouldBeNull();
        _repo.Verify(r => r.SaveAsync(It.Is<PromoCode>(p => p.TotalRedemptions == 1), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.AddRedemptionAsync(
            It.Is<PromoRedemption>(x => x.Result == RedemptionResult.Applied && x.AmountDiscounted == 50m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_codigo_caducado_returns_PROMO_EXPIRED()
    {
        var promo = new PromoCode("PRIMAVERA", PromoCodeType.Percentage, 10m,
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 1_000);
        _repo.Setup(r => r.GetAsync("PRIMAVERA", It.IsAny<CancellationToken>()))
             .ReturnsAsync(promo);

        var result = await _sut.Handle(new ApplyPromoCodeCommand
        {
            ReservationId = "res-2",
            Code = "PRIMAVERA",
            Subtotal = 100m
        }, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("PROMO_EXPIRED");
        result.Discount.ShouldBe(0m);
        result.FinalPrice.ShouldBe(100m);
        _repo.Verify(r => r.SaveAsync(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.AddRedemptionAsync(
            It.Is<PromoRedemption>(x => x.Result == RedemptionResult.Rejected && x.Reason == PromoCodeRejectReason.Expired),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Spec_SPEC_2026_0043_Scenario_codigo_agotado_returns_PROMO_EXHAUSTED()
    {
        var promo = new PromoCode("AGOTADO", PromoCodeType.Percentage, 10m,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            maxTotalRedemptions: 1);
        promo.Consume();
        _repo.Setup(r => r.GetAsync("AGOTADO", It.IsAny<CancellationToken>()))
             .ReturnsAsync(promo);

        var result = await _sut.Handle(new ApplyPromoCodeCommand
        {
            ReservationId = "res-3",
            Code = "AGOTADO",
            Subtotal = 100m
        }, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("PROMO_EXHAUSTED");
        _repo.Verify(r => r.AddRedemptionAsync(
            It.Is<PromoRedemption>(x => x.Result == RedemptionResult.Rejected && x.Reason == PromoCodeRejectReason.Exhausted),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
