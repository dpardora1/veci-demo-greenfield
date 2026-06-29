using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Application.Promotions.Common.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace greenfield_checkout.Web.Endpoints;

/// <summary>
/// SPEC-2026-0043 — promotional code endpoint group.
/// Slice 1: PUT (apply). Slice 2+ pending: DELETE (release), idempotency, rate limiting.
/// </summary>
public class PromoCodes : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Slice 1 keeps the endpoint anonymous to ease the smoke demo. Slice 2 will
        // re-enable RequireAuthorization() once the front-end auth wiring lands.
        groupBuilder.MapPut(ApplyPromoCode, "{reservationId}");
    }

    [EndpointSummary("Apply a promotional code to a reservation (SPEC-2026-0043)")]
    [EndpointDescription("Validates the code against vigency, global exhaustion and computes the discount with optional percentage cap. Returns 200 on success and 422 with an ErrorCode on rejection.")]
    public static async Task<Results<Ok<ApplyPromoCodeResult>, UnprocessableEntity<ApplyPromoCodeResult>>> ApplyPromoCode(
        ISender sender,
        string reservationId,
        ApplyPromoCodeRequest body)
    {
        var result = await sender.Send(new ApplyPromoCodeCommand
        {
            ReservationId = reservationId,
            Code = body.Code,
            Subtotal = body.Subtotal
        });

        return result.Success
            ? TypedResults.Ok(result)
            : TypedResults.UnprocessableEntity(result);
    }
}

public sealed record ApplyPromoCodeRequest(string Code, decimal Subtotal);
