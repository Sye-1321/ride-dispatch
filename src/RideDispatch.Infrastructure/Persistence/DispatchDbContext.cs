using Microsoft.EntityFrameworkCore;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
