namespace greenfield_checkout.Domain.Enums;

public enum PromoCodeRejectReason
{
    NotFound = 1,
    Expired = 2,
    Exhausted = 3,
    NotApplicable = 4,
    AlreadyUsed = 5,
    ValidationTimeout = 6
}
