using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class OffersController : ControllerBase
{
    [HttpPost("allocation-runs/{allocationRunId:guid}/offers")]
    public async Task<ActionResult<OfferResponse>> Create(Guid allocationRunId, [FromServices] CreateOffer useCase, CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(allocationRunId, cancellationToken);
        return result.Outcome switch
        {
            CreateOfferOutcome.Success => CreatedAtAction(nameof(GetById), new { offerId = result.Offer!.Id }, OfferResponse.FromView(result.Offer)),
            CreateOfferOutcome.AllocationRunNotFound => NotFound(),
            CreateOfferOutcome.NoRecommendedDriver or CreateOfferOutcome.OfferAlreadyExists => Conflict(),
            _ => throw new InvalidOperationException("Unknown offer creation outcome."),
        };
    }

    [HttpGet("offers/{offerId:guid}")]
    public async Task<ActionResult<OfferResponse>> GetById(Guid offerId, [FromServices] GetOffer useCase, CancellationToken cancellationToken)
    {
        var offer = await useCase.ExecuteAsync(offerId, cancellationToken);
        return offer is null ? NotFound() : Ok(OfferResponse.FromView(offer));
    }

    [HttpPost("offers/{offerId:guid}/accept")]
    public async Task<ActionResult<OfferResponse>> Accept(Guid offerId, [FromServices] AcceptOffer useCase, CancellationToken cancellationToken) =>
        MapTransition(await useCase.ExecuteAsync(offerId, cancellationToken));

    [HttpPost("offers/{offerId:guid}/decline")]
    public async Task<ActionResult<OfferResponse>> Decline(Guid offerId, [FromServices] DeclineOffer useCase, CancellationToken cancellationToken) =>
        MapTransition(await useCase.ExecuteAsync(offerId, cancellationToken));

    [HttpPost("offers/{offerId:guid}/withdraw")]
    public async Task<ActionResult<OfferResponse>> Withdraw(Guid offerId, [FromServices] WithdrawOffer useCase, CancellationToken cancellationToken) =>
        MapTransition(await useCase.ExecuteAsync(offerId, cancellationToken));

    [HttpPost("offers/{offerId:guid}/replacement-dispatch")]
    public async Task<ActionResult<ReplacementDispatchResponse>> CreateReplacement(
        Guid offerId,
        [FromServices] CreateReplacementDispatch useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(offerId, cancellationToken);
        return result.Outcome switch
        {
            CreateReplacementDispatchOutcome.Success => Ok(
                ReplacementDispatchResponse.FromResult(result)),
            CreateReplacementDispatchOutcome.OfferNotFound => NotFound(),
            CreateReplacementDispatchOutcome.SourceOfferNotFailed => Conflict(),
            _ => throw new InvalidOperationException("Unknown replacement dispatch outcome."),
        };
    }

    private ActionResult<OfferResponse> MapTransition(TransitionOfferResult result) => result.Outcome switch
    {
        TransitionOfferOutcome.Success => Ok(OfferResponse.FromView(result.Offer!)),
        TransitionOfferOutcome.OfferNotFound => NotFound(),
        TransitionOfferOutcome.OfferExpired or TransitionOfferOutcome.InvalidState => Conflict(result.Offer is null ? null : OfferResponse.FromView(result.Offer)),
        _ => throw new InvalidOperationException("Unknown offer transition outcome."),
    };
}

public sealed record ReplacementDispatchResponse(
    Guid SourceOfferId,
    AllocationRunResponse AllocationRun,
    OfferResponse? Offer)
{
    public static ReplacementDispatchResponse FromResult(CreateReplacementDispatchResult result) =>
        new(
            result.SourceOfferId,
            AllocationRunResponse.FromView(result.AllocationRun!),
            result.Offer is null ? null : OfferResponse.FromView(result.Offer));
}

public sealed record OfferResponse(Guid Id, Guid AllocationRunId, Guid DriverId, OfferStatus Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? ResolvedAt)
{
    public static OfferResponse FromView(OfferView offer) => new(offer.Id, offer.AllocationRunId, offer.DriverId, offer.Status, offer.CreatedAt, offer.ExpiresAt, offer.ResolvedAt);
}
