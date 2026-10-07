# Ride Dispatch

A focused ASP.NET Core backend for correct, explainable ride-to-driver allocation.

Status: early implementation.

Current scope: driver operating state, vehicles, PostGIS locations, commission standing, passengers, and ride requests.

Dispatch eligibility filters nearby drivers by approval, availability, vehicle compatibility,
location freshness/radius, and commission standing. Allocation ranking, offers, and assignments
are forthcoming.

Target stack: .NET 10 / ASP.NET Core / PostgreSQL / PostGIS.

## Local development

PostgreSQL with PostGIS is required.

```powershell
docker compose up -d database
dotnet tool restore
dotnet ef database update --project src/RideDispatch.Infrastructure --startup-project src/RideDispatch.Api
dotnet run --project src/RideDispatch.Api
```
