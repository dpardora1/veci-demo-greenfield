namespace greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;

public sealed class ApplyPromoCodeCommandValidator : AbstractValidator<ApplyPromoCodeCommand>
{
    public ApplyPromoCodeCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(3, 32)
            .Matches("^[A-Z0-9_-]+$")
            .WithMessage("Code must contain only uppercase letters, digits, '_' or '-'.");

        RuleFor(x => x.Subtotal)
            .GreaterThanOrEqualTo(0m);
    }
}
