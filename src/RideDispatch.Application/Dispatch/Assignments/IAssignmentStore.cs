using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Assignments;

public interface IAssignmentStore
{
    Task AddAsync(Assignment assignment, CancellationToken cancellationToken);
    Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
    Task<Assignment?> FindByOfferIdAsync(Guid offerId, CancellationToken cancellationToken);
    Task<Assignment?> FindByRideRequestIdAsync(Guid rideRequestId, CancellationToken cancellationToken);
    Task<bool> HasDriverOverlapAsync(Guid driverId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class AssignmentAlreadyExistsException(Exception innerException)
    : Exception("An assignment already exists for the offer or ride request.", innerException);

public sealed class AssignmentScheduleConflictException(Exception innerException)
    : Exception("The driver has an overlapping assignment.", innerException);
