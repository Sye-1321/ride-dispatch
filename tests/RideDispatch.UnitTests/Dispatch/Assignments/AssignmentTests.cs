using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Assignments;

public sealed class AssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_produces_uuid_v7_and_normalized_derived_window()
    {
        var assignment = Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Now.ToOffset(TimeSpan.FromHours(3)), TimeSpan.FromMinutes(20), Now.ToOffset(TimeSpan.FromHours(3)));

        Assert.Equal(7, assignment.Id.Version);
        Assert.Equal(TimeSpan.Zero, assignment.StartsAt.Offset);
        Assert.Equal(TimeSpan.Zero, assignment.CreatedAt.Offset);
        Assert.Equal(assignment.StartsAt.AddMinutes(20), assignment.EndsAt);
        Assert.Equal(assignment.StartsAt, assignment.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Duration_must_be_positive(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Now, TimeSpan.FromSeconds(seconds), Now));

    [Fact]
    public void IDs_must_not_be_empty()
    {
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now, TimeSpan.FromMinutes(1), Now));
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now, TimeSpan.FromMinutes(1), Now));
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now, TimeSpan.FromMinutes(1), Now));
    }

    [Fact]
    public void Start_before_created_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Now.AddTicks(-1), TimeSpan.FromMinutes(1), Now));
}
