using RideDispatch.Application.Dispatch.Assignments;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Assignments;

public sealed class TransitionAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Complete_missing_reads_no_clock()
    {
        var fixture = new Fixture(null, Now);
        var result = await fixture.Complete.ExecuteAsync(Guid.NewGuid(), default);
        Assert.Equal(CompleteAssignmentOutcome.AssignmentNotFound, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Theory]
    [InlineData(AssignmentStatus.Completed)]
    [InlineData(AssignmentStatus.Cancelled)]
    public async Task Complete_terminal_conflicts_without_clock(AssignmentStatus status)
    {
        var assignment = AssignmentAt(Now);
        if (status == AssignmentStatus.Completed) assignment.Complete(Now);
        else assignment.Cancel(Now);
        var fixture = new Fixture(assignment, Now);
        var result = await fixture.Complete.ExecuteAsync(assignment.Id, default);
        Assert.Equal(CompleteAssignmentOutcome.InvalidState, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Complete_before_start_conflicts_once_without_save()
    {
        var assignment = AssignmentAt(Now.AddMinutes(1));
        var fixture = new Fixture(assignment, Now);
        var result = await fixture.Complete.ExecuteAsync(assignment.Id, default);
        Assert.Equal(CompleteAssignmentOutcome.AssignmentNotStarted, result.Outcome);
        Assert.Equal(1, fixture.Clock.ReadCount);
        Assert.Equal(0, fixture.Store.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Complete_at_or_after_start_succeeds_with_one_clock_read(int minutesAfter)
    {
        var assignment = AssignmentAt(Now);
        var fixture = new Fixture(assignment, Now.AddMinutes(minutesAfter));
        var result = await fixture.Complete.ExecuteAsync(assignment.Id, default);
        Assert.Equal(CompleteAssignmentOutcome.Success, result.Outcome);
        Assert.Equal(AssignmentStatus.Completed, result.Assignment!.Status);
        Assert.Equal(1, fixture.Clock.ReadCount);
        Assert.Equal(1, fixture.Store.SaveCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Cancel_before_or_after_start_succeeds_with_one_clock_read(int minutesFromStart)
    {
        var assignment = AssignmentAt(Now.AddMinutes(1));
        var fixture = new Fixture(assignment, assignment.StartsAt.AddMinutes(minutesFromStart));
        var result = await fixture.Cancel.ExecuteAsync(assignment.Id, default);
        Assert.Equal(CancelAssignmentOutcome.Success, result.Outcome);
        Assert.Equal(AssignmentStatus.Cancelled, result.Assignment!.Status);
        Assert.Equal(1, fixture.Clock.ReadCount);
    }

    [Theory]
    [InlineData(AssignmentStatus.Completed)]
    [InlineData(AssignmentStatus.Cancelled)]
    public async Task Cancel_terminal_conflicts_without_clock(AssignmentStatus status)
    {
        var assignment = AssignmentAt(Now);
        if (status == AssignmentStatus.Completed) assignment.Complete(Now);
        else assignment.Cancel(Now);
        var fixture = new Fixture(assignment, Now);
        var result = await fixture.Cancel.ExecuteAsync(assignment.Id, default);
        Assert.Equal(CancelAssignmentOutcome.InvalidState, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Cancel_missing_reads_no_clock()
    {
        var fixture = new Fixture(null, Now);
        var result = await fixture.Cancel.ExecuteAsync(Guid.NewGuid(), default);
        Assert.Equal(CancelAssignmentOutcome.AssignmentNotFound, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrency_loss_returns_current_terminal_state(bool completing)
    {
        var assignment = AssignmentAt(Now);
        var winner = AssignmentAt(Now);
        if (completing) winner.Cancel(Now);
        else winner.Complete(Now);
        var fixture = new Fixture(assignment, Now) { Store = { ConcurrencyWinner = winner } };

        if (completing)
        {
            var result = await fixture.Complete.ExecuteAsync(assignment.Id, default);
            Assert.Equal(CompleteAssignmentOutcome.InvalidState, result.Outcome);
            Assert.Equal(AssignmentStatus.Cancelled, result.Assignment!.Status);
        }
        else
        {
            var result = await fixture.Cancel.ExecuteAsync(assignment.Id, default);
            Assert.Equal(CancelAssignmentOutcome.InvalidState, result.Outcome);
            Assert.Equal(AssignmentStatus.Completed, result.Assignment!.Status);
        }
        Assert.Equal(1, fixture.Clock.ReadCount);
    }

    private static Assignment AssignmentAt(DateTimeOffset startsAt) => Assignment.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startsAt, TimeSpan.FromMinutes(20), Now);

    private sealed class Fixture
    {
        public Fixture(Assignment? assignment, DateTimeOffset now)
        {
            Store = new StoreStub(assignment);
            Clock = new CountingClock(now);
            Complete = new CompleteAssignment(Store, Clock);
            Cancel = new CancelAssignment(Store, Clock);
        }
        public StoreStub Store { get; }
        public CountingClock Clock { get; }
        public CompleteAssignment Complete { get; }
        public CancelAssignment Cancel { get; }
    }

    private sealed class CountingClock(DateTimeOffset now) : TimeProvider
    {
        public int ReadCount { get; private set; }
        public override DateTimeOffset GetUtcNow() { ReadCount++; return now; }
    }

    private sealed class StoreStub(Assignment? assignment) : IAssignmentStore
    {
        public Assignment? ConcurrencyWinner { get; set; }
        public int SaveCount { get; private set; }
        public Task<Assignment?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(assignment?.Id == id ? assignment : null);
        public Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => Task.FromResult(ConcurrencyWinner);
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return ConcurrencyWinner is null ? Task.CompletedTask : Task.FromException(new AssignmentStateConcurrencyException(new Exception()));
        }
        public Task AddAsync(Assignment value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Assignment?> FindByOfferIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Assignment?> FindNonCancelledByRideRequestIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasDriverOverlapAsync(Guid id, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) => throw new NotSupportedException();
    }
}
