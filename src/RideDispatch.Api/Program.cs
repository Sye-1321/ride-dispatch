using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RideDispatch.Application.Drivers;
using RideDispatch.Application.Vehicles;
using RideDispatch.Infrastructure;
using RideDispatch.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseUpper,
                allowIntegerValues: false)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateDriver>();
builder.Services.AddScoped<GetDriver>();
builder.Services.AddScoped<ListDrivers>();
builder.Services.AddScoped<UpdateDriverApproval>();
builder.Services.AddScoped<UpdateDriverOperationalStatus>();
builder.Services.AddScoped<RegisterVehicle>();
builder.Services.AddScoped<GetDriverVehicle>();
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
