# Ride Dispatch

A focused ASP.NET Core backend for correct, explainable ride-to-driver allocation.

Status: early implementation.

Current scope: driver operating state, vehicles, PostGIS locations, commission standing, passengers, and ride requests.

Dispatch eligibility filters nearby drivers by approval, availability, vehicle compatibility,
location freshness/radius, and commission standing. Eligible drivers are ranked by the NEAREST
or LONGEST_IDLE order. Allocation runs persist recommendations, candidate evidence, and rejection
reasons. Dispatch also supports allocation runs, time-bounded driver offers, accepted-offer
assignments, and database-backed protection against overlapping driver assignments. Assignment
conflicts are retained in the existing candidate evidence. Assignment cancellation/completion,
reassignment after decline/expiry, dispatcher/manual override, and scheduled dispatch activation
remain future work.

Target stack: .NET 10 / ASP.NET Core / PostgreSQL / PostGIS.

## Local development

PostgreSQL with PostGIS is required.

```powershell
docker compose up -d database
dotnet tool restore
dotnet ef database update --project src/RideDispatch.Infrastructure --startup-project src/RideDispatch.Api
dotnet run --project src/RideDispatch.Api
```
