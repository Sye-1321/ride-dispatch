using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/ride-requests/{rideRequestId:guid}/eligible-drivers")]
public sealed class RideRequestEligibilityController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EligibleDriverResponse>>> Get(
        Guid rideRequestId,
        [FromServices] FindEligibleDrivers useCase,
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
            FindEligibleDriversOutcome.Success =>
                Ok(result.Drivers!.Select(EligibleDriverResponse.FromView)),
            FindEligibleDriversOutcome.RideRequestNotFound => NotFound(),
            _ => throw new InvalidOperationException("Unknown eligible-driver lookup outcome."),
        };
    }
}

public sealed record EligibleDriverResponse(
    Guid DriverId,
    double DistanceMeters,
    DateTimeOffset AvailableSince,
    DateTimeOffset LocationRecordedAt)
{
    public static EligibleDriverResponse FromView(EligibleDriverView driver) =>
        new(
            driver.DriverId,
            driver.DistanceMeters,
            driver.AvailableSince,
            driver.LocationRecordedAt);
}
