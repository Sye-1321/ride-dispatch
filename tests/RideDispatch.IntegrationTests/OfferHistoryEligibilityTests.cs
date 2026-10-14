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

public sealed class OfferHistoryEligibilityTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Declined_driver_is_evidenced_and_next_driver_is_recommended()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client, "Declined Ride");
        var driverA = await CreateEligibleDriverAsync(environment.Client, "Declined A", 9.031);
        var driverB = await CreateEligibleDriverAsync(environment.Client, "Declined B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId, driverA);
        var offer = await CreateOfferAsync(environment.Client, firstRun);
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/decline", null))
            Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        var secondRun = await CreateRunAsync(environment.Client, rideId, driverB);
        var evidence = await GetEvidenceAsync(environment.Client, secondRun);
        AssertRejected(evidence, driverA, "PREVIOUS_OFFER_DECLINED");
        AssertEligibleRankOne(evidence, driverB);
    }

    [Fact]
    public async Task Expired_driver_is_evidenced_and_next_driver_is_recommended()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client, "Expired Ride");
        var driverA = await CreateEligibleDriverAsync(environment.Client, "Expired A", 9.031);
        var driverB = await CreateEligibleDriverAsync(environment.Client, "Expired B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId, driverA);
        var offer = await CreateOfferAsync(environment.Client, firstRun);
        environment.Clock.Set(offer.ExpiresAt);
        using (var expire = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/accept", null))
            Assert.Equal(HttpStatusCode.Conflict, expire.StatusCode);
        using (var persisted = await environment.Client.GetAsync($"/api/v1/offers/{offer.Id}"))
        {
            var body = await persisted.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("EXPIRED", body.GetProperty("status").GetString());
        }

        var secondRun = await CreateRunAsync(environment.Client, rideId, driverB);
        var evidence = await GetEvidenceAsync(environment.Client, secondRun);
        AssertRejected(evidence, driverA, "PREVIOUS_OFFER_EXPIRED");
        AssertEligibleRankOne(evidence, driverB);
    }

    [Fact]
    public async Task Declined_offer_history_is_scoped_to_ride_request()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideX = await CreateRideAsync(environment.Client, "Ride X");
        var driverA = await CreateEligibleDriverAsync(environment.Client, "Scoped A", 9.031);
        await CreateEligibleDriverAsync(environment.Client, "Scoped B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideX, driverA);
        var offer = await CreateOfferAsync(environment.Client, firstRun);
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/decline", null))
            decline.EnsureSuccessStatusCode();

        var rideY = await CreateRideAsync(environment.Client, "Ride Y");
        var rideYRun = await CreateRunAsync(environment.Client, rideY, driverA);
        var evidence = await GetEvidenceAsync(environment.Client, rideYRun);
        var driverEvidence = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverA);
        Assert.True(driverEvidence.GetProperty("isEligible").GetBoolean());
        var reasons = driverEvidence.GetProperty("rejectionReasons").EnumerateArray()
            .Select(reason => reason.GetString()).ToArray();
        Assert.DoesNotContain("PREVIOUS_OFFER_DECLINED", reasons);
        Assert.DoesNotContain("PREVIOUS_OFFER_EXPIRED", reasons);
    }

    [Fact]
    public async Task Withdrawn_offer_does_not_exclude_driver_on_same_ride()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client, "Withdrawn Ride");
        var driverA = await CreateEligibleDriverAsync(environment.Client, "Withdrawn A", 9.031);
        await CreateEligibleDriverAsync(environment.Client, "Withdrawn B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId, driverA);
        var offer = await CreateOfferAsync(environment.Client, firstRun);
        using (var withdraw = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/withdraw", null))
            withdraw.EnsureSuccessStatusCode();

        var secondRun = await CreateRunAsync(environment.Client, rideId, driverA);
        var evidence = await GetEvidenceAsync(environment.Client, secondRun);
        var driverEvidence = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverA);
        Assert.True(driverEvidence.GetProperty("isEligible").GetBoolean());
        var reasons = driverEvidence.GetProperty("rejectionReasons").EnumerateArray()
            .Select(reason => reason.GetString()).ToArray();
        Assert.DoesNotContain("PREVIOUS_OFFER_DECLINED", reasons);
        Assert.DoesNotContain("PREVIOUS_OFFER_EXPIRED", reasons);
    }

    private static void AssertRejected(JsonElement[] evidence, Guid driverId, string reason)
    {
        var candidate = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverId);
        Assert.False(candidate.GetProperty("isEligible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("rank").ValueKind);
        Assert.Contains(reason, candidate.GetProperty("rejectionReasons")
            .EnumerateArray().Select(value => value.GetString()));
    }

    private static void AssertEligibleRankOne(JsonElement[] evidence, Guid driverId)
    {
        var candidate = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverId);
        Assert.True(candidate.GetProperty("isEligible").GetBoolean());
        Assert.Equal(1, candidate.GetProperty("rank").GetInt32());
    }

    private static async Task<JsonElement[]> GetEvidenceAsync(HttpClient client, Guid runId)
    {
        using var response = await client.GetAsync($"/api/v1/allocation-runs/{runId}/candidate-evaluations");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
    }

    private static async Task<Guid> CreateRideAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = name,
            contactPhone = "+251911000088",
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
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateEligibleDriverAsync(HttpClient client, string name, double latitude)
    {
        using var create = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var response in new[]
        {
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/approval", new { status = "APPROVED" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/operational-status", new { status = "AVAILABLE" }),
            await client.PostAsJsonAsync($"/api/v1/drivers/{id}/vehicle", new { type = "STANDARD", plateNumber = $"HIS-{id:N}"[..16] }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/location", new { latitude, longitude = 38.74 }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/financial-standing", new { commissionBalance = 1m }),
        })
        {
            using (response) response.EnsureSuccessStatusCode();
        }
        return id;
    }

    private static async Task<Guid> CreateRunAsync(HttpClient client, Guid rideId, Guid expectedDriverId)
    {
        using var response = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedDriverId, run.GetProperty("recommendedDriverId").GetGuid());
        return run.GetProperty("id").GetGuid();
    }

    private static async Task<OfferData> CreateOfferAsync(HttpClient client, Guid runId)
    {
        using var response = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(offer.GetProperty("id").GetGuid(), offer.GetProperty("expiresAt").GetDateTimeOffset());
    }

    private sealed record OfferData(Guid Id, DateTimeOffset ExpiresAt);

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database, WebApplicationFactory<Program> application,
            HttpClient client, MutableTimeProvider clock)
        { Database = database; Application = application; Client = client; Clock = clock; }
        private PostgreSqlContainer Database { get; }
        private WebApplicationFactory<Program> Application { get; }
        public HttpClient Client { get; }
        public MutableTimeProvider Clock { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
            await database.StartAsync();
            var clock = new MutableTimeProvider(InitialTime);
            var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                });
            });
            await using (var scope = application.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
            return new(database, application, application.CreateClient(), clock);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Application.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    public sealed class MutableTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
        public void Set(DateTimeOffset value) => currentTime = value;
    }
}
