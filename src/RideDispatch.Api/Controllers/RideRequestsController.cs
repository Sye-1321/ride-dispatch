using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/ride-requests")]
public sealed class RideRequestsController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RideRequestResponse>> Create(
        CreateRideRequestRequest request,
        [FromServices] CreateRideRequest useCase,
        CancellationToken cancellationToken)
    {
        CreateRideRequestResult result;

        try
        {
            result = await useCase.ExecuteAsync(request.ToInput(), cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(request), exception.Message);
            return ValidationProblem(ModelState);
        }

        return result.Outcome switch
        {
            CreateRideRequestOutcome.Success => CreatedAtAction(
                nameof(GetById),
                new { id = result.RideRequest!.Id },
                RideRequestResponse.FromView(result.RideRequest)),
            CreateRideRequestOutcome.PassengerNotFound => NotFound(),
            _ => throw new InvalidOperationException("Unknown ride-request creation outcome."),
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RideRequestResponse>> GetById(
        Guid id,
        [FromServices] GetRideRequest useCase,
        CancellationToken cancellationToken)
    {
        var rideRequest = await useCase.ExecuteAsync(id, cancellationToken);
        return rideRequest is null ? NotFound() : Ok(RideRequestResponse.FromView(rideRequest));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RideRequestResponse>>> List(
        [FromServices] ListRideRequests useCase,
        CancellationToken cancellationToken)
    {
        var rideRequests = await useCase.ExecuteAsync(cancellationToken);
        return Ok(rideRequests.Select(RideRequestResponse.FromView));
    }
}

public sealed record CreateRideRequestRequest(
    Guid? PassengerId,
    string? ContactName,
    string? ContactPhone,
    [Required] BookingSource? BookingSource,
    [Required] RideTiming? Timing,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    [Required] VehicleType? RequiredVehicleType,
    DateTimeOffset? RequestedPickupAt,
    int EstimatedTripDurationSeconds)
{
    public CreateRideRequestInput ToInput() =>
        new(
            PassengerId,
            ContactName,
            ContactPhone,
            BookingSource!.Value,
            Timing!.Value,
            PickupLatitude,
            PickupLongitude,
            DestinationLatitude,
            DestinationLongitude,
            RequiredVehicleType!.Value,
            RequestedPickupAt,
            EstimatedTripDurationSeconds);
}

public sealed record RideRequestResponse(
    Guid Id,
    Guid? PassengerId,
    string ContactName,
    string ContactPhone,
    BookingSource BookingSource,
    RideTiming Timing,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    VehicleType RequiredVehicleType,
    DateTimeOffset? RequestedPickupAt,
    int EstimatedTripDurationSeconds,
    DateTimeOffset CreatedAt)
{
    public static RideRequestResponse FromView(RideRequestView rideRequest) =>
        new(
            rideRequest.Id,
            rideRequest.PassengerId,
            rideRequest.ContactName,
            rideRequest.ContactPhone,
            rideRequest.BookingSource,
            rideRequest.Timing,
            rideRequest.PickupLatitude,
            rideRequest.PickupLongitude,
            rideRequest.DestinationLatitude,
            rideRequest.DestinationLongitude,
            rideRequest.RequiredVehicleType,
            rideRequest.RequestedPickupAt,
            rideRequest.EstimatedTripDurationSeconds,
            rideRequest.CreatedAt);
}
