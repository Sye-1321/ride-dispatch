using Microsoft.EntityFrameworkCore;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        base.OnModelCreating(modelBuilder);
    }
}
