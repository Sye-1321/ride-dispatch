using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Passengers;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/passengers")]
public sealed class PassengersController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PassengerResponse>> Create(
        CreatePassengerRequest request,
        [FromServices] CreatePassenger useCase,
        CancellationToken cancellationToken)
    {
        var passenger = await useCase.ExecuteAsync(
            request.Name,
            request.PhoneNumber,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = passenger.Id },
            PassengerResponse.FromView(passenger));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PassengerResponse>> GetById(
        Guid id,
        [FromServices] GetPassenger useCase,
        CancellationToken cancellationToken)
    {
        var passenger = await useCase.ExecuteAsync(id, cancellationToken);
        return passenger is null ? NotFound() : Ok(PassengerResponse.FromView(passenger));
    }
}

public sealed record CreatePassengerRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(32)] string PhoneNumber);

public sealed record PassengerResponse(Guid Id, string Name, string PhoneNumber)
{
    public static PassengerResponse FromView(PassengerView passenger) =>
        new(passenger.Id, passenger.Name, passenger.PhoneNumber);
}
