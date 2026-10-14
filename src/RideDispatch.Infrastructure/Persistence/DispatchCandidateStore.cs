using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchCandidateStore(DispatchDbContext dbContext) : IDispatchCandidateStore
{
    public async Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
        Guid rideRequestId,
        double pickupLatitude,
        double pickupLongitude,
        double radiusMeters,
        DateTimeOffset requestedStartsAt,
        DateTimeOffset requestedEndsAt,
        CancellationToken cancellationToken)
    {
        var pickup = new Point(pickupLongitude, pickupLatitude) { SRID = 4326 };

        return await (
            from location in dbContext.DriverLocations.AsNoTracking()
            join driver in dbContext.Drivers.AsNoTracking()
                on location.DriverId equals driver.Id
            join vehicle in dbContext.Vehicles.AsNoTracking()
                on location.DriverId equals vehicle.DriverId into vehicles
            from vehicle in vehicles.DefaultIfEmpty()
            join standing in dbContext.DriverFinancialStandings.AsNoTracking()
                on location.DriverId equals standing.DriverId into standings
            from standing in standings.DefaultIfEmpty()
            where location.Position.IsWithinDistance(pickup, radiusMeters)
            orderby location.DriverId
            select new DispatchCandidateSnapshot(
                location.DriverId,
                driver.ApprovalStatus,
                driver.OperationalStatus,
                driver.AvailableSince,
                vehicle == null ? null : (VehicleType?)vehicle.Type,
                location.RecordedAt,
                location.Position.Distance(pickup),
                standing == null ? null : (decimal?)standing.CommissionBalance,
                dbContext.Assignments.Any(assignment =>
                    assignment.Status == AssignmentStatus.Active &&
                    assignment.DriverId == location.DriverId &&
                    assignment.StartsAt < requestedEndsAt &&
                    requestedStartsAt < assignment.EndsAt),
                dbContext.Offers.Any(offer =>
                    offer.DriverId == location.DriverId &&
                    offer.Status == OfferStatus.Declined &&
                    dbContext.AllocationRuns.Any(run =>
                        run.Id == offer.AllocationRunId &&
                        run.RideRequestId == rideRequestId)),
                dbContext.Offers.Any(offer =>
                    offer.DriverId == location.DriverId &&
                    offer.Status == OfferStatus.Expired &&
                    dbContext.AllocationRuns.Any(run =>
                        run.Id == offer.AllocationRunId &&
                        run.RideRequestId == rideRequestId))))
            .ToListAsync(cancellationToken);
    }
}
