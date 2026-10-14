using Microsoft.EntityFrameworkCore;
using Npgsql;
using RideDispatch.Application.Dispatch.Assignments;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class AssignmentStore(DispatchDbContext dbContext) : IAssignmentStore
{
    public async Task AddAsync(Assignment assignment, CancellationToken cancellationToken) =>
        await dbContext.Assignments.AddAsync(assignment, cancellationToken);
    public Task<Assignment?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Assignments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Assignment?> FindByOfferIdAsync(Guid offerId, CancellationToken cancellationToken) =>
        dbContext.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offerId, cancellationToken);
    public Task<Assignment?> FindNonCancelledByRideRequestIdAsync(Guid rideRequestId, CancellationToken cancellationToken) =>
        dbContext.Assignments.AsNoTracking().SingleOrDefaultAsync(
            x => x.RideRequestId == rideRequestId && x.Status != AssignmentStatus.Cancelled,
            cancellationToken);
    public Task<bool> HasDriverOverlapAsync(Guid driverId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken cancellationToken) =>
        dbContext.Assignments.AsNoTracking().AnyAsync(x =>
            x.Status == AssignmentStatus.Active &&
            x.DriverId == driverId && x.StartsAt < endsAt && startsAt < x.EndsAt,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_assignments_offer_id" or "ux_assignments_ride_request_id" })
        { throw new AssignmentAlreadyExistsException(ex); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: "ex_assignments_driver_time" })
        { throw new AssignmentScheduleConflictException(ex); }
        catch (DbUpdateConcurrencyException ex) when (
            ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is Assignment))
        {
            dbContext.ChangeTracker.Clear();
            throw new AssignmentStateConcurrencyException(ex);
        }
    }
}
