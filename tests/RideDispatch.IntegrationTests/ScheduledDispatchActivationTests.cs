using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class ScheduledDispatchActivationTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Activation_window_retry_allocation_boundaries_schema_and_concurrency_are_enforced()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
        await database.StartAsync();

        var clock = new MutableTimeProvider(InitialTime);
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        await using (var scope = application.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
        }

        using var client = application.CreateClient();
        var pickup = InitialTime.AddHours(1);
        var rideId = await CreateScheduledRideAsync(client, pickup);

        using (var beforeRun = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, beforeRun.StatusCode);
        }
        using (var tooEarly = await client.PostAsync($"/api/v1/ride-requests/{rideId}/activate-dispatch", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        }

        clock.Set(pickup.AddMinutes(-15));
        DateTimeOffset activatedAt;
        using (var activation = await client.PostAsync($"/api/v1/ride-requests/{rideId}/activate-dispatch", null))
        {
            Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
            using var body = JsonDocument.Parse(await activation.Content.ReadAsStringAsync());
            activatedAt = body.RootElement.GetProperty("dispatchActivatedAt").GetDateTimeOffset();
            Assert.Equal(clock.GetUtcNow(), activatedAt);
        }

        clock.Set(pickup.AddMinutes(-5));
        using (var retry = await client.PostAsync($"/api/v1/ride-requests/{rideId}/activate-dispatch", null))
        {
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            using var body = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
            Assert.Equal(activatedAt, body.RootElement.GetProperty("dispatchActivatedAt").GetDateTimeOffset());
        }

        clock.Set(pickup);
        using (var exactPickup = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null))
        {
            Assert.Equal(HttpStatusCode.Created, exactPickup.StatusCode);
        }
        clock.Set(pickup.AddTicks(1));
        using (var elapsed = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, elapsed.StatusCode);
        }

        await AssertSchemaAsync(application);
        await AssertRealConcurrencyAsync(application);
    }

    private static async Task<Guid> CreateScheduledRideAsync(HttpClient client, DateTimeOffset pickup)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = "Scheduled Passenger",
            contactPhone = "+251911000000",
            bookingSource = "CALL_CENTER",
            timing = "SCHEDULED",
            pickupLatitude = 9.03,
            pickupLongitude = 38.74,
            destinationLatitude = 9.01,
            destinationLongitude = 38.78,
            requiredVehicleType = "STANDARD",
            requestedPickupAt = pickup,
            estimatedTripDurationSeconds = 1200,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("dispatchActivatedAt").ValueKind);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertSchemaAsync(WebApplicationFactory<Program> application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'ride_requests' AND column_name = 'dispatch_activated_at'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("timestamp with time zone", reader.GetString(0));
        Assert.Equal("YES", reader.GetString(1));
        await reader.CloseAsync();

        command.CommandText = """
            SELECT count(*) FROM pg_constraint
            WHERE conname IN ('ck_ride_requests_dispatch_activation_scheduled',
              'ck_ride_requests_dispatch_activation_after_creation',
              'ck_ride_requests_dispatch_activation_before_pickup')
            """;
        Assert.Equal(3L, (long)(await command.ExecuteScalarAsync())!);
    }

    private static async Task AssertRealConcurrencyAsync(WebApplicationFactory<Program> application)
    {
        Guid id;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            var ride = RideRequest.Create(null, "Concurrent", "+251911000001", BookingSource.CallCenter,
                RideTiming.Scheduled, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
                InitialTime.AddHours(2), 1200, InitialTime);
            id = ride.Id;
            db.RideRequests.Add(ride);
            await db.SaveChangesAsync();
        }

        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IRideRequestStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IRideRequestStore>();
        var firstRide = (await first.FindByIdTrackedAsync(id, CancellationToken.None))!;
        var secondRide = (await second.FindByIdTrackedAsync(id, CancellationToken.None))!;
        var activation = InitialTime.AddHours(1).AddMinutes(45);
        firstRide.ActivateDispatch(activation, TimeSpan.FromMinutes(15));
        secondRide.ActivateDispatch(activation, TimeSpan.FromMinutes(15));
        await first.SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<RideRequestActivationConcurrencyException>(() =>
            second.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(activation, (await first.FindByIdAsync(id, CancellationToken.None))!.DispatchActivatedAt);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public void Set(DateTimeOffset value) => current = value;
        public override DateTimeOffset GetUtcNow() => current;
    }
}
