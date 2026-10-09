using Microsoft.EntityFrameworkCore;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Passengers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Passenger> Passengers => Set<Passenger>();

    public DbSet<RideRequest> RideRequests => Set<RideRequest>();

    public DbSet<AllocationRun> AllocationRuns => Set<AllocationRun>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<AllocationCandidateEvaluation> AllocationCandidateEvaluations =>
        Set<AllocationCandidateEvaluation>();

    public DbSet<AllocationCandidateRejection> AllocationCandidateRejections =>
        Set<AllocationCandidateRejection>();

    public DbSet<DriverLocationRecord> DriverLocations => Set<DriverLocationRecord>();

    public DbSet<DriverFinancialStandingRecord> DriverFinancialStandings =>
        Set<DriverFinancialStandingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
