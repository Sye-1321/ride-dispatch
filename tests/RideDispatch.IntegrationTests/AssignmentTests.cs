using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideDispatch.Application.Dispatch.Assignments;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class AssignmentTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Stale_accepted_offers_are_protected_by_database_overlap_constraint()
    {
        var barrier = new Barrier(2);
        await using var environment = await TestEnvironment.CreateAsync(services =>
        {
            services.RemoveAll<IAssignmentStore>();
            services.AddScoped<IAssignmentStore>(provider => new RacingAssignmentStore(
                new AssignmentStore(provider.GetRequiredService<DispatchDbContext>()), barrier));
        });
        var client = environment.Client;
        var driverId = await CreateEligibleDriverAsync(client, "Race Driver");
        var rideA = await CreateRideAsync(client, "Race A", "IMMEDIATE", null);
        var rideB = await CreateRideAsync(client, "Race B", "IMMEDIATE", null);
        var runA = await CreateRunAsync(client, rideA, driverId);
        var runB = await CreateRunAsync(client, rideB, driverId);
        var offerA = await CreateAndAcceptOfferAsync(client, runA);
        var offerB = await CreateAndAcceptOfferAsync(client, runB);

        var responses = await Task.WhenAll(
            client.PostAsync($"/api/v1/offers/{offerA}/assignments", null),
            client.PostAsync($"/api/v1/offers/{offerB}/assignments", null));
        using var first = responses[0];
        using var second = responses[1];

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await AssertDatabaseConstraintAsync(environment.Application.Services);
    }

    [Fact]
    public async Task Exact_assignment_boundary_is_allowed_by_real_database()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;
        var driverId = await CreateEligibleDriverAsync(client, "Boundary Driver");
        var rideA = await CreateRideAsync(client, "Boundary A", "SCHEDULED", CurrentTime.AddHours(1));
        var rideB = await CreateRideAsync(client, "Boundary B", "SCHEDULED", CurrentTime.AddHours(1).AddMinutes(20));
        var runA = await CreateRunAsync(client, rideA, driverId);
        var runB = await CreateRunAsync(client, rideB, driverId);
        var offerA = await CreateAndAcceptOfferAsync(client, runA);
        var offerB = await CreateAndAcceptOfferAsync(client, runB);

        using var assignmentA = await client.PostAsync($"/api/v1/offers/{offerA}/assignments", null);
        using var assignmentB = await client.PostAsync($"/api/v1/offers/{offerB}/assignments", null);

        Assert.Equal(HttpStatusCode.Created, assignmentA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, assignmentB.StatusCode);
        var first = await assignmentA.Content.ReadFromJsonAsync<JsonElement>();
        var second = await assignmentB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("endsAt").GetDateTimeOffset(), second.GetProperty("startsAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Assignment_conflict_is_persisted_as_candidate_evidence_only_for_overlapping_window()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;
        var driverId = await CreateEligibleDriverAsync(client, "Evidence Driver");
        var committedRide = await CreateRideAsync(client, "Committed", "IMMEDIATE", null);
        var committedRun = await CreateRunAsync(client, committedRide, driverId);
        var committedOffer = await CreateAndAcceptOfferAsync(client, committedRun);
        using (var assignment = await client.PostAsync($"/api/v1/offers/{committedOffer}/assignments", null))
        {
            Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        }

        var overlappingRide = await CreateRideAsync(client, "Overlapping", "IMMEDIATE", null);
        var overlappingRun = await CreateRunAsync(client, overlappingRide, null);
        using var overlappingEvidenceResponse = await client.GetAsync(
            $"/api/v1/allocation-runs/{overlappingRun}/candidate-evaluations");
        overlappingEvidenceResponse.EnsureSuccessStatusCode();
        var overlappingEvidence = Assert.Single((await overlappingEvidenceResponse.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(driverId, overlappingEvidence.GetProperty("driverId").GetGuid());
        Assert.False(overlappingEvidence.GetProperty("isEligible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, overlappingEvidence.GetProperty("rank").ValueKind);
        Assert.Contains("ASSIGNMENT_CONFLICT", overlappingEvidence.GetProperty("rejectionReasons")
            .EnumerateArray().Select(reason => reason.GetString()));
        using (var overlappingRunResponse = await client.GetAsync($"/api/v1/allocation-runs/{overlappingRun}"))
        {
            var run = await overlappingRunResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, run.GetProperty("recommendedDriverId").ValueKind);
        }

        var nonOverlappingRide = await CreateRideAsync(client, "Non-overlapping", "SCHEDULED", CurrentTime.AddMinutes(20));
        var nonOverlappingRun = await CreateRunAsync(client, nonOverlappingRide, driverId);
        using var nonOverlappingEvidenceResponse = await client.GetAsync(
            $"/api/v1/allocation-runs/{nonOverlappingRun}/candidate-evaluations");
        nonOverlappingEvidenceResponse.EnsureSuccessStatusCode();
        var nonOverlappingEvidence = Assert.Single((await nonOverlappingEvidenceResponse.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.True(nonOverlappingEvidence.GetProperty("isEligible").GetBoolean());
        Assert.DoesNotContain("ASSIGNMENT_CONFLICT", nonOverlappingEvidence.GetProperty("rejectionReasons")
            .EnumerateArray().Select(reason => reason.GetString()));
    }

    private static async Task AssertDatabaseConstraintAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conname = 'ex_assignments_driver_time'
            """;
        await dbContext.Database.OpenConnectionAsync();
        var definition = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("EXCLUDE USING gist", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tstzrange(starts_at, ends_at, '[)'", definition, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> CreateRideAsync(HttpClient client, string name, string timing, DateTimeOffset? pickup)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ride-requests", new
        {
            contactName = name,
            contactPhone = "+251911000099",
            bookingSource = "CALL_CENTER",
            timing,
            pickupLatitude = 9.03,
            pickupLongitude = 38.74,
            destinationLatitude = 9.01,
            destinationLongitude = 38.78,
            requiredVehicleType = "STANDARD",
            requestedPickupAt = pickup,
            estimatedTripDurationSeconds = 1200,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateEligibleDriverAsync(HttpClient client, string name)
    {
        using var create = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var response in new[]
        {
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/approval", new { status = "APPROVED" }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/operational-status", new { status = "AVAILABLE" }),
            await client.PostAsJsonAsync($"/api/v1/drivers/{id}/vehicle", new { type = "STANDARD", plateNumber = $"ASN-{id:N}"[..16] }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/location", new { latitude = 9.031, longitude = 38.74 }),
            await client.PutAsJsonAsync($"/api/v1/drivers/{id}/financial-standing", new { commissionBalance = 1m }),
        })
        {
            using (response) response.EnsureSuccessStatusCode();
        }
        return id;
    }

    private static async Task<Guid> CreateRunAsync(HttpClient client, Guid rideId, Guid? expectedDriverId)
    {
        using var response = await client.PostAsync($"/api/v1/ride-requests/{rideId}/allocation-runs", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (expectedDriverId is null)
            Assert.Equal(JsonValueKind.Null, run.GetProperty("recommendedDriverId").ValueKind);
        else
            Assert.Equal(expectedDriverId, run.GetProperty("recommendedDriverId").GetGuid());
        return run.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAndAcceptOfferAsync(HttpClient client, Guid runId)
    {
        using var create = await client.PostAsync($"/api/v1/allocation-runs/{runId}/offers", null);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var offerId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var accept = await client.PostAsync($"/api/v1/offers/{offerId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        return offerId;
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database, WebApplicationFactory<Program> application, HttpClient client)
        { Database = database; Application = application; Client = client; }
        private PostgreSqlContainer Database { get; }
        public WebApplicationFactory<Program> Application { get; }
        public HttpClient Client { get; }

        public static async Task<TestEnvironment> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
            await database.StartAsync();
            var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(CurrentTime));
                    configure?.Invoke(services);
                });
            });
            await using (var scope = application.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
            return new(database, application, application.CreateClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Application.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => currentTime; }

    private sealed class RacingAssignmentStore(AssignmentStore inner, Barrier barrier) : IAssignmentStore
    {
        public Task AddAsync(Assignment assignment, CancellationToken cancellationToken) => inner.AddAsync(assignment, cancellationToken);
        public Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) => inner.FindByIdReadOnlyAsync(id, cancellationToken);
        public Task<Assignment?> FindByOfferIdAsync(Guid offerId, CancellationToken cancellationToken) => inner.FindByOfferIdAsync(offerId, cancellationToken);
        public Task<Assignment?> FindByRideRequestIdAsync(Guid rideRequestId, CancellationToken cancellationToken) => inner.FindByRideRequestIdAsync(rideRequestId, cancellationToken);
        public async Task<bool> HasDriverOverlapAsync(Guid driverId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            var result = await inner.HasDriverOverlapAsync(driverId, startsAt, endsAt, cancellationToken);
            barrier.SignalAndWait(cancellationToken);
            return result;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
    }
}
