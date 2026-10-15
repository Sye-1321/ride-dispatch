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

public sealed class ReplacementDispatchTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Declined_offer_creates_next_offer_and_retry_returns_same_resources()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "Replacement A", 9.031);
        environment.Clock.Advance(TimeSpan.FromSeconds(1));
        var driverB = await CreateDriverAsync(environment.Client, "Replacement B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId, "LongestIdle");
        var sourceOffer = await CreateOfferAsync(environment.Client, firstRun);
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{sourceOffer.Id}/decline", null))
            decline.EnsureSuccessStatusCode();

        var first = await ReplaceAsync(environment.Client, sourceOffer.Id);
        Assert.Equal(rideId, first.Run.GetProperty("rideRequestId").GetGuid());
        Assert.Equal("LONGEST_IDLE", first.Run.GetProperty("rankingPolicy").GetString());
        Assert.Equal(sourceOffer.Id, first.Run.GetProperty("sourceOfferId").GetGuid());
        Assert.Equal(driverB, first.Run.GetProperty("recommendedDriverId").GetGuid());
        Assert.Equal(first.Run.GetProperty("id").GetGuid(), first.Offer!.Value.GetProperty("allocationRunId").GetGuid());
        Assert.Equal(driverB, first.Offer.Value.GetProperty("driverId").GetGuid());
        Assert.Equal("PENDING", first.Offer.Value.GetProperty("status").GetString());

        var evidence = await GetEvidenceAsync(environment.Client, first.Run.GetProperty("id").GetGuid());
        AssertRejected(evidence, driverA, "PREVIOUS_OFFER_DECLINED");
        AssertEligible(evidence, driverB);

        var retry = await ReplaceAsync(environment.Client, sourceOffer.Id);
        Assert.Equal(first.Run.GetProperty("id").GetGuid(), retry.Run.GetProperty("id").GetGuid());
        Assert.Equal(first.Offer.Value.GetProperty("id").GetGuid(), retry.Offer!.Value.GetProperty("id").GetGuid());
        await using var scope = environment.Application.Services.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().AllocationRuns
            .CountAsync(run => run.SourceOfferId == sourceOffer.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Pending_offer_at_exact_expiry_is_expired_and_replaced()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        var driverA = await CreateDriverAsync(environment.Client, "Timeout A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Timeout B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId);
        var sourceOffer = await CreateOfferAsync(environment.Client, firstRun);
        environment.Clock.Set(sourceOffer.ExpiresAt);

        var replacement = await ReplaceAsync(environment.Client, sourceOffer.Id);
        Assert.Equal(driverB, replacement.Run.GetProperty("recommendedDriverId").GetGuid());
        Assert.Equal(driverB, replacement.Offer!.Value.GetProperty("driverId").GetGuid());
        using var persistedResponse = await environment.Client.GetAsync($"/api/v1/offers/{sourceOffer.Id}");
        var persisted = await persistedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EXPIRED", persisted.GetProperty("status").GetString());
        Assert.Equal(sourceOffer.ExpiresAt, persisted.GetProperty("resolvedAt").GetDateTimeOffset());
        AssertRejected(await GetEvidenceAsync(environment.Client, replacement.Run.GetProperty("id").GetGuid()),
            driverA, "PREVIOUS_OFFER_EXPIRED");
    }

    [Fact]
    public async Task No_remaining_driver_persists_run_with_null_offer_and_reuses_it()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, "Only Driver", 9.031);
        var firstRun = await CreateRunAsync(environment.Client, rideId);
        var sourceOffer = await CreateOfferAsync(environment.Client, firstRun);
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{sourceOffer.Id}/decline", null))
            decline.EnsureSuccessStatusCode();

        var first = await ReplaceAsync(environment.Client, sourceOffer.Id);
        Assert.Equal(JsonValueKind.Null, first.Run.GetProperty("recommendedDriverId").ValueKind);
        Assert.Null(first.Offer);
        AssertRejected(await GetEvidenceAsync(environment.Client, first.Run.GetProperty("id").GetGuid()),
            sourceOffer.DriverId, "PREVIOUS_OFFER_DECLINED");
        var retry = await ReplaceAsync(environment.Client, sourceOffer.Id);
        Assert.Equal(first.Run.GetProperty("id").GetGuid(), retry.Run.GetProperty("id").GetGuid());
        Assert.Null(retry.Offer);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("accepted")]
    [InlineData("withdrawn")]
    public async Task Invalid_source_states_return_conflict_without_replacement(string state)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, $"Invalid {state}", 9.031);
        var runId = await CreateRunAsync(environment.Client, rideId);
        var offer = await CreateOfferAsync(environment.Client, runId);
        if (state != "pending")
        {
            var action = state == "accepted" ? "accept" : "withdraw";
            using var transition = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/{action}", null);
            transition.EnsureSuccessStatusCode();
        }

        using var response = await environment.Client.PostAsync(
            $"/api/v1/offers/{offer.Id}/replacement-dispatch", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var scope = environment.Application.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().AllocationRuns
            .AnyAsync(candidate => candidate.SourceOfferId == offer.Id));
    }

    [Fact]
    public async Task Concurrent_commands_resolve_to_one_replacement_run_without_server_error()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, "Concurrent A", 9.031);
        await CreateDriverAsync(environment.Client, "Concurrent B", 9.04);
        var runId = await CreateRunAsync(environment.Client, rideId);
        var offer = await CreateOfferAsync(environment.Client, runId);
        using (var decline = await environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/decline", null))
            decline.EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(
            environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/replacement-dispatch", null),
            environment.Client.PostAsync($"/api/v1/offers/{offer.Id}/replacement-dispatch", null));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
            Assert.Equal(bodies[0].GetProperty("allocationRun").GetProperty("id").GetGuid(),
                bodies[1].GetProperty("allocationRun").GetProperty("id").GetGuid());
            Assert.Equal(bodies[0].GetProperty("offer").GetProperty("id").GetGuid(),
                bodies[1].GetProperty("offer").GetProperty("id").GetGuid());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
        await using var scope = environment.Application.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().AllocationRuns
            .CountAsync(candidate => candidate.SourceOfferId == offer.Id));
    }

    [Fact]
    public async Task Concurrent_replacement_dispatch_on_pending_offer_expires_once_and_resolves_to_one_run()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, "Pending Timeout A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Pending Timeout B", 9.04);
        var runId = await CreateRunAsync(environment.Client, rideId);
        var sourceOffer = await CreateOfferAsync(environment.Client, runId);

        environment.Clock.Set(sourceOffer.ExpiresAt);

        var responses = await Task.WhenAll(
            environment.Client.PostAsync($"/api/v1/offers/{sourceOffer.Id}/replacement-dispatch", null),
            environment.Client.PostAsync($"/api/v1/offers/{sourceOffer.Id}/replacement-dispatch", null));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
            Assert.Equal(bodies[0].GetProperty("allocationRun").GetProperty("id").GetGuid(),
                bodies[1].GetProperty("allocationRun").GetProperty("id").GetGuid());
            Assert.Equal(bodies[0].GetProperty("offer").GetProperty("id").GetGuid(),
                bodies[1].GetProperty("offer").GetProperty("id").GetGuid());
            Assert.Equal(driverB, bodies[0].GetProperty("offer").GetProperty("driverId").GetGuid());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using var scope = environment.Application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        Assert.Equal(1, await db.AllocationRuns.CountAsync(candidate => candidate.SourceOfferId == sourceOffer.Id));

        var persisted = await db.Offers.SingleAsync(o => o.Id == sourceOffer.Id);
        Assert.Equal(OfferStatus.Expired, persisted.Status);
    }

    [Fact]
    public async Task Concurrent_accept_wins_race_against_replacement_dispatch_on_pending_offer()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, "Accept Win Driver A", 9.031);
        await CreateDriverAsync(environment.Client, "Accept Win Driver B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId);
        var sourceOffer = await CreateOfferAsync(environment.Client, firstRun);

        await using var acceptScope = environment.Application.Services.CreateAsyncScope();
        await using var replaceScope = environment.Application.Services.CreateAsyncScope();

        var acceptStore = acceptScope.ServiceProvider.GetRequiredService<IOfferStore>();
        var replaceStore = replaceScope.ServiceProvider.GetRequiredService<IOfferStore>();

        var offerToAccept = (await acceptStore.FindByIdAsync(sourceOffer.Id, default))!;
        var offerToReplace = (await replaceStore.FindByIdAsync(sourceOffer.Id, default))!;

        offerToAccept.Accept(offerToAccept.CreatedAt.AddSeconds(1));
        await acceptStore.SaveChangesAsync(default);

        environment.Clock.Set(sourceOffer.ExpiresAt);
        var replacementUseCase = replaceScope.ServiceProvider.GetRequiredService<CreateReplacementDispatch>();
        var result = await replacementUseCase.ExecuteAsync(sourceOffer.Id, default);

        Assert.Equal(CreateReplacementDispatchOutcome.SourceOfferNotFailed, result.Outcome);

        await using var verifyScope = environment.Application.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        Assert.False(await db.AllocationRuns.AnyAsync(r => r.SourceOfferId == sourceOffer.Id));

        var persisted = await db.Offers.SingleAsync(o => o.Id == sourceOffer.Id);
        Assert.Equal(OfferStatus.Accepted, persisted.Status);
    }

    [Fact]
    public async Task Replacement_dispatch_concurrency_race_on_expiry_reloads_winner_and_reuses_run()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var rideId = await CreateRideAsync(environment.Client);
        await CreateDriverAsync(environment.Client, "Expiry Race A", 9.031);
        var driverB = await CreateDriverAsync(environment.Client, "Expiry Race B", 9.04);
        var firstRun = await CreateRunAsync(environment.Client, rideId);
        var sourceOffer = await CreateOfferAsync(environment.Client, firstRun);

        environment.Clock.Set(sourceOffer.ExpiresAt);

        await using var scopeA = environment.Application.Services.CreateAsyncScope();
        await using var scopeB = environment.Application.Services.CreateAsyncScope();

        var storeA = scopeA.ServiceProvider.GetRequiredService<IOfferStore>();
        var storeB = scopeB.ServiceProvider.GetRequiredService<IOfferStore>();
        var offerA = (await storeA.FindByIdAsync(sourceOffer.Id, default))!;
        var offerB = (await storeB.FindByIdAsync(sourceOffer.Id, default))!;

        var useCaseA = scopeA.ServiceProvider.GetRequiredService<CreateReplacementDispatch>();
        var resultA = await useCaseA.ExecuteAsync(sourceOffer.Id, default);
        Assert.Equal(CreateReplacementDispatchOutcome.Success, resultA.Outcome);

        var useCaseB = scopeB.ServiceProvider.GetRequiredService<CreateReplacementDispatch>();
        var resultB = await useCaseB.ExecuteAsync(sourceOffer.Id, default);
        Assert.Equal(CreateReplacementDispatchOutcome.Success, resultB.Outcome);

        Assert.Equal(resultA.AllocationRun!.Id, resultB.AllocationRun!.Id);
        Assert.Equal(resultA.Offer!.Id, resultB.Offer!.Id);

        await using var verifyScope = environment.Application.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        Assert.Equal(1, await db.AllocationRuns.CountAsync(run => run.SourceOfferId == sourceOffer.Id));
    }

    private static async Task<(JsonElement Run, JsonElement? Offer)> ReplaceAsync(HttpClient client, Guid offerId)
    {
        using var response = await client.PostAsync($"/api/v1/offers/{offerId}/replacement-dispatch", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var offer = body.GetProperty("offer");
        return (body.GetProperty("allocationRun").Clone(), offer.ValueKind == JsonValueKind.Null ? null : offer.Clone());
    }

    private static async Task<Guid> CreateRideAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = "Replacement Passenger",
            contactPhone = "+251911000099",
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

    private static async Task<Guid> CreateDriverAsync(HttpClient client, string name, double latitude)
    {
        using var create = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var response in new[]
        {
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/approval", new { status = "APPROVED" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/operational-status", new { status = "AVAILABLE" }),
            await client.PostAsJsonAsync($"/api/v1/drivers/{id}/vehicle", new { type = "STANDARD", plateNumber = $"REP-{id:N}"[..16] }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/location", new { latitude, longitude = 38.74 }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/financial-standing", new { commissionBalance = 1m }),
        }) using (response) response.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> CreateRunAsync(HttpClient client, Guid rideId, string policy = "NEAREST")
    {
        using var response = await client.PostAsync(
            $"/api/v1/ride-requests/{rideId}/allocation-runs?rankingPolicy={policy}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<OfferData> CreateOfferAsync(HttpClient client, Guid runId)
    {
        using var response = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        response.EnsureSuccessStatusCode();
        var offer = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new(offer.GetProperty("id").GetGuid(), offer.GetProperty("driverId").GetGuid(),
            offer.GetProperty("expiresAt").GetDateTimeOffset());
    }

    private static async Task<JsonElement[]> GetEvidenceAsync(HttpClient client, Guid runId) =>
        (await (await client.GetAsync($"/api/v1/allocation-runs/{runId}/candidate-evaluations"))
            .Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();

    private static void AssertRejected(JsonElement[] evidence, Guid driverId, string reason)
    {
        var item = Assert.Single(evidence, value => value.GetProperty("driverId").GetGuid() == driverId);
        Assert.False(item.GetProperty("isEligible").GetBoolean());
        Assert.Contains(reason, item.GetProperty("rejectionReasons").EnumerateArray().Select(value => value.GetString()));
    }

    private static void AssertEligible(JsonElement[] evidence, Guid driverId)
    {
        var item = Assert.Single(evidence, value => value.GetProperty("driverId").GetGuid() == driverId);
        Assert.True(item.GetProperty("isEligible").GetBoolean());
        Assert.Equal(1, item.GetProperty("rank").GetInt32());
    }

    private sealed record OfferData(Guid Id, Guid DriverId, DateTimeOffset ExpiresAt);

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database, WebApplicationFactory<Program> application,
            HttpClient client, MutableClock clock)
        { Database = database; Application = application; Client = client; Clock = clock; }
        private PostgreSqlContainer Database { get; }
        public WebApplicationFactory<Program> Application { get; }
        public HttpClient Client { get; }
        public MutableClock Clock { get; }
        public static async Task<TestEnvironment> CreateAsync()
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
            await database.StartAsync();
            var clock = new MutableClock(InitialTime);
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

    public sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Set(DateTimeOffset value) => now = value;
        public void Advance(TimeSpan value) => now += value;
    }
}
