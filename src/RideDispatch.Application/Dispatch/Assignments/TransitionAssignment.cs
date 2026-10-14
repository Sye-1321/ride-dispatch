using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Assignments;

public enum CompleteAssignmentOutcome { Success, AssignmentNotFound, InvalidState, AssignmentNotStarted }
public sealed record CompleteAssignmentResult(CompleteAssignmentOutcome Outcome, AssignmentView? Assignment = null);

public sealed class CompleteAssignment(IAssignmentStore store, TimeProvider timeProvider)
{
    public async Task<CompleteAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await store.FindByIdAsync(assignmentId, cancellationToken);
        if (assignment is null) return new(CompleteAssignmentOutcome.AssignmentNotFound);
        if (assignment.Status != AssignmentStatus.Active)
            return new(CompleteAssignmentOutcome.InvalidState, AssignmentView.FromAssignment(assignment));

        var now = timeProvider.GetUtcNow();
        if (now < assignment.StartsAt)
            return new(CompleteAssignmentOutcome.AssignmentNotStarted, AssignmentView.FromAssignment(assignment));

        assignment.Complete(now);
        try { await store.SaveChangesAsync(cancellationToken); }
        catch (AssignmentStateConcurrencyException)
        {
            var current = await store.FindByIdReadOnlyAsync(assignmentId, cancellationToken);
            return new(CompleteAssignmentOutcome.InvalidState,
                current is null ? null : AssignmentView.FromAssignment(current));
        }
        return new(CompleteAssignmentOutcome.Success, AssignmentView.FromAssignment(assignment));
    }
}

public enum CancelAssignmentOutcome { Success, AssignmentNotFound, InvalidState }
public sealed record CancelAssignmentResult(CancelAssignmentOutcome Outcome, AssignmentView? Assignment = null);

public sealed class CancelAssignment(IAssignmentStore store, TimeProvider timeProvider)
{
    public async Task<CancelAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await store.FindByIdAsync(assignmentId, cancellationToken);
        if (assignment is null) return new(CancelAssignmentOutcome.AssignmentNotFound);
        if (assignment.Status != AssignmentStatus.Active)
            return new(CancelAssignmentOutcome.InvalidState, AssignmentView.FromAssignment(assignment));

        var now = timeProvider.GetUtcNow();
        if (now < assignment.CreatedAt)
            return new(CancelAssignmentOutcome.InvalidState, AssignmentView.FromAssignment(assignment));

        assignment.Cancel(now);
        try { await store.SaveChangesAsync(cancellationToken); }
        catch (AssignmentStateConcurrencyException)
        {
            var current = await store.FindByIdReadOnlyAsync(assignmentId, cancellationToken);
            return new(CancelAssignmentOutcome.InvalidState,
                current is null ? null : AssignmentView.FromAssignment(current));
        }
        return new(CancelAssignmentOutcome.Success, AssignmentView.FromAssignment(assignment));
    }
}
