namespace greenfield_checkout.Application.Promotions.Commands.ReleasePromoCode;

public sealed class ReleasePromoCodeCommandValidator : AbstractValidator<ReleasePromoCodeCommand>
{
    public ReleasePromoCodeCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .MaximumLength(64);
    }
}
