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

The current base seed password is a temporary development credential. Replace it with `Seed__Password` through the production environment or a secret provider before using the application in a real production environment. The application fails fast when seeding is enabled without a password.

The Identity seed contains exactly these accounts:

| Username | Password | Role |
|---|---|---|
| `admin` | `P@ssword123` | `Admin` |
| `user` | `P@ssword123` | `User` |

When the seeder runs, it removes every other Identity user, updates these two accounts to the configured password, and corrects their role membership. Disable `Seed:Enabled` after initialization if the application will create additional users. Catalog seeding remains idempotent and creates six standard item units plus `Seed:ItemCount` mock items with deterministic `ITEM-0001` codes.

## Swagger

When `Swagger:Enabled` is `true`, Swagger UI is available at `/swagger` and the generated document is available at `/swagger/v1/swagger.json`. Set it to `false` for environments where the UI should be disabled. API routes use URL-segment versioning and the controller token: `/api/v1/Items` and `/api/v1/ItemUnits`.

## JWT authentication

The API issues short-lived JWT access tokens and rotating refresh tokens. In production, configure a signing key containing at least 32 bytes through a secret provider or the `Jwt__SigningKey` environment variable. The signing key in `appsettings.Development.json` is for local development only.

Sign in with an existing Identity user:

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "userName": "admin",
  "password": "P@ssword123"
}
```

The login response contains the access and refresh tokens with basic user information:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "fullName": "System Administrator",
  "email": "admin@minierp.local"
}
```

The refresh response contains only `accessToken` and `refreshToken`.

Use the access token as `Authorization: Bearer {accessToken}`. To rotate an expired or expiring access token, call:

```http
POST /api/v1/Auth/refresh
Content-Type: application/json

{
  "refreshToken": "..."
}
```

Each successful refresh revokes the submitted refresh token and returns a new access-token/refresh-token pair. Refresh tokens are stored in the database only as SHA-256 hashes. Access tokens are not stored. All API controllers require authentication except the login and refresh actions.

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
