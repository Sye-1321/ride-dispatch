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

public sealed class DriverLocationTests
{
    [Fact]
    public async Task Latest_locations_are_stored_and_searched_by_radius_in_postgis()
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

        var driverA = await CreateDriverAsync(client, "Driver A");
        var driverB = await CreateDriverAsync(client, "Driver B");
        var driverC = await CreateDriverAsync(client, "Driver C");

        await UpdateLocationAsync(client, driverA, 9.0300, 38.7400);
        await UpdateLocationAsync(client, driverB, 9.0350, 38.7400);
        await UpdateLocationAsync(client, driverC, 9.1300, 38.7400);

        using var initialGetResponse = await client.GetAsync($"/api/v1/drivers/{driverA}/location");
        initialGetResponse.EnsureSuccessStatusCode();
        using var initialLocation = JsonDocument.Parse(
            await initialGetResponse.Content.ReadAsStringAsync());
        Assert.Equal(9.0300, initialLocation.RootElement.GetProperty("latitude").GetDouble());
        Assert.Equal(38.7400, initialLocation.RootElement.GetProperty("longitude").GetDouble());

        await UpdateLocationAsync(client, driverA, 9.0310, 38.7400);

        using var latestGetResponse = await client.GetAsync($"/api/v1/drivers/{driverA}/location");
        latestGetResponse.EnsureSuccessStatusCode();
        using var latestLocation = JsonDocument.Parse(
            await latestGetResponse.Content.ReadAsStringAsync());
        Assert.Equal(9.0310, latestLocation.RootElement.GetProperty("latitude").GetDouble());
        Assert.Equal(38.7400, latestLocation.RootElement.GetProperty("longitude").GetDouble());
        Assert.Equal(JsonValueKind.String, latestLocation.RootElement.GetProperty("recordedAt").ValueKind);

        using var nearbyResponse = await client.GetAsync(
            "/api/v1/drivers/nearby?latitude=9.03&longitude=38.74&radiusMeters=2000");
        nearbyResponse.EnsureSuccessStatusCode();

        using var nearby = JsonDocument.Parse(await nearbyResponse.Content.ReadAsStringAsync());
        var results = nearby.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, results.Length);
        Assert.Equal(driverA, results[0].GetProperty("driverId").GetGuid());
        Assert.Equal(driverB, results[1].GetProperty("driverId").GetGuid());
        Assert.DoesNotContain(results, item => item.GetProperty("driverId").GetGuid() == driverC);

        var firstDistance = results[0].GetProperty("distanceMeters").GetDouble();
        var secondDistance = results[1].GetProperty("distanceMeters").GetDouble();
        Assert.InRange(firstDistance, 50, 200);
        Assert.InRange(secondDistance, 400, 700);
        Assert.True(firstDistance <= secondDistance);

        Assert.Equal(9.0310, results[0].GetProperty("latitude").GetDouble());
        Assert.Equal(38.7400, results[0].GetProperty("longitude").GetDouble());
        Assert.All(
            results,
            item => Assert.Equal(JsonValueKind.String, item.GetProperty("recordedAt").ValueKind));

        using var missingDriverResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{Guid.CreateVersion7()}/location",
            new { latitude = 9.03, longitude = 38.74 });
        Assert.Equal(HttpStatusCode.NotFound, missingDriverResponse.StatusCode);

        using var invalidCoordinateResponse = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverA}/location",
            new { latitude = 91.0, longitude = 38.74 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinateResponse.StatusCode);
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", new { name });
        response.EnsureSuccessStatusCode();

        using var driver = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return driver.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task UpdateLocationAsync(
        HttpClient client,
        Guid driverId,
        double latitude,
        double longitude)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/drivers/{driverId}/location",
            new { latitude, longitude });
        response.EnsureSuccessStatusCode();
    }
}
