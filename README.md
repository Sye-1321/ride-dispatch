# Ride Dispatch

A focused ASP.NET Core backend for correct, explainable ride-to-driver allocation.

Status: early implementation.

Current scope: driver operating state, vehicles, PostGIS locations, commission standing, passengers, and ride requests.

Dispatch eligibility filters nearby drivers by approval, availability, vehicle compatibility,
location freshness/radius, and commission standing. Eligible drivers are ranked by the NEAREST
or LONGEST_IDLE order. Allocation runs persist recommendations, candidate evidence, and rejection
reasons. Dispatch also supports allocation runs, time-bounded driver offers, accepted-offer
assignments, and database-backed protection against overlapping driver assignments. Assignment
conflicts and declined/expired Offer history are retained in candidate evidence when allocating the
same ride again. Failed Offers can drive an explicit, retry-safe replacement dispatch. Assignments
support completion and cancellation; only active Assignments reserve driver schedules. A dispatcher may
override the ranked recommendation by selecting another candidate already persisted as eligible, with a
required audit reason; override does not bypass eligibility. Scheduled dispatch activation remains future work.

Target stack: .NET 10 / ASP.NET Core / PostgreSQL / PostGIS.

## Local development

PostgreSQL with PostGIS is required.

```powershell
docker compose up -d database
dotnet tool restore
dotnet ef database update --project src/RideDispatch.Infrastructure --startup-project src/RideDispatch.Api
dotnet run --project src/RideDispatch.Api
```
