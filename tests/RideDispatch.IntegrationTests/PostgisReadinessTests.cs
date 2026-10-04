using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class PostgisReadinessTests
{
    [Fact]
    public async Task Liveness_is_healthy_and_readiness_is_unavailable_when_database_is_unreachable()
    {
        const string unreachableDatabase =
            "Host=localhost;Port=1;Database=ride_dispatch;Username=ride_dispatch;Password=ride_dispatch;Timeout=1;Command Timeout=1";

        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", unreachableDatabase);
            });

        using var client = application.CreateClient();
        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        liveResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
    }

    [Fact]
    public async Task Readiness_is_healthy_with_migrated_postgis_database()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch")
            .WithUsername("ride_dispatch")
            .WithPassword("ride_dispatch")
            .Build();

        await database.StartAsync();

        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            });

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await dbContext.Database.MigrateAsync();

            await dbContext.Database.OpenConnectionAsync();
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT PostGIS_Version();";

            var postgisVersion = await command.ExecuteScalarAsync();

            Assert.NotNull(postgisVersion);
            Assert.NotEmpty(Assert.IsType<string>(postgisVersion));
        }

        using var client = application.CreateClient();
        using var response = await client.GetAsync("/health/ready");

        response.EnsureSuccessStatusCode();
    }
}
