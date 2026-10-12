using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.Assignments;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AssignmentsController : ControllerBase
{
    [HttpPost("offers/{offerId:guid}/assignments")]
    public async Task<ActionResult<AssignmentResponse>> Create(Guid offerId, [FromServices] CreateAssignment useCase, CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(offerId, cancellationToken);
        return result.Outcome switch
        {
            CreateAssignmentOutcome.Success => CreatedAtAction(nameof(GetById), new { assignmentId = result.Assignment!.Id }, AssignmentResponse.FromView(result.Assignment)),
            CreateAssignmentOutcome.OfferNotFound => NotFound(),
            CreateAssignmentOutcome.OfferNotAccepted or CreateAssignmentOutcome.ScheduledPickupElapsed or
                CreateAssignmentOutcome.AssignmentAlreadyExists or CreateAssignmentOutcome.DriverScheduleConflict => Conflict(),
            _ => throw new InvalidOperationException("Unknown assignment creation outcome."),
        };
    }

    [HttpGet("assignments/{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentResponse>> GetById(Guid assignmentId, [FromServices] GetAssignment useCase, CancellationToken cancellationToken)
    {
        var assignment = await useCase.ExecuteAsync(assignmentId, cancellationToken);
        return assignment is null ? NotFound() : Ok(AssignmentResponse.FromView(assignment));
    }
}

public sealed record AssignmentResponse(Guid Id, Guid OfferId, Guid RideRequestId, Guid DriverId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt)
{
    public static AssignmentResponse FromView(AssignmentView value) => new(value.Id, value.OfferId,
        value.RideRequestId, value.DriverId, value.StartsAt, value.EndsAt, value.CreatedAt);
}
