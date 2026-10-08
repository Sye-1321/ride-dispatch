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

public sealed class DispatchEligibilityTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Eligibility_applies_all_current_hard_gates_to_nearby_drivers()
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

        await CreateDriverAsync(
            client,
            "E Stale Location",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.03,
            longitude: 38.74,
            commissionBalance: 1m);

        timeProvider.Advance(TimeSpan.FromSeconds(61));

        var rideRequestId = await CreateStandardRideRequestAsync(client);
        var farthestEligibleDriverId = await CreateDriverAsync(
            client,
            "K Farthest Eligible",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.05,
            longitude: 38.74,
            commissionBalance: 1m);

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        var middleEligibleDriverId = await CreateDriverAsync(
            client,
            "J Middle Eligible",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.04,
            longitude: 38.74,
            commissionBalance: 1m);

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        var nearestEligibleDriverId = await CreateDriverAsync(
            client,
            "A Nearest Eligible",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.031,
            longitude: 38.74,
            commissionBalance: 1m);

        await CreateDriverAsync(
            client,
            "B Pending Offline",
            approved: false,
            operationalStatus: null,
            vehicleType: "STANDARD",
            latitude: 9.032,
            longitude: 38.74,
            commissionBalance: 1m);
        await CreateDriverAsync(
            client,
            "C Unavailable",
            approved: true,
            operationalStatus: "UNAVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.033,
            longitude: 38.74,
            commissionBalance: 1m);
        await CreateDriverAsync(
            client,
            "D Wrong Vehicle",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "XL",
            latitude: 9.034,
            longitude: 38.74,
            commissionBalance: 1m);
        await CreateDriverAsync(
            client,
            "F Zero Balance",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.035,
            longitude: 38.74,
            commissionBalance: 0m);
        await CreateDriverAsync(
            client,
            "G Missing Standing",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 9.036,
            longitude: 38.74,
            commissionBalance: null);
        await CreateDriverAsync(
            client,
            "H Outside Radius",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: "STANDARD",
            latitude: 10.0,
            longitude: 38.74,
            commissionBalance: 1m);
        await CreateDriverAsync(
            client,
            "I Missing Vehicle",
            approved: true,
            operationalStatus: "AVAILABLE",
            vehicleType: null,
            latitude: 9.037,
            longitude: 38.74,
            commissionBalance: 1m);

        using var response = await client.GetAsync(
            $"/api/v1/ride-requests/{rideRequestId}/eligible-drivers");
        response.EnsureSuccessStatusCode();

        using var eligibleDrivers = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var drivers = eligibleDrivers.RootElement.EnumerateArray().ToArray();
        Assert.Equal(
            [nearestEligibleDriverId, middleEligibleDriverId, farthestEligibleDriverId],
            drivers.Select(driver => driver.GetProperty("driverId").GetGuid()));
        Assert.True(
            drivers.Select(driver => driver.GetProperty("distanceMeters").GetDouble())
                .SequenceEqual(
                    drivers.Select(driver => driver.GetProperty("distanceMeters").GetDouble())
                        .OrderBy(distance => distance)));
        var driver = drivers[0];
        Assert.InRange(driver.GetProperty("distanceMeters").GetDouble(), 50, 200);
        Assert.Equal(JsonValueKind.String, driver.GetProperty("availableSince").ValueKind);
        Assert.Equal(JsonValueKind.String, driver.GetProperty("locationRecordedAt").ValueKind);
        Assert.False(driver.TryGetProperty("commissionBalance", out _));

        using var longestIdleResponse = await client.GetAsync(
            $"/api/v1/ride-requests/{rideRequestId}/eligible-drivers?rankingPolicy=LongestIdle");
        longestIdleResponse.EnsureSuccessStatusCode();

        using var longestIdleDrivers = JsonDocument.Parse(
            await longestIdleResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            [farthestEligibleDriverId, middleEligibleDriverId, nearestEligibleDriverId],
            longestIdleDrivers.RootElement
                .EnumerateArray()
                .Select(longestIdleDriver => longestIdleDriver.GetProperty("driverId").GetGuid()));

        using var undefinedNumericPolicyResponse = await client.GetAsync(
            $"/api/v1/ride-requests/{rideRequestId}/eligible-drivers?rankingPolicy=999");
        Assert.Equal(HttpStatusCode.BadRequest, undefinedNumericPolicyResponse.StatusCode);

        using var invalidTextPolicyResponse = await client.GetAsync(
            $"/api/v1/ride-requests/{rideRequestId}/eligible-drivers?rankingPolicy=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidTextPolicyResponse.StatusCode);

        using var missingRideResponse = await client.GetAsync(
            $"/api/v1/ride-requests/{Guid.CreateVersion7()}/eligible-drivers");
        Assert.Equal(HttpStatusCode.NotFound, missingRideResponse.StatusCode);
    }

    private static async Task<Guid> CreateStandardRideRequestAsync(HttpClient client)
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

    private static async Task<Guid> CreateDriverAsync(
        HttpClient client,
        string name,
        bool approved,
        string? operationalStatus,
        string? vehicleType,
        double latitude,
        double longitude,
        decimal? commissionBalance)
    {
        using var createResponse = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        createResponse.EnsureSuccessStatusCode();
        using var driver = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var driverId = driver.RootElement.GetProperty("id").GetGuid();

        if (approved)
        {
            using var approvalResponse = await client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/approval",
                new { status = "APPROVED" });
            approvalResponse.EnsureSuccessStatusCode();
        }

        if (operationalStatus is not null)
        {
            using var statusResponse = await client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/operational-status",
                new { status = operationalStatus });
            statusResponse.EnsureSuccessStatusCode();
        }

        if (vehicleType is not null)
        {
            using var vehicleResponse = await client.PostAsJsonAsync(
                $"/api/v1/drivers/{driverId}/vehicle",
                new { type = vehicleType, plateNumber = $"TEST-{driverId:N}"[..16] });
            vehicleResponse.EnsureSuccessStatusCode();
        }

        using (var locationResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/location",
            new { latitude, longitude }))
        {
            locationResponse.EnsureSuccessStatusCode();
        }

        if (commissionBalance is not null)
        {
            using var standingResponse = await client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/financial-standing",
                new { commissionBalance });
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
