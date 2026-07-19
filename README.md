# MiniErp

MiniErp is a .NET 10 Web API organized with Clean Architecture. Dependencies point inward: the domain has no project dependencies, application depends only on the domain, infrastructure implements application ports, and the API is the composition root.

## Project structure

```text
src/
|-- MiniErp.Domain/          Enterprise models and rules
|-- MiniErp.Application/     Use cases, response models, and ports
|-- MiniErp.Infrastructure/  EF Core, Identity, and application port implementations
`-- MiniErp.Api/             HTTP endpoints and dependency composition
```

```text
Api ------------> Application ------------> Domain
 |                     ^
 `-> Infrastructure ---'
```

## Run

```powershell
dotnet restore MiniErp.slnx
dotnet run --project src/MiniErp.Api/MiniErp.Api.csproj
```

## Database and Identity

The infrastructure project uses EF Core with SQL Server and ASP.NET Core Identity. The development connection string is named `DefaultConnection`; override it outside local development with the `ConnectionStrings__DefaultConnection` environment variable or a secure configuration provider.

Create or update the database with:

```powershell
dotnet tool restore
dotnet ef database update --project src/MiniErp.Infrastructure --startup-project src/MiniErp.Api
```

The initial migration creates the standard Identity tables for users, roles, claims, external logins, tokens, and user-role membership. Pending migrations are applied automatically at startup when `Database:ApplyMigrationsOnStartup` is `true` (the default). Set it to `false` in environments where migrations are managed by deployment tooling.

## Seed data

Infrastructure includes an idempotent Bogus seeder for Identity users and catalog data. Pending migrations are applied before the seeder runs. Seeding is enabled by default in the base configuration, including production.

```powershell
$env:Seed__Enabled = "true"
$env:Seed__Password = "use-a-strong-secret-password"
dotnet run --project src/MiniErp.Api/MiniErp.Api.csproj --launch-profile https
```

The current base seed password is a temporary development credential. Replace it with `Seed__Password` through the production environment or a secret provider before using the application in a real production environment. The application fails fast when seeding is enabled without a password. The seeder creates the configured number of `demo1@minierp.local`, `demo2@minierp.local`, and so on, six standard item units, and `Seed:ItemCount` mock items with deterministic `ITEM-0001` codes. Existing users, units, and item codes are skipped, so restarting the application does not duplicate seed data.

## Swagger

When `Swagger:Enabled` is `true`, Swagger UI is available at `/swagger` and the generated document is available at `/swagger/v1/swagger.json`. Set it to `false` for environments where the UI should be disabled. API routes use URL-segment versioning and the controller token: `/api/v1/Items` and `/api/v1/ItemUnits`.

## Result pattern

Application use cases should return `Result<T>` for expected business outcomes instead of throwing exceptions for normal control flow:

```csharp
return Result<CustomerResponse>.Failure(
    Error.NotFound("Customer.NotFound", "The customer was not found."));
```

Use `Error.Validation`, `Error.Conflict`, `Error.Unauthorized`, and `Error.Forbidden` for their corresponding outcomes. Keep unexpected infrastructure failures as exceptions so they can be logged and translated by global exception handling at the API boundary.

## FluentValidation

FluentValidation is referenced by the Application layer, and the API automatically scans the Application assembly for validators. Add validators next to their request models:

```csharp
public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
    }
}
```

Validate requests explicitly in the application use case or endpoint, then convert validation failures to `Error.Validation(...)` and the existing `Result<T>` pattern. This avoids coupling the Application layer to ASP.NET MVC validation behavior.

## Adding an ERP feature

1. Put business entities and rules in `MiniErp.Domain`.
2. Define the use case and any required external port in `MiniErp.Application`.
3. Implement external ports in `MiniErp.Infrastructure`.
4. Expose the use case from `MiniErp.Api` and register its dependencies in `Program.cs`.
