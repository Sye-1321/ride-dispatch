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

public sealed class OfferTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Offers_follow_creation_terminal_and_exact_expiry_contracts()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
        await database.StartAsync();
        var clock = new MutableTimeProvider(InitialTime);
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.UseSetting("Dispatch:Scheduled:ActivationLeadTimeSeconds", "3600");
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
        var rideId = await CreateRideAsync(client);
        var driverId = await CreateEligibleDriverAsync(client);
        var firstRunId = await CreateRunAsync(client, rideId);

        using var createResponse = await client.PostAsync($"/api/v1/allocation-runs/{firstRunId}/offers", null);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var root = created.RootElement;
        var offerId = root.GetProperty("id").GetGuid();
        Assert.Equal(firstRunId, root.GetProperty("allocationRunId").GetGuid());
        Assert.Equal(driverId, root.GetProperty("driverId").GetGuid());
        Assert.Equal("PENDING", root.GetProperty("status").GetString());
        Assert.Equal(
            root.GetProperty("createdAt").GetDateTimeOffset().AddSeconds(20),
            root.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("resolvedAt").ValueKind);

        using var getResponse = await client.GetAsync(createResponse.Headers.Location);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(root.ToString(), (await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync())).RootElement.ToString());

        using var duplicateResponse = await client.PostAsync($"/api/v1/allocation-runs/{firstRunId}/offers", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var pendingAssignment = await client.PostAsync($"/api/v1/offers/{offerId}/assignments", null);
        Assert.Equal(HttpStatusCode.Conflict, pendingAssignment.StatusCode);

        using var acceptResponse = await client.PostAsync($"/api/v1/offers/{offerId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        using var accepted = JsonDocument.Parse(await acceptResponse.Content.ReadAsStringAsync());
        Assert.Equal("ACCEPTED", accepted.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, accepted.RootElement.GetProperty("resolvedAt").ValueKind);

        using var assignmentResponse = await client.PostAsync($"/api/v1/offers/{offerId}/assignments", null);
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        Assert.NotNull(assignmentResponse.Headers.Location);
        using var assignment = JsonDocument.Parse(await assignmentResponse.Content.ReadAsStringAsync());
        Assert.Equal(offerId, assignment.RootElement.GetProperty("offerId").GetGuid());
        Assert.Equal(rideId, assignment.RootElement.GetProperty("rideRequestId").GetGuid());
        Assert.Equal(driverId, assignment.RootElement.GetProperty("driverId").GetGuid());
        Assert.Equal(InitialTime, assignment.RootElement.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(InitialTime.AddMinutes(20), assignment.RootElement.GetProperty("endsAt").GetDateTimeOffset());
        using var getAssignment = await client.GetAsync(assignmentResponse.Headers.Location);
        getAssignment.EnsureSuccessStatusCode();
        Assert.Equal(assignment.RootElement.ToString(),
            (await JsonDocument.ParseAsync(await getAssignment.Content.ReadAsStreamAsync())).RootElement.ToString());
        using var duplicateAssignment = await client.PostAsync($"/api/v1/offers/{offerId}/assignments", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateAssignment.StatusCode);
        foreach (var action in new[] { "decline", "withdraw" })
        {
            using var conflict = await client.PostAsync($"/api/v1/offers/{offerId}/{action}", null);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }

        var secondRideId = await CreateRideAsync(client, InitialTime.AddMinutes(20));
        var secondRunId = await CreateRunAsync(client, secondRideId);
        using var secondCreate = await client.PostAsync($"/api/v1/allocation-runs/{secondRunId}/offers", null);
        secondCreate.EnsureSuccessStatusCode();
        using var second = JsonDocument.Parse(await secondCreate.Content.ReadAsStringAsync());
        var secondOfferId = second.RootElement.GetProperty("id").GetGuid();
        clock.Set(second.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
        using var expiredAccept = await client.PostAsync($"/api/v1/offers/{secondOfferId}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, expiredAccept.StatusCode);
        using var persistedExpired = await client.GetAsync($"/api/v1/offers/{secondOfferId}");
        using var expired = JsonDocument.Parse(await persistedExpired.Content.ReadAsStringAsync());
        Assert.Equal("EXPIRED", expired.RootElement.GetProperty("status").GetString());
        Assert.Equal(clock.GetUtcNow(), expired.RootElement.GetProperty("resolvedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Status_concurrency_token_prevents_terminal_last_write_wins()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;
        var rideId = await CreateRideAsync(client);
        var driverId = await CreateEligibleDriverAsync(client);
        var runId = await CreateRunAsync(client, rideId);
        using var createResponse = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        createResponse.EnsureSuccessStatusCode();
        var offerId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var firstScope = environment.Application.Services.CreateAsyncScope();
        await using var secondScope = environment.Application.Services.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IOfferStore>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IOfferStore>();
        var first = (await firstStore.FindByIdAsync(offerId, default))!;
        var second = (await secondStore.FindByIdAsync(offerId, default))!;

        first.Accept(InitialTime.AddSeconds(1));
        second.Decline(InitialTime.AddSeconds(2));

        await firstStore.SaveChangesAsync(default);
        await Assert.ThrowsAsync<OfferStateConcurrencyException>(() => secondStore.SaveChangesAsync(default));

        await using var verificationScope = environment.Application.Services.CreateAsyncScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<IOfferStore>()
            .FindByIdReadOnlyAsync(offerId, default);
        Assert.Equal(OfferStatus.Accepted, persisted!.Status);
        Assert.Equal(InitialTime.AddSeconds(1), persisted.ResolvedAt);
    }

    [Theory]
    [InlineData("accept", "decline")]
    [InlineData("accept", "accept")]
    [InlineData("withdraw", "accept")]
    public async Task Competing_transitions_cannot_all_report_success(string actionA, string actionB)
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;
        var rideId = await CreateRideAsync(client);
        var driverId = await CreateEligibleDriverAsync(client);
        var runId = await CreateRunAsync(client, rideId);
        using var createResponse = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        createResponse.EnsureSuccessStatusCode();
        var offerId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var requestA = client.PostAsync($"/api/v1/offers/{offerId}/{actionA}", null);
        var requestB = client.PostAsync($"/api/v1/offers/{offerId}/{actionB}", null);
        var responses = await Task.WhenAll(requestA, requestB);

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);

        var okResponse = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
        var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);

        var okBody = await okResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conflictBody = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(okBody.GetProperty("status").GetString(), conflictBody.GetProperty("status").GetString());
        Assert.Equal(okBody.GetProperty("resolvedAt").GetDateTimeOffset(), conflictBody.GetProperty("resolvedAt").GetDateTimeOffset());

        using var getResponse = await client.GetAsync($"/api/v1/offers/{offerId}");
        var persisted = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(okBody.GetProperty("status").GetString(), persisted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Competing_transition_when_expired_returns_expired_conflict()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;
        var rideId = await CreateRideAsync(client);
        var driverId = await CreateEligibleDriverAsync(client);
        var runId = await CreateRunAsync(client, rideId);
        using var createResponse = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        createResponse.EnsureSuccessStatusCode();
        var offerId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        environment.Clock.Advance(TimeSpan.FromSeconds(20));

        var acceptTask = client.PostAsync($"/api/v1/offers/{offerId}/accept", null);
        var declineTask = client.PostAsync($"/api/v1/offers/{offerId}/decline", null);
        var responses = await Task.WhenAll(acceptTask, declineTask);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        using var getResponse = await client.GetAsync($"/api/v1/offers/{offerId}");
        var persisted = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EXPIRED", persisted.GetProperty("status").GetString());
    }

    private static async Task<Guid> CreateRideAsync(HttpClient client, DateTimeOffset? requestedPickupAt = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = "Offer Passenger",
            contactPhone = "+251911000002",
            bookingSource = "CALL_CENTER",
            timing = requestedPickupAt is null ? "IMMEDIATE" : "SCHEDULED",
            pickupLatitude = 9.03,
            pickupLongitude = 38.74,
            destinationLatitude = 9.01,
            destinationLongitude = 38.78,
            requiredVehicleType = "STANDARD",
            requestedPickupAt,
            estimatedTripDurationSeconds = 1200,
        });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (requestedPickupAt is not null)
        {
            using var activation = await client.PostAsync($"/api/v1/ride-requests/{id}/activate-dispatch", null);
            activation.EnsureSuccessStatusCode();
        }
        return id;
    }

    private static async Task<Guid> CreateEligibleDriverAsync(HttpClient client)
    {
        using var create = await client.PostAsJsonAsync("/api/v1/drivers", new { name = "Offer Driver" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var response in new[]
        {
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/approval", new { status = "APPROVED" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/operational-status", new { status = "AVAILABLE" }),
            await client.PostAsJsonAsync($"/api/v1/drivers/{id}/vehicle", new { type = "STANDARD", plateNumber = "OFFER-001" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/location", new { latitude = 9.031, longitude = 38.74 }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/financial-standing", new { commissionBalance = 1m }),
        })
        {
            using (response) response.EnsureSuccessStatusCode();
        }
        return id;
    }

    private static async Task<Guid> CreateRunAsync(HttpClient client, Guid rideId)
    {
        using var response = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private sealed class MutableTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
        public void Set(DateTimeOffset value) => currentTime = value;
        public void Advance(TimeSpan value) => currentTime += value;
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database, WebApplicationFactory<Program> application,
            HttpClient client, MutableTimeProvider clock)
        { Database = database; Application = application; Client = client; Clock = clock; }
        private PostgreSqlContainer Database { get; }
        public WebApplicationFactory<Program> Application { get; }
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
                builder.UseSetting("Dispatch:Scheduled:ActivationLeadTimeSeconds", "3600");
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
}
