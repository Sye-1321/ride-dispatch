using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RideDispatch.Application.Drivers;
using RideDispatch.Application.Vehicles;
using RideDispatch.Infrastructure.Persistence;

namespace RideDispatch.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The ConnectionStrings:Database connection string is required.");
        }

        services.AddDbContext<DispatchDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        services.AddScoped<IDriverStore, DriverStore>();
        services.AddScoped<IVehicleStore, VehicleStore>();

        return services;
    }
}
