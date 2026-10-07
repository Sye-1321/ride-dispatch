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

public sealed class RideRequestTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Passengers_and_immediate_scheduled_ride_requests_follow_contracts()
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
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new IncrementingTimeProvider(CurrentTime));
                });
            });

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = application.CreateClient();
        var passengerId = await CreatePassengerAsync(client);

        var linkedId = await CreateAndAssertLinkedRequestAsync(client, passengerId);

        using (var getResponse = await client.GetAsync($"/api/v1/ride-requests/{linkedId}"))
        {
            getResponse.EnsureSuccessStatusCode();
            using var rideRequest = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
            Assert.Equal(linkedId, rideRequest.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(passengerId, rideRequest.RootElement.GetProperty("passengerId").GetGuid());
        }

        var directId = await CreateAndAssertDirectRequestAsync(client);
        var scheduledId = await CreateAndAssertScheduledRequestAsync(client);

        using (var listResponse = await client.GetAsync("/api/v1/ride-requests"))
        {
            listResponse.EnsureSuccessStatusCode();
            using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
            var ids = list.RootElement.EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToArray();
            Assert.Equal([scheduledId, directId, linkedId], ids);
        }

        await AssertInvalidRequestsAsync(client, passengerId);
    }

    private static async Task<Guid> CreatePassengerAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/passengers",
            new { name = "Sara Tesfaye", phoneNumber = "+251911234567" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var passenger = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Sara Tesfaye", passenger.RootElement.GetProperty("name").GetString());
        Assert.Equal("+251911234567", passenger.RootElement.GetProperty("phoneNumber").GetString());
        return passenger.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAndAssertLinkedRequestAsync(
        HttpClient client,
        Guid passengerId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            ImmediateRequest(passengerId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var rideRequest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = rideRequest.RootElement;
        Assert.Equal(passengerId, root.GetProperty("passengerId").GetGuid());
        Assert.Equal("Sara Tesfaye", root.GetProperty("contactName").GetString());
        Assert.Equal("+251911234567", root.GetProperty("contactPhone").GetString());
        Assert.Equal("IMMEDIATE", root.GetProperty("timing").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("requestedPickupAt").ValueKind);
        Assert.Equal("STANDARD", root.GetProperty("requiredVehicleType").GetString());
        Assert.Equal(9.03, root.GetProperty("pickupLatitude").GetDouble());
        Assert.Equal(38.74, root.GetProperty("pickupLongitude").GetDouble());
        Assert.Equal(9.01, root.GetProperty("destinationLatitude").GetDouble());
        Assert.Equal(38.78, root.GetProperty("destinationLongitude").GetDouble());
        Assert.Equal(1200, root.GetProperty("estimatedTripDurationSeconds").GetInt32());
        Assert.Equal(JsonValueKind.String, root.GetProperty("createdAt").ValueKind);
        return root.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAndAssertDirectRequestAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            DirectRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var rideRequest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = rideRequest.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("passengerId").ValueKind);
        Assert.Equal("Hana Gebru", root.GetProperty("contactName").GetString());
        Assert.Equal("+251911000000", root.GetProperty("contactPhone").GetString());
        Assert.Equal("CALL_CENTER", root.GetProperty("bookingSource").GetString());
        return root.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAndAssertScheduledRequestAsync(HttpClient client)
    {
        var requestedPickupAt = CurrentTime.AddDays(1);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            ScheduledRequest(requestedPickupAt));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var rideRequest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = rideRequest.RootElement;
        Assert.Equal("SCHEDULED", root.GetProperty("timing").GetString());
        Assert.Equal(requestedPickupAt, root.GetProperty("requestedPickupAt").GetDateTimeOffset());
        return root.GetProperty("id").GetGuid();
    }

    private static async Task AssertInvalidRequestsAsync(HttpClient client, Guid passengerId)
    {
        using var missingPassenger = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            ImmediateRequest(Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.NotFound, missingPassenger.StatusCode);

        using var ambiguous = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            new
            {
                passengerId,
                contactName = "Hana",
                contactPhone = "+251911000000",
                bookingSource = "APP",
                timing = "IMMEDIATE",
                pickupLatitude = 9.03,
                pickupLongitude = 38.74,
                destinationLatitude = 9.01,
                destinationLongitude = 38.78,
                requiredVehicleType = "STANDARD",
                estimatedTripDurationSeconds = 1200,
            });
        Assert.Equal(HttpStatusCode.BadRequest, ambiguous.StatusCode);

        using var invalidTiming = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            new
            {
                contactName = "Hana",
                contactPhone = "+251911000000",
                bookingSource = "CALL_CENTER",
                timing = "IMMEDIATE",
                pickupLatitude = 9.03,
                pickupLongitude = 38.74,
                destinationLatitude = 9.01,
                destinationLongitude = 38.78,
                requiredVehicleType = "STANDARD",
                requestedPickupAt = CurrentTime.AddHours(1),
                estimatedTripDurationSeconds = 1200,
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidTiming.StatusCode);

        using var invalidCoordinate = await client.PostAsJsonAsync(
            "/api/v1/ride-requests",
            new
            {
                contactName = "Hana",
                contactPhone = "+251911000000",
                bookingSource = "CALL_CENTER",
                timing = "IMMEDIATE",
                pickupLatitude = 91,
                pickupLongitude = 38.74,
                destinationLatitude = 9.01,
                destinationLongitude = 38.78,
                requiredVehicleType = "STANDARD",
                estimatedTripDurationSeconds = 1200,
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinate.StatusCode);
    }

    private static object ImmediateRequest(Guid passengerId) => new
    {
        passengerId,
        bookingSource = "APP",
        timing = "IMMEDIATE",
        pickupLatitude = 9.03,
        pickupLongitude = 38.74,
        destinationLatitude = 9.01,
        destinationLongitude = 38.78,
        requiredVehicleType = "STANDARD",
        estimatedTripDurationSeconds = 1200,
    };

    private static object DirectRequest() => new
    {
        contactName = "Hana Gebru",
        contactPhone = "+251911000000",
        bookingSource = "CALL_CENTER",
        timing = "IMMEDIATE",
        pickupLatitude = 9.03,
        pickupLongitude = 38.74,
        destinationLatitude = 9.06,
        destinationLongitude = 38.72,
        requiredVehicleType = "XL",
        estimatedTripDurationSeconds = 1800,
    };

    private static object ScheduledRequest(DateTimeOffset requestedPickupAt) => new
    {
        contactName = "Hana Gebru",
        contactPhone = "+251911000000",
        bookingSource = "CALL_CENTER",
        timing = "SCHEDULED",
        pickupLatitude = 9.03,
        pickupLongitude = 38.74,
        destinationLatitude = 9.06,
        destinationLongitude = 38.72,
        requiredVehicleType = "STANDARD",
        requestedPickupAt,
        estimatedTripDurationSeconds = 1800,
    };

    private sealed class IncrementingTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        private long callCount;

        public override DateTimeOffset GetUtcNow() =>
            currentTime.AddSeconds(Interlocked.Increment(ref callCount));
    }
}
