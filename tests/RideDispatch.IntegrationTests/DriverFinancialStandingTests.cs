using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class DriverFinancialStandingTests
{
    [Fact]
    public async Task Financial_standing_is_validated_and_stores_only_the_latest_balance()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch")
            .WithUsername("ride_dispatch")
            .WithPassword("ride_dispatch")
            .Build();

        await database.StartAsync();

        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = application.CreateClient();
        var driverId = await CreateDriverAsync(client);

        using var firstUpdate = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/financial-standing",
            new { commissionBalance = 250.50m });
        firstUpdate.EnsureSuccessStatusCode();

        using (var standing = JsonDocument.Parse(await firstUpdate.Content.ReadAsStringAsync()))
        {
            Assert.Equal(driverId, standing.RootElement.GetProperty("driverId").GetGuid());
            Assert.Equal(250.50m, standing.RootElement.GetProperty("commissionBalance").GetDecimal());
            Assert.Equal(JsonValueKind.String, standing.RootElement.GetProperty("updatedAt").ValueKind);
        }

        await AssertBalanceAsync(client, driverId, 250.50m);

        using var secondUpdate = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/financial-standing",
            new { commissionBalance = 75.25m });
        secondUpdate.EnsureSuccessStatusCode();

        await AssertBalanceAsync(client, driverId, 75.25m);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            Assert.Equal(
                1,
                await dbContext.DriverFinancialStandings.CountAsync(
                    standing => standing.DriverId == driverId));
        }

        using var missingDriver = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{Guid.CreateVersion7()}/financial-standing",
            new { commissionBalance = 10m });
        Assert.Equal(HttpStatusCode.NotFound, missingDriver.StatusCode);

        using var negativeBalance = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/financial-standing",
            new { commissionBalance = -1m });
        Assert.Equal(HttpStatusCode.BadRequest, negativeBalance.StatusCode);

        using var excessiveScale = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/financial-standing",
            new { commissionBalance = 100.123m });
        Assert.Equal(HttpStatusCode.BadRequest, excessiveScale.StatusCode);
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/drivers",
            new { name = "Financial Standing Driver" });
        response.EnsureSuccessStatusCode();

        using var driver = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return driver.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertBalanceAsync(
        HttpClient client,
        Guid driverId,
        decimal expectedBalance)
    {
        using var response = await client.GetAsync(
            $"/api/v1/drivers/{driverId}/financial-standing");
        response.EnsureSuccessStatusCode();

        using var standing = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            expectedBalance,
            standing.RootElement.GetProperty("commissionBalance").GetDecimal());
    }
}
