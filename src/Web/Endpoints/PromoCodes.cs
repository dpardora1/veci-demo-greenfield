using greenfield_checkout.Application.Promotions.Commands.ApplyPromoCode;
using greenfield_checkout.Application.Promotions.Commands.ReleasePromoCode;
using greenfield_checkout.Application.Promotions.Common.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace greenfield_checkout.Web.Endpoints;

/// <summary>
/// SPEC-2026-0043 — promotional code endpoint group.
/// Slice 1: PUT (apply).
/// Slice 2B: DELETE (release) and idempotent PUT.
/// Slice 2+ pending: rate limiting, auth, RN3/RN4/RN7/RN8.
/// </summary>
public class PromoCodes : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Slice 1 keeps the endpoint anonymous to ease the smoke demo. Slice 2 will
        // re-enable RequireAuthorization() once the front-end auth wiring lands.
        groupBuilder.MapPut(ApplyPromoCode, "{reservationId}");
        groupBuilder.MapDelete(ReleasePromoCode, "{reservationId}");
    }

    [EndpointSummary("Apply a promotional code to a reservation (SPEC-2026-0043)")]
    [EndpointDescription("Validates the code against vigency, global exhaustion and computes the discount with optional percentage cap. Returns 200 on success and 422 with an ErrorCode on rejection. Idempotent on (reservationId, code, user) within the pre-reservation window.")]
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

    [EndpointSummary("Release the promotional code applied to a reservation (SPEC-2026-0043 §7.1)")]
    [EndpointDescription("Returns the slot to the global redemption pool and emits PromoCodeReleased. 404 when there is no Applied code on the reservation for the current user.")]
    public static async Task<Results<Ok<ReleasePromoCodeResult>, NotFound>> ReleasePromoCode(
        ISender sender,
        string reservationId)
    {
        var result = await sender.Send(new ReleasePromoCodeCommand
        {
            ReservationId = reservationId
        });

        return result.Released
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }
}

public sealed record ApplyPromoCodeRequest(string Code, decimal Subtotal);
