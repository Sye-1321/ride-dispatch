using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class DispatcherOverrideOfferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Override_selects_rank_two_and_preserves_recommendation_and_evidence()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "Recommended", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Alternate", 9.04);
        var run = await CreateRunAsync(environment.Client, ride, driverA);

        using var response = await OverrideAsync(environment.Client, run, driverB, "  Passenger preference  ");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(driverB, offer.GetProperty("driverId").GetGuid());
        Assert.Equal("DISPATCHER_OVERRIDE", offer.GetProperty("selectionMode").GetString());
        Assert.Equal("Passenger preference", offer.GetProperty("overrideReason").GetString());
        using var persistedRun = await environment.Client.GetAsync($"/api/v1/allocation-runs/{run}");
        Assert.Equal(driverA, (await persistedRun.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("recommendedDriverId").GetGuid());
        var evidence = await GetEvidenceAsync(environment.Client, run);
        AssertEligible(evidence, driverA, 1);
        AssertEligible(evidence, driverB, 2);
    }

    [Fact]
    public async Task Rejected_candidate_cannot_be_forced()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var eligible = await CreateDriverAsync(environment.Client, "Eligible", 9.031);
        var rejected = await CreateDriverAsync(environment.Client, "Wrong Vehicle", 9.04, "XL");
        var run = await CreateRunAsync(environment.Client, ride, eligible);
        var before = await GetEvidenceAsync(environment.Client, run);

        using var response = await OverrideAsync(environment.Client, run, rejected, "Force rejected");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNoOfferAsync(environment, run);
        Assert.Equal(JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(await GetEvidenceAsync(environment.Client, run)));
    }

    [Fact]
    public async Task Driver_outside_persisted_evidence_cannot_be_selected()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var recommended = await CreateDriverAsync(environment.Client, "In Snapshot", 9.031);
        var run = await CreateRunAsync(environment.Client, ride, recommended);
        var laterDriver = await CreateDriverAsync(environment.Client, "Created Later", 9.04);

        using var response = await OverrideAsync(environment.Client, run, laterDriver, "Late driver");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNoOfferAsync(environment, run);
    }

    [Fact]
    public async Task Recommendation_cannot_be_override_but_normal_offer_still_succeeds()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var recommended = await CreateDriverAsync(environment.Client, "Recommended Only", 9.031);
        var run = await CreateRunAsync(environment.Client, ride, recommended);

        using (var overrideResponse = await OverrideAsync(
            environment.Client, run, recommended, "Same driver"))
            Assert.Equal(HttpStatusCode.Conflict, overrideResponse.StatusCode);
        using var normal = await environment.Client.PostAsync($"/api/v1/allocation-runs/{run}/offers", null);
        Assert.Equal(HttpStatusCode.Created, normal.StatusCode);
        var offer = await normal.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RECOMMENDED", offer.GetProperty("selectionMode").GetString());
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("overrideReason").ValueKind);
    }

    [Fact]
    public async Task Accepted_override_creates_assignment_for_selected_driver()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "Assignment A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Assignment B", 9.04);
        var run = await CreateRunAsync(environment.Client, ride, driverA);
        using var create = await OverrideAsync(environment.Client, run, driverB, "Select B");
        var offerId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var accept = await environment.Client.PostAsync($"/api/v1/offers/{offerId}/accept", null))
            accept.EnsureSuccessStatusCode();
        using var assignment = await environment.Client.PostAsync($"/api/v1/offers/{offerId}/assignments", null);
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        Assert.Equal(driverB, (await assignment.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("driverId").GetGuid());
    }

    [Fact]
    public async Task Declined_override_excludes_actual_driver_and_recommends_original_driver()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var ride = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "History A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "History B", 9.04);
        var run = await CreateRunAsync(environment.Client, ride, driverA);
        using var create = await OverrideAsync(environment.Client, run, driverB, "Select B");
        var offerId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{offerId}/decline", null))
            decline.EnsureSuccessStatusCode();

        using var replacement = await environment.Client.PostAsync(
            $"/api/v1/offers/{offerId}/replacement-dispatch", null);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        var body = await replacement.Content.ReadFromJsonAsync<JsonElement>();
        var replacementRun = body.GetProperty("allocationRun");
        Assert.Equal(driverA, replacementRun.GetProperty("recommendedDriverId").GetGuid());
        var evidence = await GetEvidenceAsync(environment.Client, replacementRun.GetProperty("id").GetGuid());
        AssertEligible(evidence, driverA, 1);
        var failed = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverB);
        Assert.Contains("PREVIOUS_OFFER_DECLINED", failed.GetProperty("rejectionReasons")
            .EnumerateArray().Select(reason => reason.GetString()));
    }

    [Fact]
    public async Task Normal_and_override_race_for_same_unique_offer_slot()
    {
        using var barrier = new Barrier(2);
        await using var environment = await TestEnvironment.CreateAsync(services =>
        {
            services.RemoveAll<IOfferStore>();
            services.AddScoped<IOfferStore>(provider => new RacingOfferStore(
                new OfferStore(provider.GetRequiredService<DispatchDbContext>()), barrier));
        });
        var ride = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "Race A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Race B", 9.04);
        var run = await CreateRunAsync(environment.Client, ride, driverA);

        var responses = await Task.WhenAll(
            environment.Client.PostAsync($"/api/v1/allocation-runs/{run}/offers", null),
            environment.Client.PostAsJsonAsync($"/api/v1/allocation-runs/{run}/offers/override",
                new { driverId = driverB, reason = "Race override" }));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using var scope = environment.Application.Services.CreateAsyncScope();
        var offers = await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Offers
            .AsNoTracking().Where(offer => offer.AllocationRunId == run).ToListAsync();
        var persisted = Assert.Single(offers);
        if (persisted.SelectionMode == OfferSelectionMode.Recommended)
        {
            Assert.Equal(driverA, persisted.DriverId);
            Assert.Null(persisted.OverrideReason);
        }
        else
        {
            Assert.Equal(OfferSelectionMode.DispatcherOverride, persisted.SelectionMode);
            Assert.Equal(driverB, persisted.DriverId);
            Assert.Equal("Race override", persisted.OverrideReason);
        }
    }

    [Fact]
    public async Task Database_keeps_offer_uniqueness_and_audit_check()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using var scope = environment.Application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'offers' AND indexname = 'ux_offers_allocation_run_id';
            """;
        var index = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("UNIQUE", index, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allocation_run_id", index, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE", index, StringComparison.OrdinalIgnoreCase);
        command.CommandText = """
            SELECT pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conname = 'ck_offers_selection_override_reason';
            """;
        var check = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("Recommended", check, StringComparison.Ordinal);
        Assert.Contains("DispatcherOverride", check, StringComparison.Ordinal);
        Assert.Contains("override_reason", check, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scheduled_override_at_exact_pickup_succeeds_and_after_pickup_returns_conflict()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var pickup = Now.AddMinutes(10);
        var ride = await CreateScheduledRideAsync(environment.Client, pickup);
        var driverA = await CreateDriverAsync(environment.Client, "Sched Rec", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Sched Alt", 9.04);
        var run = await CreateRunAsync(environment.Client, ride, driverA);

        // At exact pickup: override succeeds
        environment.Clock.Set(pickup);
        using var exact = await OverrideAsync(environment.Client, run, driverB, "Exact pickup override");
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);

        // For another scheduled ride whose pickup has passed: override returns Conflict
        var secondPickup = Now.AddMinutes(10);
        environment.Clock.Set(Now);
        var secondRide = await CreateScheduledRideAsync(environment.Client, secondPickup);
        var secondRun = await CreateRunAsync(environment.Client, secondRide, driverA);

        environment.Clock.Set(secondPickup.AddSeconds(1));
        using var elapsed = await OverrideAsync(environment.Client, secondRun, driverB, "Late override");
        Assert.Equal(HttpStatusCode.Conflict, elapsed.StatusCode);
        await AssertNoOfferAsync(environment, secondRun);
    }

    private static async Task<Guid> CreateScheduledRideAsync(HttpClient client, DateTimeOffset pickupAt)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = "Scheduled Override Passenger",
            contactPhone = "+251911000077",
            bookingSource = "CALL_CENTER",
            timing = "SCHEDULED",
            pickupLatitude = 9.03,
            pickupLongitude = 38.74,
            destinationLatitude = 9.01,
            destinationLongitude = 38.78,
            requiredVehicleType = "STANDARD",
            requestedPickupAt = pickupAt,
            estimatedTripDurationSeconds = 1200,
        });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var activate = await client.PostAsync($"/api/v1/ride-requests/{id}/activate-dispatch", null);
        activate.EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> OverrideAsync(
        HttpClient client, Guid runId, Guid driverId, string reason) =>
        client.PostAsJsonAsync($"/api/v1/allocation-runs/{runId}/offers/override",
            new { driverId, reason });

    private static async Task<Guid> CreateRideAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = "Override Passenger",
            contactPhone = "+251911000077",
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

    private static async Task<Guid> CreateDriverAsync(
        HttpClient client, string name, double latitude, string vehicleType = "STANDARD")
    {
        using var create = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var response in new[]
        {
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/approval", new { status = "APPROVED" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/operational-status", new { status = "AVAILABLE" }),
            await client.PostAsJsonAsync($"/api/v1/drivers/{id}/vehicle", new { type = vehicleType, plateNumber = $"OVR-{id:N}"[..16] }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/location", new { latitude, longitude = 38.74 }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/financial-standing", new { commissionBalance = 1m }),
        }) using (response) response.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> CreateRunAsync(
        HttpClient client, Guid rideId, Guid expectedRecommendation)
    {
        using var response = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null);
        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedRecommendation, run.GetProperty("recommendedDriverId").GetGuid());
        return run.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> GetEvidenceAsync(HttpClient client, Guid runId)
    {
        using var response = await client.GetAsync($"/api/v1/allocation-runs/{runId}/candidate-evaluations");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
    }

    private static void AssertEligible(JsonElement[] evidence, Guid driverId, int rank)
    {
        var candidate = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverId);
        Assert.True(candidate.GetProperty("isEligible").GetBoolean());
        Assert.Equal(rank, candidate.GetProperty("rank").GetInt32());
    }

    private static async Task AssertNoOfferAsync(TestEnvironment environment, Guid runId)
    {
        await using var scope = environment.Application.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Offers
            .AnyAsync(offer => offer.AllocationRunId == runId));
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database,
            WebApplicationFactory<Program> application, HttpClient client, MutableClock clock)
        { Database = database; Application = application; Client = client; Clock = clock; }
        private PostgreSqlContainer Database { get; }
        public WebApplicationFactory<Program> Application { get; }
        public HttpClient Client { get; }
        public MutableClock Clock { get; }
        public static async Task<TestEnvironment> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch").WithUsername("ride_dispatch")
                .WithPassword("ride_dispatch").Build();
            await database.StartAsync();
            var clock = new MutableClock(Now);
            var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                    configure?.Invoke(services);
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

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Set(DateTimeOffset value) => now = value;
    }

    private sealed class RacingOfferStore(OfferStore inner, Barrier barrier) : IOfferStore
    {
        public Task AddAsync(Offer offer, CancellationToken ct) => inner.AddAsync(offer, ct);
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => inner.FindByIdAsync(id, ct);
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => inner.FindByIdReadOnlyAsync(id, ct);
        public Task<Offer?> FindActionableByRideRequestIdAsync(Guid id, CancellationToken ct) => inner.FindActionableByRideRequestIdAsync(id, ct);
        public async Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct)
        {
            var result = await inner.FindByAllocationRunIdAsync(id, ct);
            barrier.SignalAndWait(ct);
            return result;
        }
        public Task SaveChangesAsync(CancellationToken ct) => inner.SaveChangesAsync(ct);
    }
}
