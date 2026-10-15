using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class DriverLifecycleTests
{
    [Fact]
    public async Task Driver_can_be_created_approved_and_made_available()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers",
            new { name = "  Abebe Kebede  " });
        var createResponseBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)createResponse.StatusCode}: {createResponseBody}");

        using var created = JsonDocument.Parse(createResponseBody);
        var driverId = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Abebe Kebede", created.RootElement.GetProperty("name").GetString());
        Assert.Equal("PENDING", created.RootElement.GetProperty("approvalStatus").GetString());
        Assert.Equal("OFFLINE", created.RootElement.GetProperty("operationalStatus").GetString());
        Assert.Equal(JsonValueKind.Null, created.RootElement.GetProperty("availableSince").ValueKind);

        using var numericApprovalResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/approval",
            new { status = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, numericApprovalResponse.StatusCode);

        using var unavailableResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/operational-status",
            new { status = "AVAILABLE" });
        Assert.Equal(HttpStatusCode.Conflict, unavailableResponse.StatusCode);

        using var problem = JsonDocument.Parse(await unavailableResponse.Content.ReadAsStringAsync());
        Assert.Equal("DRIVER_NOT_APPROVED", problem.RootElement.GetProperty("code").GetString());

        using var approvalResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/approval",
            new { status = "APPROVED" });
        approvalResponse.EnsureSuccessStatusCode();

        using var availabilityResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/operational-status",
            new { status = "AVAILABLE" });
        availabilityResponse.EnsureSuccessStatusCode();

        using var getResponse = await client.GetAsync($"/api/v1/drivers/{driverId}");
        getResponse.EnsureSuccessStatusCode();

        using var current = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal("APPROVED", current.RootElement.GetProperty("approvalStatus").GetString());
        Assert.Equal("AVAILABLE", current.RootElement.GetProperty("operationalStatus").GetString());
        Assert.Equal(JsonValueKind.String, current.RootElement.GetProperty("availableSince").ValueKind);
    }

    [Fact]
    public async Task Approval_and_operational_status_concurrency_tokens_prevent_lost_updates_at_store_boundary()
    {
        await using var environment = await TestEnvironment.CreateAsync();

        // 1. Race: Approval revocation commits first, competing operational status update fails with DriverStateConcurrencyException
        Guid driver1Id;
        await using (var setupScope = environment.Application.Services.CreateAsyncScope())
        {
            var store = setupScope.ServiceProvider.GetRequiredService<IDriverStore>();
            var driver = Driver.Create("Store Test Driver 1");
            driver.SetApprovalStatus(DriverApprovalStatus.Approved);
            await store.AddAsync(driver, CancellationToken.None);
            await store.SaveChangesAsync(CancellationToken.None);
            driver1Id = driver.Id;
        }

        await using (var scope1 = environment.Application.Services.CreateAsyncScope())
        await using (var scope2 = environment.Application.Services.CreateAsyncScope())
        {
            var store1 = scope1.ServiceProvider.GetRequiredService<IDriverStore>();
            var store2 = scope2.ServiceProvider.GetRequiredService<IDriverStore>();

            var driverFrom1 = (await store1.FindAsync(driver1Id, CancellationToken.None))!;
            var driverFrom2 = (await store2.FindAsync(driver1Id, CancellationToken.None))!;

            driverFrom1.SetApprovalStatus(DriverApprovalStatus.Suspended);
            driverFrom2.TrySetOperationalStatus(DriverOperationalStatus.Available, DateTimeOffset.UtcNow);

            await store1.SaveChangesAsync(CancellationToken.None);
            await Assert.ThrowsAsync<DriverStateConcurrencyException>(() =>
                store2.SaveChangesAsync(CancellationToken.None));
        }

        await using (var verifyScope = environment.Application.Services.CreateAsyncScope())
        {
            var store = verifyScope.ServiceProvider.GetRequiredService<IDriverStore>();
            var persisted = (await store.FindAsync(driver1Id, CancellationToken.None))!;
            Assert.Equal(DriverApprovalStatus.Suspended, persisted.ApprovalStatus);
            Assert.Equal(DriverOperationalStatus.Offline, persisted.OperationalStatus);
            Assert.Null(persisted.AvailableSince);
        }

        // 2. Reverse race: Operational status update commits first, competing approval revocation fails with DriverStateConcurrencyException
        Guid driver2Id;
        await using (var setupScope = environment.Application.Services.CreateAsyncScope())
        {
            var store = setupScope.ServiceProvider.GetRequiredService<IDriverStore>();
            var driver = Driver.Create("Store Test Driver 2");
            driver.SetApprovalStatus(DriverApprovalStatus.Approved);
            await store.AddAsync(driver, CancellationToken.None);
            await store.SaveChangesAsync(CancellationToken.None);
            driver2Id = driver.Id;
        }

        await using (var scopeA = environment.Application.Services.CreateAsyncScope())
        await using (var scopeB = environment.Application.Services.CreateAsyncScope())
        {
            var storeA = scopeA.ServiceProvider.GetRequiredService<IDriverStore>();
            var storeB = scopeB.ServiceProvider.GetRequiredService<IDriverStore>();

            var driverFromA = (await storeA.FindAsync(driver2Id, CancellationToken.None))!;
            var driverFromB = (await storeB.FindAsync(driver2Id, CancellationToken.None))!;

            driverFromA.TrySetOperationalStatus(DriverOperationalStatus.Available, DateTimeOffset.UtcNow);
            driverFromB.SetApprovalStatus(DriverApprovalStatus.Suspended);

            await storeA.SaveChangesAsync(CancellationToken.None);
            await Assert.ThrowsAsync<DriverStateConcurrencyException>(() =>
                storeB.SaveChangesAsync(CancellationToken.None));
        }
    }

    [Fact]
    public async Task Concurrent_approval_revocation_and_mark_available_never_leaves_unapproved_driver_available()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var client = environment.Client;

        // Run multiple rounds to exercise different thread scheduling and race winners
        for (var i = 0; i < 5; i++)
        {
            using var createResponse = await client.PostAsJsonAsync(
                "/api/v1/drivers",
                new { name = $"Race Driver {i}" });
            createResponse.EnsureSuccessStatusCode();
            var driverId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();

            using var approveResponse = await client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/approval",
                new { status = "APPROVED" });
            approveResponse.EnsureSuccessStatusCode();

            // Concurrently revoke approval and mark driver available
            var revokeTask = client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/approval",
                new { status = "SUSPENDED" });
            var availableTask = client.PutAsJsonAsync(
                $"/api/v1/drivers/{driverId}/operational-status",
                new { status = "AVAILABLE" });

            var responses = await Task.WhenAll(revokeTask, availableTask);
            var revokeRes = responses[0];
            var availRes = responses[1];

            // Revocation must always succeed
            Assert.Equal(HttpStatusCode.OK, revokeRes.StatusCode);

            // Availability must either succeed (if it committed first and was subsequently revoked to Offline)
            // or return 409 Conflict DRIVER_NOT_APPROVED (if revocation committed first)
            Assert.True(
                availRes.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                $"Unexpected status code {(int)availRes.StatusCode}: {await availRes.Content.ReadAsStringAsync()}");

            if (availRes.StatusCode == HttpStatusCode.Conflict)
            {
                using var problem = JsonDocument.Parse(await availRes.Content.ReadAsStringAsync());
                Assert.Equal("DRIVER_NOT_APPROVED", problem.RootElement.GetProperty("code").GetString());
            }

            // CRITICAL INVARIANT: The unapproved driver must NEVER be persisted as AVAILABLE
            using var getResponse = await client.GetAsync($"/api/v1/drivers/{driverId}");
            getResponse.EnsureSuccessStatusCode();
            var persisted = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());

            Assert.Equal("SUSPENDED", persisted.RootElement.GetProperty("approvalStatus").GetString());
            Assert.Equal("OFFLINE", persisted.RootElement.GetProperty("operationalStatus").GetString());
            Assert.Equal(JsonValueKind.Null, persisted.RootElement.GetProperty("availableSince").ValueKind);
        }
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(PostgreSqlContainer database, WebApplicationFactory<Program> application, HttpClient client)
        {
            Database = database;
            Application = application;
            Client = client;
        }

        public PostgreSqlContainer Database { get; }
        public WebApplicationFactory<Program> Application { get; }
        public HttpClient Client { get; }

        public static async Task<TestEnvironment> CreateAsync()
        {
            var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("ride_dispatch")
                .WithUsername("ride_dispatch")
                .WithPassword("ride_dispatch")
                .Build();

            await database.StartAsync();

            var application = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                    builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));

            await using (var scope = application.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            return new(database, application, application.CreateClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Application.DisposeAsync();
            await Database.DisposeAsync();
        }
    }
}

