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

public sealed class VehicleRegistrationTests
{
    [Fact]
    public async Task Vehicle_can_be_registered_and_retrieved_once_for_an_existing_driver()
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

        using var createDriverResponse = await client.PostAsJsonAsync(
            "/api/v1/drivers",
            new { name = "Abebe Kebede" });
        createDriverResponse.EnsureSuccessStatusCode();

        using var createdDriver = JsonDocument.Parse(
            await createDriverResponse.Content.ReadAsStringAsync());
        var driverId = createdDriver.RootElement.GetProperty("id").GetGuid();

        using var registerResponse = await client.PostAsJsonAsync(
            $"/api/v1/drivers/{driverId}/vehicle",
            new { type = "STANDARD", plateNumber = "  2-B12345  " });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using var registered = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var vehicleId = registered.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(driverId, registered.RootElement.GetProperty("driverId").GetGuid());
        Assert.Equal("STANDARD", registered.RootElement.GetProperty("type").GetString());
        Assert.Equal("2-B12345", registered.RootElement.GetProperty("plateNumber").GetString());

        using var getResponse = await client.GetAsync($"/api/v1/drivers/{driverId}/vehicle");
        getResponse.EnsureSuccessStatusCode();

        using var retrieved = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(vehicleId, retrieved.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(driverId, retrieved.RootElement.GetProperty("driverId").GetGuid());
        Assert.Equal("STANDARD", retrieved.RootElement.GetProperty("type").GetString());
        Assert.Equal("2-B12345", retrieved.RootElement.GetProperty("plateNumber").GetString());

        using var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/v1/drivers/{driverId}/vehicle",
            new { type = "XL", plateNumber = "3-C98765" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var duplicateProblem = JsonDocument.Parse(
            await duplicateResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "VEHICLE_ALREADY_REGISTERED",
            duplicateProblem.RootElement.GetProperty("code").GetString());

        using var missingDriverResponse = await client.PostAsJsonAsync(
            $"/api/v1/drivers/{Guid.CreateVersion7()}/vehicle",
            new { type = "STANDARD", plateNumber = "4-D54321" });
        Assert.Equal(HttpStatusCode.NotFound, missingDriverResponse.StatusCode);
    }
}
