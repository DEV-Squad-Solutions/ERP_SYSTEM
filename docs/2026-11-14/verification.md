# Verification — cash voucher edit lock

Verified on 2026-11-14 against the current backend and frontend working trees.

| Check | Result |
| --- | --- |
| Backend full suite | 1,408 passed; 4 existing failures; 1,412 total |
| Related backend voucher, exchange-rate cascade, source-filter, validator and standalone-trip tests in that run | 218 passed; 0 failed |
| Frontend full Node suite | 66 passed; 0 failed |
| Frontend TypeScript without emitting files | Passed |
| Frontend production build to an isolated temporary directory | Passed; existing bundle-size advisory remains |
| Scoped diff review | No material unresolved source finding |

The full-suite failures match the previously established baseline:

- `BusinessPartnerContainerStoreServiceTests.GetAll_FiltersBySpecialFlag`: SQLite fixture lacks `JournalEntryLines`.
- `BusinessPartnerContainerStoreServiceTests.GetAll_ReturnsOnlyActiveContainersAssignedToPartnerStore`: same fixture issue.
- `EmployeeMovementServiceTests.GetAllAsync_WithTypeAndCurrencyFilter_ShouldWork`: fiscal-year name expectation differs from the fixture.
- `StockTransferServiceTests.Create_InOpenNonCurrentFiscalYear_CostsTheTransferInItsOwnYear`: fixture setup inserts source cost outside its current year.

Final commands:

```powershell
dotnet test tests/MiniErp.Tests/MiniErp.Tests.csproj --artifacts-path .artifacts/cash-voucher-lock-review --no-restore --results-directory .artifacts/cash-voucher-lock-review/results --logger 'trx;LogFileName=cash-voucher-lock-full-final.trx' --verbosity minimal
```

From the frontend repository:

```powershell
node --test --test-concurrency=1 tests/*.test.mjs
tsc -p tsconfig.app.json --noEmit --incremental false
vite build --outDir <unique-temporary-directory>
```

The frontend SSR cleanup failure was traced to an unused client dependency scan writing temporary files during teardown. Disabling that scan in the owning SSR test harness fixed the repeated failure; product dependency optimization was not changed.

No live database migration, application startup, or deployment was performed. Stale-session tests exercise transaction rollback and saved revisions using SQLite. Acceptance with simultaneous SQL Server sessions and the [historical voucher inventory](cash-voucher-legacy-inventory.sql) remains for the target environment before rollout.
