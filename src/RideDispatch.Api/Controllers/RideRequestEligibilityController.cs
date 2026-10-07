using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.Eligibility;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/ride-requests/{rideRequestId:guid}/eligible-drivers")]
public sealed class RideRequestEligibilityController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EligibleDriverResponse>>> Get(
        Guid rideRequestId,
        [FromServices] FindEligibleDrivers useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(rideRequestId, cancellationToken);

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
