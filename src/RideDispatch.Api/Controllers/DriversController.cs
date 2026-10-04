using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/drivers")]
public sealed class DriversController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DriverResponse>> Create(
        CreateDriverRequest request,
        [FromServices] CreateDriver useCase,
        CancellationToken cancellationToken)
    {
        var driver = await useCase.ExecuteAsync(request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, DriverResponse.FromView(driver));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DriverResponse>> GetById(
        Guid id,
        [FromServices] GetDriver useCase,
        CancellationToken cancellationToken)
    {
        var driver = await useCase.ExecuteAsync(id, cancellationToken);
        return driver is null ? NotFound() : Ok(DriverResponse.FromView(driver));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DriverResponse>>> List(
        [FromServices] ListDrivers useCase,
        CancellationToken cancellationToken)
    {
        var drivers = await useCase.ExecuteAsync(cancellationToken);
        return Ok(drivers.Select(DriverResponse.FromView));
    }

    [HttpPut("{id:guid}/approval")]
    public async Task<ActionResult<DriverResponse>> UpdateApproval(
        Guid id,
        UpdateDriverApprovalRequest request,
        [FromServices] UpdateDriverApproval useCase,
        CancellationToken cancellationToken)
    {
        var driver = await useCase.ExecuteAsync(id, request.Status, cancellationToken);
        return driver is null ? NotFound() : Ok(DriverResponse.FromView(driver));
    }

    [HttpPut("{id:guid}/operational-status")]
    public async Task<ActionResult<DriverResponse>> UpdateOperationalStatus(
        Guid id,
        UpdateDriverOperationalStatusRequest request,
        [FromServices] UpdateDriverOperationalStatus useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, request.Status, cancellationToken);
        return result.Outcome switch
        {
            UpdateDriverOperationalStatusOutcome.Success =>
                Ok(DriverResponse.FromView(result.Driver!)),
            UpdateDriverOperationalStatusOutcome.DriverNotFound => NotFound(),
            UpdateDriverOperationalStatusOutcome.DriverNotApproved => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Driver is not approved",
                Detail = "Only approved drivers can be marked as available.",
                Extensions = { ["code"] = "DRIVER_NOT_APPROVED" },
            }),
            _ => throw new InvalidOperationException("Unknown operational status update outcome."),
        };
    }
}

public sealed record CreateDriverRequest(
    [Required, StringLength(200)] string Name);

public sealed record UpdateDriverApprovalRequest(DriverApprovalStatus Status);

public sealed record UpdateDriverOperationalStatusRequest(DriverOperationalStatus Status);

public sealed record DriverResponse(
    Guid Id,
    string Name,
    DriverApprovalStatus ApprovalStatus,
    DriverOperationalStatus OperationalStatus,
    DateTimeOffset? AvailableSince)
{
    public static DriverResponse FromView(DriverView driver) =>
        new(
            driver.Id,
            driver.Name,
            driver.ApprovalStatus,
            driver.OperationalStatus,
            driver.AvailableSince);
}
