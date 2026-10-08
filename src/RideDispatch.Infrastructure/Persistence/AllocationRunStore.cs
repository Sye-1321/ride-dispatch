using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class AllocationRunStore(DispatchDbContext dbContext) : IAllocationRunStore
{
    public async Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken) =>
        await dbContext.AllocationRuns.AddAsync(allocationRun, cancellationToken);

    public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AllocationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(allocationRun => allocationRun.Id == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
