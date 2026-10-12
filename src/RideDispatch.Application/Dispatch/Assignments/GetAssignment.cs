namespace RideDispatch.Application.Dispatch.Assignments;

public sealed class GetAssignment(IAssignmentStore store)
{
    public async Task<AssignmentView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await store.FindByIdReadOnlyAsync(id, cancellationToken);
        return assignment is null ? null : AssignmentView.FromAssignment(assignment);
    }
}
