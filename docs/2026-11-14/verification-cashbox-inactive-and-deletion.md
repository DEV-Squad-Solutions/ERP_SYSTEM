# Verification — cashbox deactivation/deletion and containers-only invoices

Verified on 2026-11-14 against the current working trees.

| Check | Result |
| --- | --- |
| Backend full suite | 1,433 passed, 4 existing failures, 1,437 total |
| New cashbox activity/deletion tests within that run | 25 passed, 0 failed |
| Frontend full Node suite | 71 passed, 0 failed |
| Frontend TypeScript without emitting files | Passed |
| Frontend production build to an isolated temporary directory | Passed; existing bundle-size advisory remains |
| Scoped source/diff checks | Passed |

Regression checks cover positive/negative balances, native versus base-only carry, source-less manual journal cash, excluded/deleted/reversed rows, fiscal-year isolation, missing current year, stale tracked state, revisions and permissions. They verify that status-only edits leave the opening journal unchanged and that financial opening edits still synchronize it.

Deletion checks verify that soft-deleted voucher history no longer blocks master deletion, including already-tracked historical children. Live vouchers in any fiscal year, drafts, transfers and journal lines still block it. Own opening journal deletion remains atomic; a closed-year rejection rolls back the cashbox change. Audit information and historical deleted records are preserved.

Existing containers-only invoice tests passed for sales, purchases and both return types: empty item lines with saved container quantities create container movements without item movements. The frontend content switch/save transport tests also passed. This workflow required no new invoice implementation.

The four full-suite failures match the established baseline:

- `BusinessPartnerContainerStoreServiceTests.GetAll_FiltersBySpecialFlag`: SQLite fixture lacks `JournalEntryLines`.
- `BusinessPartnerContainerStoreServiceTests.GetAll_ReturnsOnlyActiveContainersAssignedToPartnerStore`: same fixture issue.
- `EmployeeMovementServiceTests.GetAllAsync_WithTypeAndCurrencyFilter_ShouldWork`: fiscal-year name expectation differs from the fixture.
- `StockTransferServiceTests.Create_InOpenNonCurrentFiscalYear_CostsTheTransferInItsOwnYear`: source-cost fixture date falls outside the current year.

Final commands:

```powershell
dotnet test tests/MiniErp.Tests/MiniErp.Tests.csproj --artifacts-path .artifacts/cashbox-inactive --no-restore --verbosity minimal --logger 'trx;LogFileName=cashbox-inactive-full.trx' --results-directory .artifacts/cashbox-inactive/results
```

From the frontend repository:

```powershell
node --test --test-concurrency=1 tests/*.test.mjs
tsc -p tsconfig.app.json --noEmit --incremental false
vite build --outDir <unique-temporary-directory>
```

No live database, migration, deployment, commit or push was performed. Automated database checks use isolated SQLite fixtures. Simultaneous SQL Server transaction and deployed UI acceptance remain for the target environment.
