using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchCandidateStore(DispatchDbContext dbContext) : IDispatchCandidateStore
{
    public async Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
        double pickupLatitude,
        double pickupLongitude,
        double radiusMeters,
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
                standing == null ? null : (decimal?)standing.CommissionBalance))
            .ToListAsync(cancellationToken);
    }
}
