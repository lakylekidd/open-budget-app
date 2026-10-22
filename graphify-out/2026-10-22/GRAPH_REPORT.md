# Graph Report - ynab-clone  (2026-10-21)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 240 nodes · 365 edges · 12 communities (10 shown, 2 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 8 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `61b54910`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- App
- BudgetEntity
- CategoryEntity
- OpenBudget.Api
- package.json
- http
- compilerOptions
- http
- .UpdateMonthAsync
- OpenBudget.Api

## God Nodes (most connected - your core abstractions)
1. `App()` - 24 edges
2. `BudgetEntity` - 20 edges
3. `CategoryEntity` - 20 edges
4. `compilerOptions` - 16 edges
5. `CategoryGroupEntity` - 15 edges
6. `BudgetDbContext` - 14 edges
7. `BudgetService` - 13 edges
8. `BudgetPlanEntity` - 12 edges
9. `OpenBudget.Api` - 8 edges
10. `BudgetDto` - 7 edges

## Surprising Connections (you probably didn't know these)
- `BudgetService` --references--> `BudgetDbContext`  [EXTRACTED]
  src/OpenBudget.Api/BudgetService.cs → src/OpenBudget.Api/BudgetDbContext.cs
- `OpenBudget.AppHost` --references--> `net10.0`  [EXTRACTED]
  src/OpenBudget.AppHost/OpenBudget.AppHost.csproj → src/OpenBudget.Api/OpenBudget.Api.csproj

## Import Cycles
- None detected.

## Communities (12 total, 2 thin omitted)

### Community 0 - "App"
Cohesion: 0.07
Nodes (19): react, react-dom, amountFromInput(), App(), addFunds(), applyMove(), saveEdit(), updateCategory() (+11 more)

### Community 1 - "BudgetEntity"
Cohesion: 0.10
Nodes (18): BudgetPlanDto, BudgetPlanUpdateResult, BudgetEntity, BudgetId, FundsAdded, Groups, Month, Note (+10 more)

### Community 2 - "CategoryEntity"
Cohesion: 0.06
Nodes (27): BudgetDbContext, BudgetPlans, Budgets, Categories, CategoryGroups, CategoryEntity, Activity, Assigned (+19 more)

### Community 3 - "OpenBudget.Api"
Cohesion: 0.13
Nodes (5): OpenBudget.Api, OpenBudget.Api.Migrations, InitialCreate, BudgetCore, NamedBudget

### Community 4 - "package.json"
Cohesion: 0.09
Nodes (22): @types/react, @types/react-dom, typescript, vite, @vitejs/plugin-react, dependencies, react, react-dom (+14 more)

### Community 5 - "http"
Cohesion: 0.13
Nodes (18): ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables (+10 more)

### Community 6 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+9 more)

### Community 7 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 8 - ".UpdateMonthAsync"
Cohesion: 0.30
Nodes (8): BudgetDto, BudgetPlanUpdateDto, BudgetUpdateDto, BudgetUpdateResult, CategoryDto, CategoryGroupDto, CategoryGroupUpdateDto, CategoryUpdateDto

### Community 9 - "OpenBudget.Api"
Cohesion: 0.24
Nodes (9): Aspire.Hosting.JavaScript (13.6.2), Aspire.Hosting.PostgreSQL (13.6.2), Microsoft.EntityFrameworkCore.Design (10.0.13), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Aspire.AppHost.Sdk/13.6.2, Microsoft.NET.Sdk.Web, OpenBudget.Api, net10.0 (+1 more)

## Knowledge Gaps
- **45 isolated node(s):** `BudgetId`, `FundsAdded`, `Groups`, `Month`, `Note` (+40 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 124 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **2 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CategoryEntity` connect `CategoryEntity` to `.UpdateMonthAsync`, `BudgetEntity`, `OpenBudget.Api`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `CategoryEntity` (e.g. with `.CreateNextMonth()` and `.CreateStarterMonth()`) actually correct?**
  _`CategoryEntity` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `BudgetId`, `FundsAdded`, `Groups` to the rest of the system?**
  _45 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App` be split into smaller, more focused modules?**
  _Cohesion score 0.0746031746031746 - nodes in this community are weakly interconnected._
- **Why does `OpenBudget.Api` connect `OpenBudget.Api` to `.UpdateMonthAsync`?**
  _High betweenness centrality (0.048) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `CategoryGroupEntity` (e.g. with `.CreateNextMonth()` and `.CreateStarterMonth()`) actually correct?**
  _`CategoryGroupEntity` has 3 INFERRED edges - model-reasoned connections that need verification._
- **Should `BudgetEntity` be split into smaller, more focused modules?**
  _Cohesion score 0.10160427807486631 - nodes in this community are weakly interconnected._