using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.DriverLocations;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/drivers")]
public sealed class DriverLocationsController : ControllerBase
{
    [HttpPut("{driverId:guid}/location")]
    public async Task<ActionResult<DriverLocationResponse>> Update(
        Guid driverId,
        UpdateDriverLocationRequest request,
        [FromServices] UpdateDriverLocation useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            driverId,
            request.Latitude,
            request.Longitude,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateDriverLocationOutcome.Success =>
                Ok(DriverLocationResponse.FromView(result.Location!)),
            UpdateDriverLocationOutcome.DriverNotFound => NotFound(),
            _ => throw new InvalidOperationException("Unknown driver location update outcome."),
        };
    }

    [HttpGet("{driverId:guid}/location")]
    public async Task<ActionResult<DriverLocationResponse>> Get(
        Guid driverId,
        [FromServices] GetDriverLocation useCase,
        CancellationToken cancellationToken)
    {
        var location = await useCase.ExecuteAsync(driverId, cancellationToken);
        return location is null ? NotFound() : Ok(DriverLocationResponse.FromView(location));
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<IReadOnlyList<NearbyDriverResponse>>> FindNearby(
        [FromQuery, Range(-90.0, 90.0)] double latitude,
        [FromQuery, Range(-180.0, 180.0)] double longitude,
        [FromQuery, Range(double.Epsilon, double.MaxValue)] double radiusMeters,
        [FromServices] FindNearbyDrivers useCase,
        CancellationToken cancellationToken)
    {
        var drivers = await useCase.ExecuteAsync(
            latitude,
            longitude,
            radiusMeters,
            cancellationToken);

        return Ok(drivers.Select(NearbyDriverResponse.FromView));
    }
}

public sealed record UpdateDriverLocationRequest(
    [Range(-90.0, 90.0)] double Latitude,
    [Range(-180.0, 180.0)] double Longitude);

public sealed record DriverLocationResponse(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt)
{
    public static DriverLocationResponse FromView(DriverLocationView location) =>
        new(location.DriverId, location.Latitude, location.Longitude, location.RecordedAt);
}

public sealed record NearbyDriverResponse(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    double DistanceMeters)
{
    public static NearbyDriverResponse FromView(NearbyDriverView driver) =>
        new(
            driver.DriverId,
            driver.Latitude,
            driver.Longitude,
            driver.RecordedAt,
            driver.DistanceMeters);
}
