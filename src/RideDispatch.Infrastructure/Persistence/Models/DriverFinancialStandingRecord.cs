namespace RideDispatch.Infrastructure.Persistence.Models;

public sealed class DriverFinancialStandingRecord
{
    private DriverFinancialStandingRecord()
    {
    }

    public DriverFinancialStandingRecord(
        Guid driverId,
        decimal commissionBalance,
        DateTimeOffset updatedAt)
    {
        DriverId = driverId;
        CommissionBalance = commissionBalance;
        UpdatedAt = updatedAt;
    }

    public Guid DriverId { get; private set; }

    public decimal CommissionBalance { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(decimal commissionBalance, DateTimeOffset updatedAt)
    {
        CommissionBalance = commissionBalance;
        UpdatedAt = updatedAt;
    }
}
