using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Vehicles;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/drivers/{driverId:guid}/vehicle")]
public sealed class VehiclesController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VehicleResponse>> Register(
        Guid driverId,
        RegisterVehicleRequest request,
        [FromServices] RegisterVehicle useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            driverId,
            request.Type,
            request.PlateNumber,
            cancellationToken);

        return result.Outcome switch
        {
            RegisterVehicleOutcome.Success => CreatedAtAction(
                nameof(Get),
                new { driverId },
                VehicleResponse.FromView(result.Vehicle!)),
            RegisterVehicleOutcome.DriverNotFound => NotFound(),
            RegisterVehicleOutcome.VehicleAlreadyRegistered => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Vehicle already registered",
                Detail = "This driver already has a registered vehicle.",
                Extensions = { ["code"] = "VEHICLE_ALREADY_REGISTERED" },
            }),
            _ => throw new InvalidOperationException("Unknown vehicle registration outcome."),
        };
    }

    [HttpGet]
    public async Task<ActionResult<VehicleResponse>> Get(
        Guid driverId,
        [FromServices] GetDriverVehicle useCase,
        CancellationToken cancellationToken)
    {
        var vehicle = await useCase.ExecuteAsync(driverId, cancellationToken);
        return vehicle is null ? NotFound() : Ok(VehicleResponse.FromView(vehicle));
    }
}

public sealed record RegisterVehicleRequest(
    VehicleType Type,
    [Required, StringLength(32)] string PlateNumber);

public sealed record VehicleResponse(
    Guid Id,
    Guid DriverId,
    VehicleType Type,
    string PlateNumber)
{
    public static VehicleResponse FromView(VehicleView vehicle) =>
        new(vehicle.Id, vehicle.DriverId, vehicle.Type, vehicle.PlateNumber);
}
