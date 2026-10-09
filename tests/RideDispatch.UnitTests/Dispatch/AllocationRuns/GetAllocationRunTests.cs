using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.AllocationRuns;

public sealed class GetAllocationRunTests
{
    [Fact]
    public async Task Existing_run_returns_view()
    {
        var run = AllocationRun.Create(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow);
        var useCase = new GetAllocationRun(new StubAllocationRunStore(run));

        var result = await useCase.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(run.Id, result!.Id);
        Assert.Equal(run.RecommendedDriverId, result.RecommendedDriverId);
    }

    [Fact]
    public async Task Missing_run_returns_null()
    {
        var useCase = new GetAllocationRun(new StubAllocationRunStore(null));

        var result = await useCase.ExecuteAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubAllocationRunStore(AllocationRun? allocationRun) : IAllocationRunStore
    {
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(allocationRun?.Id == id ? allocationRun : null);

        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
            Guid allocationRunId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
