using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.DriverFinancialStanding;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DriverFinancialStandingStore(DispatchDbContext dbContext)
    : IDriverFinancialStandingStore
{
    public async Task<DriverFinancialStandingView> UpsertAsync(
        Guid driverId,
        decimal commissionBalance,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var standing = await dbContext.DriverFinancialStandings.FindAsync(
            [driverId],
            cancellationToken);

        if (standing is null)
        {
            standing = new DriverFinancialStandingRecord(driverId, commissionBalance, updatedAt);
            await dbContext.DriverFinancialStandings.AddAsync(standing, cancellationToken);
        }
        else
        {
            standing.Replace(commissionBalance, updatedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(standing);
    }

    public async Task<DriverFinancialStandingView?> FindByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var standing = await dbContext.DriverFinancialStandings
            .AsNoTracking()
            .SingleOrDefaultAsync(standing => standing.DriverId == driverId, cancellationToken);

        return standing is null ? null : ToView(standing);
    }

    private static DriverFinancialStandingView ToView(DriverFinancialStandingRecord standing) =>
        new(standing.DriverId, standing.CommissionBalance, standing.UpdatedAt);
}
