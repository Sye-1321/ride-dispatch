using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AllocationRunsController : ControllerBase
{
    [HttpPost("ride-requests/{rideRequestId:guid}/allocation-runs")]
    public async Task<ActionResult<AllocationRunResponse>> Create(
        Guid rideRequestId,
        [FromServices] CreateAllocationRun useCase,
        CancellationToken cancellationToken,
        [FromQuery] DispatchRankingPolicy rankingPolicy = DispatchRankingPolicy.Nearest)
    {
        if (!Enum.IsDefined(rankingPolicy))
        {
            ModelState.AddModelError(nameof(rankingPolicy), "The ranking policy is invalid.");
            return ValidationProblem(ModelState);
        }

        var result = await useCase.ExecuteAsync(rideRequestId, rankingPolicy, cancellationToken);

        return result.Outcome switch
        {
            CreateAllocationRunOutcome.Success => CreatedAtAction(
                nameof(GetById),
                new { allocationRunId = result.AllocationRun!.Id },
                AllocationRunResponse.FromView(result.AllocationRun)),
            CreateAllocationRunOutcome.RideRequestNotFound => NotFound(),
            _ => throw new InvalidOperationException("Unknown allocation-run creation outcome."),
        };
    }

    [HttpGet("allocation-runs/{allocationRunId:guid}")]
    public async Task<ActionResult<AllocationRunResponse>> GetById(
        Guid allocationRunId,
        [FromServices] GetAllocationRun useCase,
        CancellationToken cancellationToken)
    {
        var allocationRun = await useCase.ExecuteAsync(allocationRunId, cancellationToken);
        return allocationRun is null
            ? NotFound()
            : Ok(AllocationRunResponse.FromView(allocationRun));
    }

    [HttpGet("allocation-runs/{allocationRunId:guid}/candidate-evaluations")]
    public async Task<ActionResult<IReadOnlyList<AllocationCandidateEvaluationResponse>>> GetCandidateEvaluations(
        Guid allocationRunId,
        [FromServices] GetAllocationCandidateEvaluations useCase,
        CancellationToken cancellationToken)
    {
        var evaluations = await useCase.ExecuteAsync(allocationRunId, cancellationToken);
        return evaluations is null
            ? NotFound()
            : Ok(evaluations.Select(AllocationCandidateEvaluationResponse.FromView));
    }
}

public sealed record AllocationRunResponse(
    Guid Id,
    Guid RideRequestId,
    DispatchRankingPolicy RankingPolicy,
    Guid? RecommendedDriverId,
    DateTimeOffset CreatedAt)
{
    public static AllocationRunResponse FromView(AllocationRunView allocationRun) =>
        new(
            allocationRun.Id,
            allocationRun.RideRequestId,
            allocationRun.RankingPolicy,
            allocationRun.RecommendedDriverId,
            allocationRun.CreatedAt);
}

public sealed record AllocationCandidateEvaluationResponse(
    Guid DriverId,
    bool IsEligible,
    int? Rank,
    double DistanceMeters,
    DateTimeOffset? AvailableSince,
    DateTimeOffset LocationRecordedAt,
    IReadOnlyList<EligibilityRejectionReason> RejectionReasons)
{
    public static AllocationCandidateEvaluationResponse FromView(
        AllocationCandidateEvaluationView evaluation) =>
        new(
            evaluation.DriverId,
            evaluation.IsEligible,
            evaluation.Rank,
            evaluation.DistanceMeters,
            evaluation.AvailableSince,
            evaluation.LocationRecordedAt,
            evaluation.RejectionReasons);
}
