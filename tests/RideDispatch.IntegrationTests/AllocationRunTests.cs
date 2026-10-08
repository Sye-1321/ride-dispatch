using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class AllocationRunTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_persist_policy_specific_recommendations_and_can_be_retrieved()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch")
            .WithUsername("ride_dispatch")
            .WithPassword("ride_dispatch")
            .Build();

        await database.StartAsync();

        var timeProvider = new MutableTimeProvider(InitialTime);
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(timeProvider);
                });
            });

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = application.CreateClient();
        var rideRequestId = await CreateRideRequestAsync(client);
        var fartherLongerIdleDriverId = await CreateEligibleDriverAsync(
            client,
            "Farther Longer Idle",
            9.05,
            38.74);

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        var nearerNewerDriverId = await CreateEligibleDriverAsync(
            client,
            "Nearer Newer",
            9.031,
            38.74);

        using var nearestResponse = await client.PostAsync(
            $"/api/v1/ride-requests/{rideRequestId}/allocation-runs",
            null);
        Assert.Equal(HttpStatusCode.Created, nearestResponse.StatusCode);
        using var nearestRun = JsonDocument.Parse(await nearestResponse.Content.ReadAsStringAsync());
        var nearestRoot = nearestRun.RootElement;
        var nearestRunId = nearestRoot.GetProperty("id").GetGuid();
        Assert.Equal(rideRequestId, nearestRoot.GetProperty("rideRequestId").GetGuid());
        Assert.Equal("NEAREST", nearestRoot.GetProperty("rankingPolicy").GetString());
        Assert.Equal(nearerNewerDriverId, nearestRoot.GetProperty("recommendedDriverId").GetGuid());
        Assert.Equal(JsonValueKind.String, nearestRoot.GetProperty("createdAt").ValueKind);
        Assert.NotNull(nearestResponse.Headers.Location);

        using var getResponse = await client.GetAsync(nearestResponse.Headers.Location);
        getResponse.EnsureSuccessStatusCode();
        using var persistedRun = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(nearestRoot.ToString(), persistedRun.RootElement.ToString());

        using var longestIdleResponse = await client.PostAsync(
            $"/api/v1/ride-requests/{rideRequestId}/allocation-runs?rankingPolicy=LongestIdle",
            null);
        Assert.Equal(HttpStatusCode.Created, longestIdleResponse.StatusCode);
        using var longestIdleRun = JsonDocument.Parse(
            await longestIdleResponse.Content.ReadAsStringAsync());
        var longestIdleRoot = longestIdleRun.RootElement;
        Assert.NotEqual(nearestRunId, longestIdleRoot.GetProperty("id").GetGuid());
        Assert.Equal("LONGEST_IDLE", longestIdleRoot.GetProperty("rankingPolicy").GetString());
        Assert.Equal(
            fartherLongerIdleDriverId,
            longestIdleRoot.GetProperty("recommendedDriverId").GetGuid());

        using var invalidPolicyResponse = await client.PostAsync(
            $"/api/v1/ride-requests/{rideRequestId}/allocation-runs?rankingPolicy=999",
            null);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPolicyResponse.StatusCode);
    }

    private static async Task<Guid> CreateRideRequestAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            new
            {
                contactName = "Dispatch Passenger",
                contactPhone = "+251911000001",
                bookingSource = "CALL_CENTER",
                timing = "IMMEDIATE",
                pickupLatitude = 9.03,
                pickupLongitude = 38.74,
                destinationLatitude = 9.01,
                destinationLongitude = 38.78,
                requiredVehicleType = "STANDARD",
                estimatedTripDurationSeconds = 1200,
            });
        response.EnsureSuccessStatusCode();

        using var rideRequest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return rideRequest.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateEligibleDriverAsync(
        HttpClient client,
        string name,
        double latitude,
        double longitude)
    {
        using var createResponse = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        createResponse.EnsureSuccessStatusCode();
        using var driver = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var driverId = driver.RootElement.GetProperty("id").GetGuid();

        using (var approvalResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/approval",
            new { status = "APPROVED" }))
        {
            approvalResponse.EnsureSuccessStatusCode();
        }

        using (var statusResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/operational-status",
            new { status = "AVAILABLE" }))
        {
            statusResponse.EnsureSuccessStatusCode();
        }

        using (var vehicleResponse = await client.PostAsJsonAsync(
            $"/api/v1/drivers/{driverId}/vehicle",
            new { type = "STANDARD", plateNumber = $"RUN-{driverId:N}"[..16] }))
        {
            vehicleResponse.EnsureSuccessStatusCode();
        }

        using (var locationResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/location",
            new { latitude, longitude }))
        {
            locationResponse.EnsureSuccessStatusCode();
        }

        using (var standingResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/financial-standing",
            new { commissionBalance = 1m }))
        {
            standingResponse.EnsureSuccessStatusCode();
        }

        return driverId;
    }

    private sealed class MutableTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;

        public void Advance(TimeSpan duration) => currentTime += duration;
    }
}
