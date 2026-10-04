using Microsoft.EntityFrameworkCore;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Vehicles;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<DriverLocationRecord> DriverLocations => Set<DriverLocationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
