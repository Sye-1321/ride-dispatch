# Ride Dispatch

A focused ASP.NET Core backend for correct, explainable ride-to-driver allocation.

Status: early implementation.

Current scope: driver lifecycle, registered vehicles, and PostGIS-backed latest driver locations.

Target stack: .NET 10 / ASP.NET Core / PostgreSQL / PostGIS.

## Local development

PostgreSQL with PostGIS is required.

```powershell
docker compose up -d database
dotnet tool restore
dotnet ef database update --project src/RideDispatch.Infrastructure --startup-project src/RideDispatch.Api
dotnet run --project src/RideDispatch.Api
```
