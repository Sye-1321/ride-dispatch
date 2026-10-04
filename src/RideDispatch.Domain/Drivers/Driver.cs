namespace RideDispatch.Domain.Drivers;

public sealed class Driver
{
    private Driver()
    {
        Name = null!;
    }

    private Driver(Guid id, string name)
    {
        Id = id;
        Name = name;
        ApprovalStatus = DriverApprovalStatus.Pending;
        OperationalStatus = DriverOperationalStatus.Offline;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public DriverApprovalStatus ApprovalStatus { get; private set; }

    public DriverOperationalStatus OperationalStatus { get; private set; }

    public DateTimeOffset? AvailableSince { get; private set; }

    public static Driver Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var normalizedName = name.Trim();
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("Driver name must not be empty or whitespace.", nameof(name));
        }

        if (normalizedName.Length > 200)
        {
            throw new ArgumentException("Driver name must not exceed 200 characters.", nameof(name));
        }

        return new Driver(Guid.CreateVersion7(), normalizedName);
    }

    public void SetApprovalStatus(DriverApprovalStatus status)
    {
        ApprovalStatus = status;

        if (status != DriverApprovalStatus.Approved &&
            OperationalStatus == DriverOperationalStatus.Available)
        {
            OperationalStatus = DriverOperationalStatus.Offline;
            AvailableSince = null;
        }
    }

    public bool TrySetOperationalStatus(DriverOperationalStatus status, DateTimeOffset currentUtcTime)
    {
        if (status == DriverOperationalStatus.Available)
        {
            if (ApprovalStatus != DriverApprovalStatus.Approved)
            {
                return false;
            }

            if (OperationalStatus != DriverOperationalStatus.Available)
            {
                OperationalStatus = status;
                AvailableSince = currentUtcTime;
            }

            return true;
        }

        OperationalStatus = status;
        AvailableSince = null;
        return true;
    }
}
