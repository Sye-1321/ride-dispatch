using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Assignments;

public sealed record AssignmentView(Guid Id, Guid OfferId, Guid RideRequestId, Guid DriverId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt)
{
    public static AssignmentView FromAssignment(Assignment assignment) => new(
        assignment.Id, assignment.OfferId, assignment.RideRequestId, assignment.DriverId,
        assignment.StartsAt, assignment.EndsAt, assignment.CreatedAt);
}
