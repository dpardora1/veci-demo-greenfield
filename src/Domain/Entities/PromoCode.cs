namespace greenfield_checkout.Domain.Entities;

/// <summary>
/// Promotional code aggregate root.
/// SPEC-2026-0043, slice 1: rules RN1 (validity window), RN2 (global exhaustion),
/// RN5 (type exclusivity invariant at construction), RN6 (percentage cap).
/// Out of scope for slice 1: RN3 max_per_user, RN4 destinations/excursion types,
/// RN7 tips exclusion, RN8 stacking, RN9 pre-reservation slot.
/// </summary>
public class PromoCode
{
    public string Code { get; private set; } = default!;
    public PromoCodeType Type { get; private set; }
    public decimal Value { get; private set; }
    public decimal? MaxDiscount { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }
    public int MaxTotalRedemptions { get; private set; }
    public int TotalRedemptions { get; private set; }

    private PromoCode() { }

    public PromoCode(
        string code,
        PromoCodeType type,
        decimal value,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        int maxTotalRedemptions,
        decimal? maxDiscount = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty.", nameof(code));
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be positive.");
        if (validTo < validFrom)
            throw new ArgumentException("ValidTo must not be before ValidFrom.", nameof(validTo));
        if (maxTotalRedemptions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalRedemptions));
        if (type == PromoCodeType.Percentage && (value <= 0m || value > 100m))
            throw new ArgumentOutOfRangeException(nameof(value), "Percentage must be in (0,100].");
        // RN5 invariant: type-and-value pair is exclusive by construction (single Type, single Value).
        if (maxDiscount is < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDiscount));

        Code = code;
        Type = type;
        Value = value;
        ValidFrom = validFrom;
        ValidTo = validTo;
        MaxTotalRedemptions = maxTotalRedemptions;
        MaxDiscount = maxDiscount;
        TotalRedemptions = 0;
    }

    /// <summary>
    /// Pure evaluation: does this code apply to the given subtotal at the given instant?
    /// Does not mutate state (consumption happens explicitly via <see cref="Consume"/>).
    /// </summary>
    public PromoCodeEvaluation Evaluate(decimal subtotal, DateTimeOffset now)
    {
        if (subtotal < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotal));

        // RN1
        if (now < ValidFrom || now > ValidTo)
            return PromoCodeEvaluation.Reject(PromoCodeRejectReason.Expired);

        // RN2
        if (TotalRedemptions >= MaxTotalRedemptions)
            return PromoCodeEvaluation.Reject(PromoCodeRejectReason.Exhausted);

        var discount = Type switch
        {
            PromoCodeType.Percentage => subtotal * (Value / 100m),
            PromoCodeType.Fixed => Math.Min(Value, subtotal),
            _ => 0m
        };

        // RN6
        if (Type == PromoCodeType.Percentage && MaxDiscount.HasValue && discount > MaxDiscount.Value)
            discount = MaxDiscount.Value;

        discount = Math.Round(discount, 2, MidpointRounding.AwayFromZero);

        return PromoCodeEvaluation.Apply(discount);
    }

    /// <summary>
    /// Increments the global redemption counter. Caller must have evaluated success first
    /// and must persist within an atomic boundary (see ADR-pending on concurrency).
    /// </summary>
    public void Consume()
    {
        if (TotalRedemptions >= MaxTotalRedemptions)
            throw new InvalidOperationException($"PromoCode '{Code}' is exhausted.");
        TotalRedemptions++;
    }
}

public readonly record struct PromoCodeEvaluation(bool Success, decimal Discount, PromoCodeRejectReason? Reason)
{
    public static PromoCodeEvaluation Apply(decimal discount) => new(true, discount, null);
    public static PromoCodeEvaluation Reject(PromoCodeRejectReason reason) => new(false, 0m, reason);
}
