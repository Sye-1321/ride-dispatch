using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RideDispatch.Infrastructure;
using RideDispatch.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
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
