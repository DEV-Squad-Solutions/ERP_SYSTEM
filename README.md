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

The Identity seed ensures these test accounts exist:

| Username | Password | Roles | Company access |
|---|---|---|---|
| `admin` | `P@ssword123` | `Admin`, `User` | All three seeded companies |
| `user` | `P@ssword123` | `User` | Primary seeded company only |

The seeder does not delete other application users. It creates one primary company plus two additional simulation companies and assigns all three to `admin`, allowing the company-selection login flow to be tested. It assigns only the primary company to `user`, allowing the direct-login flow to be tested. Seeding remains idempotent and creates three company-specific stores, drivers, and shared customer/supplier business partners, six standard item units, and `Seed:ItemCount` visibly company-labelled mock items per seeded company with deterministic codes. Business-partner, driver, item, item-unit, and store endpoints read the single `company_id` in the selected-company access token and return only that company's data.

Both roles can read company master data. Creating, updating, or deleting
business partners, drivers, stores, items, and item units requires the `Admin`
role.

User create and update requests accept a `roles` array, for example
`"roles": ["Admin", "User"]`. A user must have at least one role. Changed role
assignments are included in newly issued access tokens, so the affected user must
log in again after an administrator updates their roles.

## React test client

The separate `F:/client/client` project is a responsive React/Vite test client
used to exercise backend contracts. It is not the production frontend.
Successful login and company selection open a company-scoped workspace with
CRUD screens and reports.

```powershell
cd F:/client/client
npm install
npm run dev
```

The client targets `https://localhost:7067/api/v1` by default. Override it with `VITE_API_BASE_URL` or edit the API URL on the login screen. Companies and Users require the `Admin` role; non-admin users receive read-only catalog screens.

Frontend SignalR handoff documents:

- [`FRONTEND_SIGNALR_QUICK_START_AR.md`](FRONTEND_SIGNALR_QUICK_START_AR.md)
  is the short Arabic quick-start checklist.
- [`FRONTEND_SIGNALR_GUIDE_AR.md`](FRONTEND_SIGNALR_GUIDE_AR.md) is the Arabic
  explanation with a complete React example and resource-to-page routing.
- [`FRONTEND_SIGNALR_PRACTICAL_STEPS.md`](FRONTEND_SIGNALR_PRACTICAL_STEPS.md)
  is the short implementation checklist.
- [`FRONTEND_SIGNALR_INTEGRATION_GUIDE.md`](FRONTEND_SIGNALR_INTEGRATION_GUIDE.md)
  is the complete contract, client implementation, delivery behavior,
  filtering guidance, troubleshooting, and acceptance test guide.

## Swagger

When `Swagger:Enabled` is `true`, Swagger UI is available at `/swagger` and the generated document is available at `/swagger/v1/swagger.json`. Set it to `false` for environments where the UI should be disabled. API routes use URL-segment versioning and the controller token: `/api/v1/Items` and `/api/v1/ItemUnits`.

Swagger persists the authorized access token in the same browser, so refreshing
the Swagger page does not require entering the token again.

Business-partner, driver, item, and item-unit lists are paginated:

```http
GET /api/v1/Drivers?pageNumber=1&pageSize=20
GET /api/v1/BusinessPartners?pageNumber=1&pageSize=20
GET /api/v1/Items?pageNumber=1&pageSize=20
GET /api/v1/ItemUnits?pageNumber=1&pageSize=20
```

`pageNumber` defaults to `1`, `pageSize` defaults to `20`, and the maximum page
size is `100`. The response includes `items`, `pageNumber`, `pageSize`,
`totalCount`, and `totalPages`.

## JWT authentication

The API issues short-lived JWT access tokens and rotating refresh tokens. All
development token settings are under `Jwt` in `appsettings.json`. In
production, replace the development signing key with a key containing at least
32 bytes through a secret provider or the `Jwt__SigningKey` environment
variable.

Sign in with an existing Identity user:

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "userName": "admin",
  "password": "P@ssword123"
}
```

Because the seeded `admin` has multiple companies, login returns a short-lived selection token instead of the final access and refresh tokens:

```json
{
  "requiresCompanySelection": true,
  "selectionToken": "...",
  "accessToken": null,
  "refreshToken": null,
  "fullName": "System Administrator",
  "email": "admin@minierp.local",
  "roles": ["Admin", "User"],
  "companies": [
    { "id": 1, "name": "..." },
    { "id": 2, "name": "MiniERP Trading Company" },
    { "id": 3, "name": "MiniERP Distribution Company" }
  ]
}
```

Do not put `selectionToken` in Swagger's **Authorize** dialog. It has the
separate `MiniErp.Client.CompanySelection` audience and is accepted only by
`POST /api/v1/Auth/select-company`.

Select one of the returned companies to receive the final company-scoped tokens:

```http
POST /api/v1/Auth/select-company
Content-Type: application/json

{
  "selectionToken": "...",
  "companyId": 2
}
```

The company-selection response contains `accessToken` and `refreshToken`.
Put only that `accessToken` in Swagger's **Authorize** dialog. Swagger adds the
`Bearer` prefix automatically.

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

Follow the detailed [MiniErp Feature Development Guide](FEATURE_DEVELOPMENT_GUIDE.md)
before implementing a feature, especially its cross-service impact and
foreign-key delete checks.

1. Put business entities and rules in `MiniErp.Domain`.
2. Define the use case and any required external port in `MiniErp.Application`.
3. Implement external ports in `MiniErp.Infrastructure`.
4. Expose the use case from `MiniErp.Api` and register its dependencies in `Program.cs`.
