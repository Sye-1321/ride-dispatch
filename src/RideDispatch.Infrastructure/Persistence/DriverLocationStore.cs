using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RideDispatch.Application.DriverLocations;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DriverLocationStore(DispatchDbContext dbContext) : IDriverLocationStore
{
    public async Task<DriverLocationView> UpsertAsync(
        Guid driverId,
        double latitude,
        double longitude,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        var position = CreatePoint(latitude, longitude);
        var location = await dbContext.DriverLocations.FindAsync([driverId], cancellationToken);

        if (location is null)
        {
            location = new DriverLocationRecord(driverId, position, recordedAt);
            await dbContext.DriverLocations.AddAsync(location, cancellationToken);
        }
        else
        {
            location.Replace(position, recordedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(location);
    }

    public async Task<DriverLocationView?> FindByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.DriverLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(location => location.DriverId == driverId, cancellationToken);

        return location is null ? null : ToView(location);
    }

    public async Task<IReadOnlyList<NearbyDriverView>> FindWithinRadiusAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken)
    {
        var origin = CreatePoint(latitude, longitude);

        var matches = await dbContext.DriverLocations
            .AsNoTracking()
            .Where(location => location.Position.IsWithinDistance(origin, radiusMeters))
            .Select(location => new
            {
                location.DriverId,
                location.Position,
                location.RecordedAt,
                DistanceMeters = location.Position.Distance(origin),
            })
            .OrderBy(location => location.DistanceMeters)
            .ThenBy(location => location.DriverId)
            .ToListAsync(cancellationToken);

        return matches
            .Select(location => new NearbyDriverView(
                location.DriverId,
                location.Position.Y,
                location.Position.X,
                location.RecordedAt,
                location.DistanceMeters))
            .ToList();
    }

    private static Point CreatePoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };

    private static DriverLocationView ToView(DriverLocationRecord location) =>
        new(
            location.DriverId,
            location.Position.Y,
            location.Position.X,
            location.RecordedAt);
}
