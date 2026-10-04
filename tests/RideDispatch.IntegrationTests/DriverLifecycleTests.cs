using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RideDispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RideDispatch.IntegrationTests;

public sealed class DriverLifecycleTests
{
    [Fact]
    public async Task Driver_can_be_created_approved_and_made_available()
    {
        await using var database = new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithDatabase("ride_dispatch")
            .WithUsername("ride_dispatch")
            .WithPassword("ride_dispatch")
            .Build();

        await database.StartAsync();

        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = application.CreateClient();

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
}
