using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.DriverFinancialStanding;
using RideDispatch.Application.DriverLocations;
using RideDispatch.Application.Drivers;
using RideDispatch.Application.Passengers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Application.Vehicles;
using RideDispatch.Infrastructure;
using RideDispatch.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var eligibilityConfiguration = builder.Configuration.GetSection("Dispatch:Eligibility");
var eligibilityPolicy = new DispatchEligibilityPolicy(
    eligibilityConfiguration.GetValue<double>("MaxRadiusMeters"),
    TimeSpan.FromSeconds(eligibilityConfiguration.GetValue<double>("LocationFreshnessSeconds")),
    eligibilityConfiguration.GetValue<decimal>("MinimumCommissionBalance"));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseUpper,
                allowIntegerValues: false)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(eligibilityPolicy);
builder.Services.AddScoped<CreateDriver>();
builder.Services.AddScoped<GetDriver>();
builder.Services.AddScoped<ListDrivers>();
builder.Services.AddScoped<UpdateDriverApproval>();
builder.Services.AddScoped<UpdateDriverOperationalStatus>();
builder.Services.AddScoped<RegisterVehicle>();
builder.Services.AddScoped<GetDriverVehicle>();
builder.Services.AddScoped<UpdateDriverLocation>();
builder.Services.AddScoped<GetDriverLocation>();
builder.Services.AddScoped<FindNearbyDrivers>();
builder.Services.AddScoped<UpdateDriverFinancialStanding>();
builder.Services.AddScoped<GetDriverFinancialStanding>();
builder.Services.AddScoped<CreatePassenger>();
builder.Services.AddScoped<GetPassenger>();
builder.Services.AddScoped<CreateRideRequest>();
builder.Services.AddScoped<GetRideRequest>();
builder.Services.AddScoped<ListRideRequests>();
builder.Services.AddScoped<EvaluateDispatchCandidates>();
builder.Services.AddScoped<FindEligibleDrivers>();
builder.Services.AddScoped<CreateAllocationRun>();
builder.Services.AddScoped<GetAllocationRun>();
builder.Services.AddScoped<GetAllocationCandidateEvaluations>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<DispatchDbContext>(tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
});

app.Run();

public partial class Program
{
}
