---
type: "query"
date: "2026-07-31T18:12:33.734900+00:00"
question: "Cross-project MiniErp feature flow impact analysis"
contributor: "graphify"
outcome: "useful"
source_nodes: ["InvoicePage.tsx", "api.ts", "InvoicesController", "IInvoiceService", "InvoiceService", "ApplicationDbContext", "AuthController", "AuthenticationService"]
---

# Q: Cross-project MiniErp feature flow impact analysis

## Answer

Expanded graph terms: invoice page api controller request response service entity database; authentication authorization token company. Merged graph has backend and client repos, but zero explicit cross-repo AST edges. Deterministic endpoint-literal matching maps frontend callers through api.ts to same-named backend controllers, DTOs, application services, domain entities, and ApplicationDbContext. Graphify is visibility only and does not synchronize code.

## Outcome

- Signal: useful

## Source Nodes

- InvoicePage.tsx
- api.ts
- InvoicesController
- IInvoiceService
- InvoiceService
- ApplicationDbContext
- AuthController
- AuthenticationService