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
