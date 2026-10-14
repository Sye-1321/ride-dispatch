using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.Dispatch.Assignments;
using RideDispatch.Domain.Dispatch;

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

    [HttpPost("assignments/{assignmentId:guid}/complete")]
    public async Task<ActionResult<AssignmentResponse>> Complete(Guid assignmentId, [FromServices] CompleteAssignment useCase, CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(assignmentId, cancellationToken);
        return result.Outcome switch
        {
            CompleteAssignmentOutcome.Success => Ok(AssignmentResponse.FromView(result.Assignment!)),
            CompleteAssignmentOutcome.AssignmentNotFound => NotFound(),
            CompleteAssignmentOutcome.InvalidState or CompleteAssignmentOutcome.AssignmentNotStarted =>
                Conflict(result.Assignment is null ? null : AssignmentResponse.FromView(result.Assignment)),
            _ => throw new InvalidOperationException("Unknown assignment completion outcome."),
        };
    }

    [HttpPost("assignments/{assignmentId:guid}/cancel")]
    public async Task<ActionResult<AssignmentResponse>> Cancel(Guid assignmentId, [FromServices] CancelAssignment useCase, CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(assignmentId, cancellationToken);
        return result.Outcome switch
        {
            CancelAssignmentOutcome.Success => Ok(AssignmentResponse.FromView(result.Assignment!)),
            CancelAssignmentOutcome.AssignmentNotFound => NotFound(),
            CancelAssignmentOutcome.InvalidState =>
                Conflict(result.Assignment is null ? null : AssignmentResponse.FromView(result.Assignment)),
            _ => throw new InvalidOperationException("Unknown assignment cancellation outcome."),
        };
    }
}

public sealed record AssignmentResponse(Guid Id, Guid OfferId, Guid RideRequestId, Guid DriverId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt,
    AssignmentStatus Status, DateTimeOffset? ResolvedAt)
{
    public static AssignmentResponse FromView(AssignmentView value) => new(value.Id, value.OfferId,
        value.RideRequestId, value.DriverId, value.StartsAt, value.EndsAt, value.CreatedAt,
        value.Status, value.ResolvedAt);
}
