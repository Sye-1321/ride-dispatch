using NetTopologySuite.Geometries;

namespace RideDispatch.Infrastructure.Persistence.Models;

public sealed class DriverLocationRecord
{
    private DriverLocationRecord()
    {
        Position = null!;
    }

    public DriverLocationRecord(Guid driverId, Point position, DateTimeOffset recordedAt)
    {
        DriverId = driverId;
        Position = position;
        RecordedAt = recordedAt;
    }

    public Guid DriverId { get; private set; }

    public Point Position { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public void Replace(Point position, DateTimeOffset recordedAt)
    {
        Position = position;
        RecordedAt = recordedAt;
    }
}
