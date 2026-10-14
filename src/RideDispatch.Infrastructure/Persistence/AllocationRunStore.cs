using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public Task<AllocationRun?> FindBySourceOfferIdAsync(Guid sourceOfferId, CancellationToken cancellationToken) =>
        dbContext.AllocationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(allocationRun => allocationRun.SourceOfferId == sourceOfferId, cancellationToken);

    public async Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
        Guid allocationRunId,
        CancellationToken cancellationToken)
    {
        var runExists = await dbContext.AllocationRuns
            .AsNoTracking()
            .AnyAsync(allocationRun => allocationRun.Id == allocationRunId, cancellationToken);
        if (!runExists)
        {
            return null;
        }

        return await dbContext.AllocationCandidateEvaluations
            .AsNoTracking()
            .Include(evaluation => evaluation.Rejections)
            .Where(evaluation => evaluation.AllocationRunId == allocationRunId)
            .OrderBy(evaluation => evaluation.Rank == null)
            .ThenBy(evaluation => evaluation.Rank)
            .ThenBy(evaluation => evaluation.DriverId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_allocation_runs_source_offer_id",
            })
        {
            dbContext.ChangeTracker.Clear();
            throw new ReplacementAllocationRunAlreadyExistsException(exception);
        }
    }
}
