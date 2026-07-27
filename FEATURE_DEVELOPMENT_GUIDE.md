# MiniErp Feature Development Guide

## Paginated `GetAll` filtering

Every paginated `GetAll` endpoint should expose a typed, optional filter
request, following the Invoice pattern. Filters should be applied with `AND`
semantics after tenant and soft-delete scoping, while preserving deterministic
ordering and the existing pagination metadata.

Filter contracts should contain only supported, resource-specific fields and
should have FluentValidation validators with Arabic validation messages for
length, ID, enum, and date-range rules. The controller should bind the filter
contract from the query string, the service should apply it to the database
query, and Swagger should list the available query fields and their validation
rules. Adding filters must not require a database migration.
