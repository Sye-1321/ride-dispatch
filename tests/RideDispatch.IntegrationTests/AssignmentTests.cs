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

    [Fact]
    public async Task Cancellation_releases_ride_and_driver_for_new_assignment()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var driverId = await CreateEligibleDriverAsync(environment.Client, "Cancellation Driver");
        var rideId = await CreateRideAsync(environment.Client, "Cancellation Ride", "IMMEDIATE", null);
        var firstRun = await CreateRunAsync(environment.Client, rideId, driverId);
        var firstOffer = await CreateAndAcceptOfferAsync(environment.Client, firstRun);
        var firstAssignment = await CreateAssignmentAsync(environment.Client, firstOffer);
        Assert.Equal("ACTIVE", firstAssignment.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, firstAssignment.GetProperty("resolvedAt").ValueKind);

        using var cancel = await environment.Client.PostAsync(
            $"/api/v1/assignments/{firstAssignment.GetProperty("id").GetGuid()}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CANCELLED", cancelled.GetProperty("status").GetString());
        Assert.Equal(CurrentTime, cancelled.GetProperty("resolvedAt").GetDateTimeOffset());
        Assert.Equal(firstAssignment.GetProperty("startsAt").GetDateTimeOffset(), cancelled.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(firstAssignment.GetProperty("endsAt").GetDateTimeOffset(), cancelled.GetProperty("endsAt").GetDateTimeOffset());

        var secondRun = await CreateRunAsync(environment.Client, rideId, driverId);
        var secondOffer = await CreateAndAcceptOfferAsync(environment.Client, secondRun);
        var secondAssignment = await CreateAssignmentAsync(environment.Client, secondOffer);
        Assert.NotEqual(firstAssignment.GetProperty("id").GetGuid(), secondAssignment.GetProperty("id").GetGuid());
        using var oldAssignment = await environment.Client.GetAsync(
            $"/api/v1/assignments/{firstAssignment.GetProperty("id").GetGuid()}");
        oldAssignment.EnsureSuccessStatusCode();
        Assert.Equal("CANCELLED", (await oldAssignment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Completion_releases_driver_but_keeps_ride_reserved()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var driverA = await CreateEligibleDriverAsync(environment.Client, "Completion A");
        var originalRide = await CreateRideAsync(environment.Client, "Completed Ride", "IMMEDIATE", null);
        var originalRun = await CreateRunAsync(environment.Client, originalRide, driverA);
        var originalOffer = await CreateAndAcceptOfferAsync(environment.Client, originalRun);
        var originalAssignment = await CreateAssignmentAsync(environment.Client, originalOffer);
        environment.Clock.Advance(TimeSpan.FromMinutes(1));
        using (var complete = await environment.Client.PostAsync(
            $"/api/v1/assignments/{originalAssignment.GetProperty("id").GetGuid()}/complete", null))
        {
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            Assert.Equal("COMPLETED", (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        }

        await RefreshLocationAsync(environment.Client, driverA);
        var otherRide = await CreateRideAsync(environment.Client, "Other Ride", "IMMEDIATE", null);
        var otherRun = await CreateRunAsync(environment.Client, otherRide, driverA);
        var evidence = await GetEvidenceAsync(environment.Client, otherRun);
        var driverAEvidence = Assert.Single(evidence, item => item.GetProperty("driverId").GetGuid() == driverA);
        Assert.True(driverAEvidence.GetProperty("isEligible").GetBoolean());
        Assert.DoesNotContain("ASSIGNMENT_CONFLICT", driverAEvidence.GetProperty("rejectionReasons").EnumerateArray().Select(x => x.GetString()));
        var otherOffer = await CreateAndAcceptOfferAsync(environment.Client, otherRun);
        await CreateAssignmentAsync(environment.Client, otherOffer);

        var driverB = await CreateEligibleDriverAsync(environment.Client, "Completion B");
        var rerun = await CreateRunAsync(environment.Client, originalRide, driverB);
        var reoffer = await CreateAndAcceptOfferAsync(environment.Client, rerun);
        using var duplicate = await environment.Client.PostAsync($"/api/v1/offers/{reoffer}/assignments", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = environment.Application.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Assignments
            .CountAsync(x => x.RideRequestId == originalRide && x.Status != AssignmentStatus.Cancelled));
    }

    [Fact]
    public async Task Future_assignment_cannot_complete_early_but_can_complete_at_start()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var driver = await CreateEligibleDriverAsync(environment.Client, "Scheduled Completion");
        var startsAt = CurrentTime.AddHours(1);
        var ride = await CreateRideAsync(environment.Client, "Scheduled Ride", "SCHEDULED", startsAt);
        var run = await CreateRunAsync(environment.Client, ride, driver);
        var offer = await CreateAndAcceptOfferAsync(environment.Client, run);
        var assignment = await CreateAssignmentAsync(environment.Client, offer);
        var assignmentId = assignment.GetProperty("id").GetGuid();

        using (var early = await environment.Client.PostAsync($"/api/v1/assignments/{assignmentId}/complete", null))
            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        using (var current = await environment.Client.GetAsync($"/api/v1/assignments/{assignmentId}"))
        {
            var body = await current.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("resolvedAt").ValueKind);
        }

        environment.Clock.Set(startsAt);
        using var exact = await environment.Client.PostAsync($"/api/v1/assignments/{assignmentId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        Assert.Equal("COMPLETED", (await exact.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Assignment_conflict_disappears_from_new_evidence_after_cancellation()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var driver = await CreateEligibleDriverAsync(environment.Client, "Lifecycle Evidence");
        var rideA = await CreateRideAsync(environment.Client, "Evidence A", "IMMEDIATE", null);
        var runA = await CreateRunAsync(environment.Client, rideA, driver);
        var offerA = await CreateAndAcceptOfferAsync(environment.Client, runA);
        var assignmentA = await CreateAssignmentAsync(environment.Client, offerA);
        var rideB = await CreateRideAsync(environment.Client, "Evidence B", "IMMEDIATE", null);
        var blockedRun = await CreateRunAsync(environment.Client, rideB, null);
        Assert.Contains("ASSIGNMENT_CONFLICT", (await GetEvidenceAsync(environment.Client, blockedRun))[0]
            .GetProperty("rejectionReasons").EnumerateArray().Select(x => x.GetString()));

        using (var cancel = await environment.Client.PostAsync(
            $"/api/v1/assignments/{assignmentA.GetProperty("id").GetGuid()}/cancel", null))
            cancel.EnsureSuccessStatusCode();
        var eligibleRun = await CreateRunAsync(environment.Client, rideB, driver);
        Assert.DoesNotContain("ASSIGNMENT_CONFLICT", (await GetEvidenceAsync(environment.Client, eligibleRun))[0]
            .GetProperty("rejectionReasons").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Status_concurrency_token_prevents_terminal_last_write_wins()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var driver = await CreateEligibleDriverAsync(environment.Client, "Concurrency Transition");
        var ride = await CreateRideAsync(environment.Client, "Concurrency Ride", "IMMEDIATE", null);
        var run = await CreateRunAsync(environment.Client, ride, driver);
        var offer = await CreateAndAcceptOfferAsync(environment.Client, run);
        var assignmentId = (await CreateAssignmentAsync(environment.Client, offer)).GetProperty("id").GetGuid();

        await using var firstScope = environment.Application.Services.CreateAsyncScope();
        await using var secondScope = environment.Application.Services.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        var first = (await firstStore.FindByIdAsync(assignmentId, default))!;
        var second = (await secondStore.FindByIdAsync(assignmentId, default))!;
        first.Complete(CurrentTime);
        second.Cancel(CurrentTime);
        await firstStore.SaveChangesAsync(default);
        await Assert.ThrowsAsync<AssignmentStateConcurrencyException>(() => secondStore.SaveChangesAsync(default));

        await using var verificationScope = environment.Application.Services.CreateAsyncScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<IAssignmentStore>()
            .FindByIdReadOnlyAsync(assignmentId, default);
        Assert.Equal(AssignmentStatus.Completed, persisted!.Status);
    }

    [Fact]
    public async Task Database_constraints_are_lifecycle_filtered_and_half_open()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await using var scope = environment.Application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'assignments' AND indexname = 'ux_assignments_ride_request_id';
            """;
        var index = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("UNIQUE", index, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status", index, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cancelled", index, StringComparison.Ordinal);
        command.CommandText = """
            SELECT pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conname = 'ex_assignments_driver_time';
            """;
        var exclusion = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("WHERE", exclusion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active", exclusion, StringComparison.Ordinal);
        Assert.Contains("tstzrange(starts_at, ends_at, '[)'", exclusion, StringComparison.OrdinalIgnoreCase);
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

    private static async Task<JsonElement> CreateAssignmentAsync(HttpClient client, Guid offerId)
    {
        using var response = await client.PostAsync($"/api/v1/offers/{offerId}/assignments", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement[]> GetEvidenceAsync(HttpClient client, Guid runId)
    {
        using var response = await client.GetAsync($"/api/v1/allocation-runs/{runId}/candidate-evaluations");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
    }

    private static async Task RefreshLocationAsync(HttpClient client, Guid driverId)
    {
        using var response = await client.PutAsJsonAsync($"/api/v1/drivers/{driverId}/location",
            new { latitude = 9.031, longitude = 38.74 });
        response.EnsureSuccessStatusCode();
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

        public static async Task<TestEnvironment> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch").WithUsername("ride_dispatch").WithPassword("ride_dispatch").Build();
            await database.StartAsync();
            var clock = new MutableTimeProvider(CurrentTime);
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

    public sealed class MutableTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
        public void Set(DateTimeOffset value) => currentTime = value;
        public void Advance(TimeSpan value) => currentTime += value;
    }

    private sealed class RacingAssignmentStore(AssignmentStore inner, Barrier barrier) : IAssignmentStore
    {
        public Task AddAsync(Assignment assignment, CancellationToken cancellationToken) => inner.AddAsync(assignment, cancellationToken);
        public Task<Assignment?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => inner.FindByIdAsync(id, cancellationToken);
        public Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) => inner.FindByIdReadOnlyAsync(id, cancellationToken);
        public Task<Assignment?> FindByOfferIdAsync(Guid offerId, CancellationToken cancellationToken) => inner.FindByOfferIdAsync(offerId, cancellationToken);
        public Task<Assignment?> FindNonCancelledByRideRequestIdAsync(Guid rideRequestId, CancellationToken cancellationToken) => inner.FindNonCancelledByRideRequestIdAsync(rideRequestId, cancellationToken);
        public async Task<bool> HasDriverOverlapAsync(Guid driverId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            var result = await inner.HasDriverOverlapAsync(driverId, startsAt, endsAt, cancellationToken);
            barrier.SignalAndWait(cancellationToken);
            return result;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
    }
}
