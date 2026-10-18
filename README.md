# QueryLens — Phases 1-9 (Core + EF6 + EF Core + ADO.NET + Query Store + Execution Plans + Regression Baselines + CLI + HTML Report)

QueryLens is a cross-version .NET database performance analyzer.

- **Phase 1** delivered the framework-agnostic **Core** engine.
- **Phase 2** added the **EF6 adapter**.
- **Phase 3** added the **EF Core adapter**, and moved shared command-capture
  logic into Core (`DbCommandExecutionBuilder`).
- **Phase 4** added the **ADO.NET adapter** (also giving Dapper first-class support).
- **Phase 5** added **SQL Server Query Store integration** - reading
  `sys.query_store_*` and correlating it with application-captured queries.
- **Phase 6** added **execution plan analysis**: parsing the
  plan XML Query Store already hands back (captured but unparsed since Phase
  5) and running deterministic, evidence-based rules over it - Table Scan,
  Key Lookup, Sort, Hash Match, Nested Loops, Spool, and parallelism
  detection, plus surfacing SQL Server's own missing-index suggestions.
- **Phase 7** added **regression baselines**: snapshotting metrics and detecting
  duration, CPU, or read performance regressions across releases.
- **Phase 8** added the **CLI** (`src/QueryLens.Cli`): `querylens analyze`,
  `report`, `compare`, and `verify` commands.
- **Phase 9** (this delivery) adds **self-contained HTML reporting**: an offline,
  single-file HTML performance report with embedded styling, scorecard, diagnostic
  callouts (regressions, N+1, query explosion), top query rankings, and full query details.

## What's in this phase

```
src/QueryLens.SqlServer/ExecutionPlans/
├── PlanOperator.cs, ExecutionPlan.cs, MissingIndexSuggestion.cs   Data model
├── ExecutionPlanParseException.cs
├── IExecutionPlanParser.cs / ExecutionPlanParser.cs               ShowPlan XML -> ExecutionPlan
└── Rules/
    ├── LargeScanRule.cs           "LARGESCAN001" - Table/Clustered Index/Index Scan over many rows
    ├── KeyLookupRule.cs           "KEYLOOKUP001" - Key/RID Lookup running many times
    ├── ExpensiveSortRule.cs       "EXPENSIVESORT001"
    ├── ExpensiveHashMatchRule.cs  "EXPENSIVEHASH001"
    ├── NestedLoopsRule.cs         "NESTEDLOOPSLARGE001" - large outer (driving) input
    ├── SpoolRule.cs               "SPOOL001"
    ├── ParallelismRule.cs         "PARALLELISM001" - informational, not a warning
    ├── RowCountDiscrepancyRule.cs "ROWCOUNTDISCREPANCY001" - estimated vs actual, when actual is available
    ├── MissingIndexRule.cs        "MISSINGINDEX001" - relays SQL Server's own suggestion
    └── ExecutionPlanRuleEngine.cs

tests/QueryLens.SqlServer.Tests/ExecutionPlans/   Full xUnit suite - see Testing notes
```

`QueryLensSqlServer.AnalyzeWithQueryStoreAsync` (Phase 5's orchestrator) now
parses and analyzes each correlated query's plan automatically - no new
entry point needed. `CorrelatedQuery` gained a `ParsedExecutionPlan`
property so callers can inspect the operator tree directly.

### Two small Core additions

- `ThresholdOptions` gained six new thresholds: `LargeScanRowThreshold`,
  `KeyLookupRowThreshold`, `ExpensiveSortRowThreshold`,
  `ExpensiveHashRowThreshold`, `SpoolRowThreshold`, and
  `RowCountDiscrepancyRatio`.
- `PerformanceScorer` gained category mappings for the nine new rule IDs -
  scan/lookup/missing-index findings land in "Logical Reads" (they're
  fundamentally about reads), sort/hash/nested-loops/spool findings land in
  "CPU" (compute-bound operators). Parallelism and row-count-discrepancy
  findings fall through to "Other" by design - neither maps cleanly to an
  existing category. No `CategoryOrder` changes were needed - Phase 1's
  scorer was explicitly built with this kind of extension in mind.

### A design principle worth restating: advisory only, never Critical

Every rule in this module caps out at `Severity.Warning` - never `Critical`.
That's deliberate, not an oversight: a slow query being critical is an
unambiguous fact (duration is duration), but "this scan might indicate a
missing index" is inherently a judgment call that needs a human to weigh
against the actual workload. `MissingIndexRule` goes a step further by
relaying SQL Server's *own* suggestion rather than inventing one - it's
still capped at Warning, and its recommendation explicitly says "review it
like any missing-index hint, verify against actual workload."

## Quick start

Nothing new to call - if you're already using Phase 5:

```csharp
var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection);

foreach (var query in report.CorrelatedQueries.Where(q => q.ParsedExecutionPlan != null))
{
    foreach (var op in query.ParsedExecutionPlan.AllOperators)
    {
        Console.WriteLine($"  {op.PhysicalOp}: ~{op.EffectiveEstimatedRows:N0} rows");
    }
}

// report.Findings already includes plan-based findings alongside everything else:
foreach (var finding in report.Findings.Where(f => f.RuleId.StartsWith("LARGESCAN") || f.RuleId.StartsWith("MISSINGINDEX")))
{
    Console.WriteLine($"[{finding.Severity}] {finding.Title}: {finding.Description}");
}
```

Turn it off entirely with `options.Analysis.ExecutionPlan = false` (Phase 1
defined this toggle; this is the first phase that uses it).

## Design notes worth knowing

- **Elements are matched by local name, not exact namespace/version.**
  ShowPlan XML has had version bumps over the years (`Version="1.0"`
  through `"1.539"` and beyond); matching on `e.Name.LocalName == "RelOp"`
  rather than a fully-qualified `XName` is more resilient to that than it
  might look.
- **Child operators are found by searching descendants and stopping at the
  first nested `RelOp`**, rather than assuming a specific wrapper element
  name (`<Hash>`, `<NestedLoops>`, `<Sort>`, ...) - this sidesteps needing to
  enumerate every wrapper element SQL Server uses across operator types.
- **`EstimateRows` is per-execution; `EstimateExecutions` matters.** A Key
  Lookup that returns 1 row but runs 15,000 times (once per outer row in a
  Nested Loops) is a much bigger deal than "1 row" suggests.
  `PlanOperator.EffectiveEstimatedRows` (`EstimateRows * EstimateExecutions`)
  is what every threshold rule actually compares against.
- **`RowCountDiscrepancyRule` will usually find nothing to evaluate** when
  fed Query Store plans specifically, since Query Store captures
  compile-time *estimated* plans, not *actual* ones with runtime statistics
  - there's no `RunTimeInformation` to compare against. The rule is ready
  for when actual-plan XML is available (e.g. a future capture path using
  `SET STATISTICS XML ON`); it just won't fire on typical Query Store data
  today. This is documented in the rule itself, not a bug.
- **A plan that fails to parse doesn't break the rest of the report** - 
  `AnalyzeWithQueryStoreAsync` catches `ExecutionPlanParseException` per
  query and just skips plan-level analysis for that one.

## Testing notes

Unlike Query Store's SQL text (Phase 5's one genuinely unverified piece),
**XML parsing has no SQL Server dependency at all** - it's `System.Xml.Linq`
over a string. That made this phase fully buildable and testable end to end
in this sandbox:

- The parser, all nine rules, and the rule engine were validated against
  hand-written ShowPlan XML fixtures in a throwaway .NET 8 harness, then
  re-validated a second time directly against the final, shipped
  `QueryLens.SqlServer` project (after being split into its final
  one-rule-per-file layout) - 27 assertions covering operator parsing, the
  "unknown wrapper element" child-RelOp search, missing-index parsing,
  scorer category mappings, and the full Query Store + execution-plan
  orchestration end to end, all passing against the actual code being
  delivered, not a copy of it.
- **What's genuinely unverified**: whether these hand-written XML fixtures
  are a faithful match for real SQL Server ShowPlan XML. They're written
  carefully against Microsoft's documented, stable-since-SQL-Server-2016
  schema (element names, attributes like `PhysicalOp`/`EstimateRows`/
  `EstimateExecutions`, the `MissingIndexGroup` structure), but this project
  had no live SQL Server to capture a real plan from and compare against.
  If a real plan trips up this parser, it's most likely an attribute name or
  a wrapper-element shape these fixtures didn't happen to exercise - the
  defensive design choices above (local-name matching, wrapper-agnostic
  child search) exist specifically to reduce that risk, but can't eliminate
  it without a real server to validate against.
- The `QueryStoreDemo` sample now exercises this automatically (via
  `AnalyzeWithQueryStoreAsync`) but, like the rest of that sample, needs a
  real SQL Server/LocalDB instance and wasn't run.

## Building and testing

```bash
dotnet build QueryLens.sln
dotnet test
```

`QueryLens.Core`, `QueryLens.AdoNet`, and `QueryLens.SqlServer` all have zero
external dependencies and build offline. Everything else needs a normal
NuGet restore.

## Known limitations (by design, for this phase)

- No automatic index creation or DDL generation - every finding is advisory
  text, per the project's explicit design principle.
- `RowCountDiscrepancyRule` needs an actual (not estimated) plan to do
  anything useful - see above.
- The "outer input" heuristic for `NestedLoopsRule` (first child = outer)
  is a strong convention in ShowPlan XML but not something this project
  could confirm against a real plan.
- No regression baselines comparing plan shape/cost across releases yet
  (that's Phase 10, alongside Query Store's own regression comparison).

## Roadmap (per the original phased plan)

1. ✅ **Core** — models, fingerprinting, aggregation, rules, scoring
2. ✅ **EF6 adapter**
3. ✅ **EF Core adapter**
4. ✅ **ADO.NET adapter** — connection/command decorator, Dapper support
5. ✅ **SQL Server Query Store integration** — reader, correlation, CPU/reads rules
6. ✅ **Execution plan parser** — ShowPlan XML parsing, 9 advisory rules, missing-index suggestions
7. ✅ **Advanced rules (regression baselines)** — baseline snapshots & comparison
8. ✅ **CLI** (`querylens analyze`, `report`, `compare`, `verify`)
9. ✅ **HTML report** — self-contained, offline HTML report renderer
10. Regression baselines / CI-CD gate (`querylens verify`)
11. Dashboard
