using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.AllocationRuns;

public sealed class GetAllocationCandidateEvaluationsTests
{
    [Fact]
    public async Task Missing_run_returns_null()
    {
        var useCase = new GetAllocationCandidateEvaluations(new StubStore(null));

        var result = await useCase.ExecuteAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Existing_run_with_no_evaluations_returns_empty_list()
    {
        var useCase = new GetAllocationCandidateEvaluations(new StubStore([]));

        var result = await useCase.ExecuteAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Empty(result!);
    }

    [Fact]
    public async Task Existing_evaluations_are_projected_in_store_order()
    {
        var runId = Guid.CreateVersion7();
        var eligible = AllocationCandidateEvaluation.Create(
            runId, Guid.CreateVersion7(), true, 1, 100, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, []);
        var rejected = AllocationCandidateEvaluation.Create(
            runId, Guid.CreateVersion7(), false, null, 200, null,
            DateTimeOffset.UtcNow, [EligibilityRejectionReason.DriverNotApproved]);
        var useCase = new GetAllocationCandidateEvaluations(new StubStore([eligible, rejected]));

        var result = await useCase.ExecuteAsync(runId, CancellationToken.None);
        var views = result!;

        Assert.Equal([eligible.DriverId, rejected.DriverId], views.Select(view => view.DriverId));
        Assert.Empty(views[0].RejectionReasons);
        Assert.Equal([EligibilityRejectionReason.DriverNotApproved], views[1].RejectionReasons);
    }

    private sealed class StubStore(
        IReadOnlyList<AllocationCandidateEvaluation>? evaluations) : IAllocationRunStore
    {
        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
            Guid allocationRunId,
            CancellationToken cancellationToken) =>
            Task.FromResult(evaluations);

        public Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
