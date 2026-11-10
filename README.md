Rira
Person CRUD service built with .NET 10, gRPC, EF Core, and SQLite.
Prerequisites
.NET 10 SDK
A gRPC client
Run all commands in PowerShell from the directory containing `Rira.slnx`.
Restore and Build
```powershell
dotnet restore Rira.slnx
dotnet tool restore
dotnet build Rira.slnx -c Release
```
Create or Update the Database
Use the same connection string for migrations and the application:
```powershell
$dbPath = Join-Path (Get-Location).Path "rira.db"
$connection = "Data Source=$dbPath"
$env:ConnectionStrings__Rira = $connection

dotnet ef database update `
  --project Rira.Infrastructure `
  --startup-project Rira.Endpoint `
  --configuration Release --no-build `
  --connection "$connection"
```
This creates the SQLite database if needed and applies existing migrations.
The application does not apply migrations automatically.
Run the Application
Run in the same terminal to preserve the connection string:
```powershell
dotnet run --project Rira.Endpoint `
  --configuration Release --no-build --launch-profile http
```
The service listens at `http://localhost:5120` using HTTP/2.
Import `Rira.Endpoint/Protos/persons.proto` into your gRPC client.
Server reflection is not enabled.
If you open a new terminal, set the connection string again before running the application or updating the database.
Add a Migration
After changing the persistence model:
```powershell
dotnet ef migrations add YourMigrationName `
  --project Rira.Infrastructure `
  --startup-project Rira.Endpoint `
  --output-dir Persistence/Migrations
```
Review the generated migration, then apply it:
```powershell
dotnet ef database update `
  --project Rira.Infrastructure `
  --startup-project Rira.Endpoint `
  --connection "$connection"
```
Run Tests
```powershell
dotnet test Rira.slnx -c Release
```

## Error Handling

- Invalid input returns `InvalidArgument`; missing people return `NotFound`.
- Duplicate values return `AlreadyExists`; optimistic concurrency conflicts return `Aborted`.
- Failed saves discard pending tracked changes. Reload current data before retrying a concurrency conflict.
- Unexpected exceptions are logged and return a generic `Internal` response without internal details.
- The interceptor handles request cancellation and deadline expiration with `Cancelled` and `DeadlineExceeded`.
