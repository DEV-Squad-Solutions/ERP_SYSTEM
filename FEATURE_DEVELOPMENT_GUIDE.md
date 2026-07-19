# MiniErp Feature Development Guide

Use this guide whenever adding or changing a feature. The goal is to keep the
Domain, Application, Infrastructure, and API layers consistent and to prevent a
change in one feature from silently breaking another feature.

## 1. Define the feature before coding

Write down the following:

- Feature name and business purpose.
- API operations: list, select, get, create, update, and delete.
- Users or roles allowed to use each operation.
- Entities and tables that will be read or changed.
- Existing services, response models, or endpoints that may be affected.
- Validation, uniqueness, and active/inactive rules.
- Whether deletion is physical or soft deletion.

Do not start implementation until the affected entities and relationships are
known.

## 2. Mandatory impact confirmation

Before changing a feature, answer every question in this table. A `Yes` answer
must include the affected component and the verification that will be run.

| Question | Yes/No | Affected component | Required verification |
|---|---|---|---|
| Does this change another application service? | | | Service and integration tests |
| Does it change a shared request or response model? | | | All API consumers and Swagger |
| Does it change entity mapping or database schema? | | | Migration and pending-model check |
| Does another table reference this entity? | | | Foreign-key and delete checks |
| Does this entity reference another table? | | | Validate the referenced record |
| Does it affect authentication, roles, or claims? | | | Authorized and unauthorized requests |
| Does it affect seed data? | | | Fresh and existing database startup |
| Does it affect audit fields or the current user? | | | Create/update/delete audit values |
| Does it change filtering, selection, or active-state behavior? | | | List and select endpoints |

Search the repository before deciding that a change is isolated:

```powershell
rg -n "EntityName|EntityId|IEntityService|EntityResponse" src
```

Check controllers, services, mappings, validators, configurations, migrations,
seeders, and navigation properties. Do not confirm "no impact" from the service
file alone.

## 3. Place code in the correct layer

```text
MiniErp.Domain
  Entities and business rules

MiniErp.Application
  Requests, responses, validators, service interfaces, and mappings

MiniErp.Infrastructure
  EF Core configurations, service implementations, Identity, and persistence

MiniErp.Api
  Controllers, HTTP responses, authorization, and Swagger documentation
```

Recommended feature layout:

```text
src/MiniErp.Application/Features/FeatureName/
  IFeatureNameService.cs
  FeatureNameRequest.cs
  FeatureNameRequestValidator.cs
  FeatureNameResponse.cs
  FeatureNameMappingRegister.cs

src/MiniErp.Infrastructure/Services/FeatureName/
  FeatureNameService.cs

src/MiniErp.Api/Controllers/
  FeatureNameController.cs

src/MiniErp.Api/Swagger/
  FeatureNameSwaggerDocumentation.cs
```

Application services should return `Result<T>` for expected business failures.
Use the matching error type:

- `Error.Validation` for invalid input or identifiers.
- `Error.NotFound` when a requested or referenced record does not exist.
- `Error.Conflict` for duplicate values, dependent records, or invalid state.
- `Error.Unauthorized` and `Error.Forbidden` for access failures.

Unexpected database or infrastructure failures should remain exceptions and be
handled by the global exception handler.

## 4. Foreign-key checks are required

Before implementing create, update, or delete, inspect both directions of every
relationship.

### Outgoing foreign keys

If the new or updated entity contains a foreign key, verify that the referenced
record exists before saving. Also verify active state when inactive parent
records must not be selected.

```csharp
var parentExists = await dbContext.Parents.AnyAsync(
    parent => parent.Id == request.ParentId && parent.IsActive,
    cancellationToken);

if (!parentExists)
{
    return Result<FeatureResponse>.Failure(
        Error.NotFound(
            "Parents.NotFound",
            $"Active parent with ID {request.ParentId} was not found."));
}
```

### Incoming foreign keys

Before deleting an entity, find every table that references it. Check:

- Entity navigation properties.
- `IEntityTypeConfiguration<T>` classes.
- `HasForeignKey`, `OnDelete`, and `DeleteBehavior` calls.
- Existing migrations and the model snapshot.
- Services that query the entity ID without a navigation property.

Useful searches:

```powershell
rg -n "HasForeignKey|OnDelete|DeleteBehavior" src/MiniErp.Infrastructure
rg -n "EntityNameId|EntityName" src -g "*.cs"
```

Never assume that the database will safely choose the intended delete behavior.

Global query filters also affect dependency checks. Explicitly decide whether
soft-deleted dependents still count. Use a normal query when only current
records should block deletion, or `IgnoreQueryFilters()` when current and
historical records must both block deletion.

## 5. Choose delete behavior explicitly

Choose one of these behaviors for every relationship:

| Behavior | Use when | Service behavior |
|---|---|---|
| Restrict | Dependent data must prevent deletion | Check dependents and return `409 Conflict` |
| Cascade | Dependents have no meaning without the parent | Document and test all rows that will be deleted |
| Set null | The relationship is optional after deletion | Confirm the foreign key is nullable |
| Soft delete | Records must remain for history or auditing | Mark inactive/deleted and filter normal queries |

Prefer `DeleteBehavior.Restrict` for ERP master data unless the business rule
explicitly requires cascading deletion.

```csharp
builder.HasOne(entity => entity.Parent)
    .WithMany(parent => parent.Children)
    .HasForeignKey(entity => entity.ParentId)
    .OnDelete(DeleteBehavior.Restrict);
```

For restricted deletion, check dependencies in the service before calling
`Remove`:

```csharp
var hasDependencies = await dbContext.Children.AnyAsync(
    child => child.ParentId == id,
    cancellationToken);

if (hasDependencies)
{
    return Result.Failure(
        Error.Conflict(
            "Parents.HasDependencies",
            "The parent cannot be deleted because dependent records exist."));
}
```

Do not rely on a `DbUpdateException` as normal delete validation. The service
should return a clear business error before the database rejects the operation.

### Delete confirmation gate

Do not complete a delete feature until all statements are true:

- [ ] All incoming foreign keys have been identified.
- [ ] Query-filter behavior for current and historical dependents is confirmed.
- [ ] The EF Core delete behavior is explicitly configured.
- [ ] The business owner has chosen restrict, cascade, set-null, or soft delete.
- [ ] Restricted deletes return `409 Conflict` with a clear error code.
- [ ] Cascade deletes have tests proving exactly which records are removed.
- [ ] Soft-deleted records are excluded from normal list and select queries.
- [ ] Delete behavior is tested with and without dependent records.

## 6. Create and update checks

For create and update operations, confirm:

- IDs are greater than zero when applicable.
- Required strings are trimmed and validated.
- Unique codes or names are checked, excluding the current entity on update.
- Every foreign-key record exists.
- Required parent records are active.
- Mapping does not overwrite IDs or creation audit fields during update.
- The returned response contains the saved relationship details expected by the
  frontend.

## 7. Database migration workflow

Create a migration for every model change:

```powershell
dotnet ef migrations add MigrationName `
  --project src/MiniErp.Infrastructure `
  --startup-project src/MiniErp.Api
```

Inspect the generated migration before applying it. Confirm:

- Column types, nullability, and maximum lengths.
- Unique and lookup indexes.
- Foreign-key names and delete behavior.
- No unrelated table or column changes.
- The `Down` method safely reverses the migration.

Then verify the model and build:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/MiniErp.Infrastructure `
  --startup-project src/MiniErp.Api

dotnet build MiniErp.slnx
```

Do not manually edit the model snapshot unless a generated migration is being
carefully repaired.

## 8. API and authorization checks

For each endpoint:

- Use the versioned API controller base.
- Add correct `ProducesResponseType` declarations.
- Apply the required authorization or role policy.
- Keep only intentionally public endpoints marked `AllowAnonymous`.
- Update Swagger summaries and descriptions.
- Verify `400`, `401`, `403`, `404`, and `409` responses where applicable.
- Verify that authenticated requests work with `Authorization: Bearer {token}`.

Changing a response model requires confirming all frontend or external API
consumers before merging.

## 9. Required verification scenarios

At minimum, verify:

### Read

- Empty and populated lists.
- Existing and missing IDs.
- Select endpoints return only allowed active records.

### Create

- Valid request.
- Duplicate code or name.
- Missing or inactive foreign-key record.
- Validation errors.

### Update

- Valid request.
- Missing entity.
- Duplicate value belonging to another entity.
- Missing or inactive foreign-key record.

### Delete

- Missing entity.
- Entity without dependents.
- Entity with dependents.
- Confirmed soft-delete filtering or cascade results.

### Cross-feature impact

- Every service identified in the impact table still builds and behaves as
  expected.
- Shared response and selection models remain compatible.
- Seed startup works on both an existing and a fresh database.

## 10. Mandatory edge-case review

Every feature must record which edge cases apply and how each applicable case
was verified. Do not mark a case as not applicable without a reason.

### Input boundaries

- Zero and negative route or foreign-key IDs.
- Empty, whitespace-only, trimmed, minimum-length, and maximum-length strings.
- Values exactly at and one character beyond configured database limits.
- Null optional values and omitted optional JSON properties.
- Case-only differences in unique codes, names, usernames, and emails.
- Boolean state combinations such as active, inactive, and deleted.

Validation limits must match EF Core column limits. Normalized values used for
duplicate checks must be the same values saved to the database.

### Data relationships and state

- Referenced record is missing, soft-deleted, or inactive.
- Referenced record changes or is deleted between validation and save.
- Parent becomes inactive after a child has already been created.
- Dependency checks include or exclude historical records intentionally.
- Soft-deleted values interact correctly with filtered unique indexes.
- Create and update responses still contain required navigation details.
- Restoring data, if supported, does not violate a unique index or reference an
  unavailable parent.

### Concurrent requests

- Two requests attempt to create the same unique value simultaneously.
- Two requests update or delete the same record simultaneously.
- A dependent record is created while its parent is being deleted.
- Token or one-time-value rotation is attempted concurrently.

An application-level `AnyAsync` check does not replace a database unique index.
Use database constraints as the final protection and translate expected
constraint or concurrency failures into a clear result where appropriate.
Use a row-version concurrency token when lost updates would be harmful.

### Duplicate data

For every field that should be unique, confirm all of the following:

- Existing data is checked for duplicates before adding a unique index.
- The service checks the normalized value that will actually be saved.
- Case-only and whitespace-only differences follow the intended business rule.
- The database has a unique index as the final concurrency-safe protection.
- Update checks exclude the current record by ID.
- Soft-deleted records are intentionally included or excluded from uniqueness.
- Seeder reruns cannot create duplicate users, roles, codes, names, or lookup
  records.
- A clear cleanup or merge decision exists if historical duplicates are found.

Example SQL for reviewing an existing table before a unique migration:

```sql
SELECT Code, COUNT(*) AS DuplicateCount
FROM Items
WHERE IsDeleted = 0
GROUP BY Code
HAVING COUNT(*) > 1;
```

Repeat the check using the same normalization and filter used by the intended
unique index. Never add a unique constraint to existing data without first
checking whether the migration will fail.

### Query and response behavior

- Empty result sets and large result sets.
- Sorting is deterministic when multiple records have the same display value.
- Select endpoints exclude inactive or unavailable relationships.
- Global query filters behave correctly for normal and administrative queries.
- Projection and mapping handle optional or unavailable navigation properties.
- API response status and body match the declared Swagger contract.

Add pagination before an unbounded list can reasonably become large.

Use the shared `IPaginationService` with `PaginationRequest` and
`PagedResponse<T>` for paginated endpoints. Feature services should supply a
deterministically ordered `IOrderedQueryable<TEntity>` and must not duplicate
count, offset, projection, or total-page calculations.

```csharp
var query = dbContext.Entities
    .AsNoTracking()
    .OrderBy(entity => entity.Name)
    .ThenBy(entity => entity.Id);

return await paginationService.PaginateAsync<Entity, EntityResponse>(
    query,
    pagination,
    cancellationToken);
```

### Query performance and projection

Read endpoints should select only the columns required by their response. Use
server-side projection such as `ProjectToType<TResponse>()` or `Select(...)` so
EF Core does not materialize complete entities and navigation graphs.

```csharp
var response = await dbContext.Items
    .AsNoTracking()
    .Where(item => item.IsActive)
    .OrderBy(item => item.Name)
    .ProjectToType<ItemResponse>()
    .ToListAsync(cancellationToken);
```

Avoid O(n) database round trips and N+1 queries:

- Never call `FirstAsync`, `AnyAsync`, or another database query inside a loop
  over records.
- Load required IDs or related values in one query using joins, projection, or
  `Contains` with a bounded ID set.
- Use `AnyAsync` instead of loading a collection only to check whether it has
  rows.
- Use `AsNoTracking` for read-only queries.
- Keep filtering, sorting, projection, and pagination in the database query.
- Do not call `ToListAsync` before filters or projection that SQL can perform.
- Avoid `Include` when projection can return the required related fields.
- Inspect generated SQL when a query contains multiple relationships or an
  unexpected number of round trips.

Returning n records naturally requires O(n) result processing. The requirement
is to avoid O(n) separate database calls, repeated full-table scans, and
unbounded entity materialization.

### Authorization and security

- Missing, malformed, expired, and valid access tokens.
- Authenticated user with the wrong role receives `403 Forbidden`.
- Anonymous endpoints do not accidentally expose protected data.
- User-supplied IDs cannot access or modify data outside the permitted scope.
- Error messages do not expose passwords, token hashes, connection strings, or
  internal exception details.

### Audit, time, and transactions

- Create, update, and delete operations set the correct actor and UTC timestamp.
- Failed operations do not leave partial data or misleading audit values.
- Multi-step writes use a transaction when partial completion is invalid.
- Cancellation before save does not create partial records.
- Time comparisons use UTC consistently, especially for expiration behavior.

### Seed and migration behavior

- Seeder can run repeatedly without duplicates or unexpected data loss.
- Destructive seed behavior is explicit and disabled when no longer required.
- Migration works for both an empty database and a database containing data.
- New required columns have a safe value or backfill for existing rows.
- Migration rollback behavior is understood before deployment.

### Edge-case confirmation gate

- [ ] Applicable boundary values were tested.
- [ ] Existing and concurrently-created duplicate data was checked.
- [ ] Missing, inactive, and soft-deleted relationship cases were tested.
- [ ] Unique-index and concurrent-request behavior was considered.
- [ ] Query-filter behavior was verified.
- [ ] Read queries use projection and avoid N+1/O(n) database round trips.
- [ ] Authentication and wrong-role behavior were tested.
- [ ] Audit and partial-failure behavior were verified.
- [ ] Existing-data migration and repeated-seed behavior were checked.
- [ ] Every untested or non-applicable case has a recorded reason.

## 11. Definition of done

A feature is complete only when:

- [ ] The mandatory impact table has been answered.
- [ ] The mandatory edge-case confirmation gate has been answered.
- [ ] A repository search confirmed affected services and consumers.
- [ ] Requests, validators, responses, mappings, service, controller, and Swagger
      documentation are complete where applicable.
- [ ] Foreign-key existence checks are implemented.
- [ ] Delete relationships and dependency behavior are explicitly confirmed.
- [ ] The migration was reviewed and has no unrelated changes.
- [ ] The solution builds without errors or warnings.
- [ ] Formatting checks pass.
- [ ] Authorized, unauthorized, success, validation, not-found, and conflict
      scenarios have been verified.
- [ ] Documentation and seed data are updated when behavior changes.

When reporting completion, explicitly state:

1. Which other services or features were checked.
2. Whether the change affects any shared contract.
3. Which foreign keys were found.
4. The chosen delete behavior and how it was tested.
5. Which edge cases were tested and which were not applicable.
