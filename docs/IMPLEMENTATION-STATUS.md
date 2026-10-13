# QueryLens — Implementation Status & Roadmap

This document is the single reference for what QueryLens actually contains
today (Phases 1-6, delivered) and what's left to build (Phases 7-11, not
started). It consolidates and supersedes the phase-by-phase notes scattered
across the root `README.md` and `docs/user-guide.md` — read this first if
you want the full picture; read those for narrower, task-focused guidance.

## At a glance

| # | Phase | Status | One-line summary |
|---|---|---|---|
| 1 | Core engine | ✅ Delivered | Framework-agnostic models, fingerprinting, aggregation, rules, scoring |
| 2 | EF6 adapter | ✅ Delivered | Interceptor + registration API for legacy .NET Framework apps |
| 3 | EF Core adapter | ✅ Delivered | Interceptor + DI registration for modern .NET apps |
| 4 | ADO.NET adapter | ✅ Delivered | Connection/command decorator; gives Dapper support for free |
| 5 | SQL Server Query Store | ✅ Delivered | Reads `sys.query_store_*`, correlates with app-captured queries |
| 6 | Execution plan analysis | ✅ Delivered | Parses ShowPlan XML, 9 advisory rules, missing-index suggestions |
| 7 | Advanced rules / regression | ❌ Not started | Baseline storage + comparison, regression detection |
| 8 | CLI | ❌ Not started | `querylens analyze/report/compare/verify` |
| 9 | HTML report | ❌ Not started | Static, server-less HTML report renderer |
| 10 | Regression baselines / CI-CD gate | ❌ Not started | Build-breaking performance gate for pipelines |
| 11 | Dashboard | ❌ Not started | Web UI over everything above |

Project layout (unchanged shape since Phase 1, phases only add to it):

```
QueryLens/
├── src/            QueryLens.Core, .EF6, .EFCore, .AdoNet, .SqlServer
├── tests/           One xUnit project per src project, same name + .Tests
├── samples/         One illustrative sample per adapter
└── docs/            user-guide.md (task-focused), this file (status-focused)
```

---

## Part 1 — What's implemented

### Phase 1: `QueryLens.Core`

The framework-agnostic engine every other project depends on. Zero external
package dependencies (targets `netstandard2.0`); works unmodified from a
.NET Framework 4.6.1+ app or any modern .NET app.

**Models** (`src/QueryLens.Core/Models/`)
- `QueryExecution` — one captured command: `Sql`, `NormalizedSql`,
  `RepresentativeSql`, `QueryHash`, `CommandType`, `Provider`, `Orm`,
  `Framework`, `Database`, `Server`, `StartTimeUtc`, `Duration`,
  `CpuTimeMs`/`LogicalReads`/`PhysicalReads`/`RowsReturned`/`RowsAffected`
  (all nullable — populated only when an adapter can supply them),
  `RequestId`, `CorrelationId`, `OperationName`, `Source`,
  `ApplicationName`, `Parameters`.
- `QueryParameter` — `Name`, `DbType`, `Size`. **No `Value` property exists
  on this type at all** — a compile-time guarantee against ever capturing a
  parameter value, not just a runtime check.
- `QueryStatistics` — aggregated per-query-hash rollup: count, min/max/avg/
  total duration, P50/P90/P95/P99, and average CPU/logical reads/physical
  reads/rows-returned (nullable, populated only if any underlying execution
  had them).
- `Finding` — one rule's output: `RuleId`, `Severity`, `Title`,
  `Description`, `QueryHash`, `Confidence` (0.0-1.0), `Recommendation`,
  `Evidence` (a string dictionary).
- `Severity` — `Info` / `Warning` / `Critical`.
- `PerformanceScoreResult` / `ScoreComponent` — the 0-100 score plus its
  full per-category breakdown; never returned as a bare number.

**Configuration** (`src/QueryLens.Core/Configuration/QueryLensOptions.cs`) —
see the full reference table in Part 2 below.

**Fingerprinting** (`src/QueryLens.Core/Fingerprinting/`)
- `SqlQueryFingerprinter` — regex-based normalizer (not a full SQL parser):
  strips `--`/`/* */` comments, collapses whitespace, replaces string/
  numeric/hex literals with `?`, collapses variable-length `IN (...)` lists
  to a single placeholder, and hashes the result (12 hex chars). Two
  structurally identical queries with different literals fingerprint
  identically; structurally different queries don't.

**Aggregation** (`src/QueryLens.Core/Aggregation/`)
- `QueryAggregator` — thread-safe, per-query-hash rollup using reservoir
  sampling (default 5,000 samples/query) so memory stays bounded regardless
  of how long the process runs.
- `PercentileCalculator` — linear-interpolation percentiles (the Excel/NumPy
  "linear" method) over a sorted sample set.

**Rules** (`src/QueryLens.Core/Rules/`) — `RuleEngine.CreateDefault()` wires up:
| RuleId | Class | Fires when |
|---|---|---|
| `SLOWQUERY001` | `SlowQueryRule` | avg duration ≥ `SlowQueryMs`, < `CriticalQueryMs` |
| `CRITICALQUERY001` | `CriticalQueryRule` | avg duration ≥ `CriticalQueryMs` |
| `LARGERESULT001` | `LargeResultSetRule` | max rows returned ≥ `MaxRowsReturned` |
| `NPLUSONE001` | `NPlusOneRule` | same query repeats ≥ `NPlusOneMinExecutions` times in one request, confidence ≥ `NPlusOneMinConfidence` (confidence curve: `1 - 2/(count+2)`) |
| `QUERYEXPLOSION001` | `QueryExplosionRule` | total queries in one request ≥ `MaxQueriesPerRequestWarning` (Critical at `MaxQueriesPerRequestCritical`) |

**Scoring** (`src/QueryLens.Core/Scoring/PerformanceScorer.cs`) — starts at
100, deducts a capped-per-category penalty (`Critical`=10, `Warning`=4,
`Info`=1, each scaled by the finding's `Confidence`). Category cap: 25
points max per category, so no single noisy category dominates. See Part 2
for the full RuleId→category table (now spanning all six phases).

**Pipeline** (`src/QueryLens.Core/Pipeline/QueryLensPipeline.cs`) — the one
class every adapter ultimately calls: `CaptureAsync(QueryExecution)` to
fingerprint+aggregate one execution, `Analyze()` to run rules+scoring over
everything captured so far. Exposes `.Options` so adapters can read capture
settings (e.g. whether parameter capture is on).

**Interfaces** — `IQueryMetricsSink` (storage abstraction; no concrete
implementations ship yet — see Phase 8/9).

**Validation**: fully validated. Temporarily retargeted to `net8.0` (this
project has zero external dependencies) and exercised with ~90 assertions
covering every component above.

---

### Phase 2: `QueryLens.EF6`

For legacy .NET Framework + EF6 apps. Targets `net472;net48`.

- `QueryLensEf6Interceptor` (`Interceptors/`) — implements EF6's
  `DbCommandInterceptor`. Times each command with a `Stopwatch` (EF6 doesn't
  hand you a duration the way EF Core does), keyed by `DbCommand` reference
  in a `ConcurrentDictionary`. Skips failed commands (doesn't pollute
  aggregates with failure noise).
- `QueryLensEf6` (`Extensions/`) — public entry point:
  `Register(options, requestIdProvider)` (creates a pipeline) or
  `Register(existingPipeline, ...)`. `SetCurrentRequestId`/
  `GetCurrentRequestId` wrap `System.Runtime.Remoting.Messaging.CallContext`
  for ambient request correlation across async calls.
- Delegates all field-extraction to Core's `DbCommandExecutionBuilder`
  (moved there in Phase 3) with `provider`/`orm` = `"EF6"`.
- Sample: `samples/NetFrameworkEf6/` — console app, LocalDB connection
  string, deliberate N+1, prints the report.

**Validation**: the EF6-independent logic (now living in Core, see Phase 3)
is fully validated. `QueryLensEf6Interceptor` itself — the part that touches
real EF6 types (`DbCommandInterceptionContext<T>`) — was **not** compiled or
run anywhere; this sandbox has no access to nuget.org and `EntityFramework`
is NuGet-only. Written carefully against EF6's long-stable interception API
and kept deliberately thin to minimize what rests on that assumption.

---

### Phase 3: `QueryLens.EFCore`

For modern .NET + EF Core apps. Targets `net8.0`.

- `QueryLensEfCoreInterceptor` (`Interceptors/`) — implements EF Core's
  `DbCommandInterceptor`. Simpler than EF6's: EF Core's
  `CommandExecutedEventData.Duration` is computed by EF Core itself, no
  manual timing needed. Both sync (`ReaderExecuted`, etc.) and async
  (`ReaderExecutedAsync`, etc.) hooks are overridden, since which pair fires
  depends on whether calling code used sync or async EF Core APIs.
- `QueryLensServiceCollectionExtensions.AddQueryLens(...)` — DI registration
  (`IServiceCollection` extension).
- `QueryLensDbContextOptionsExtensions.AddQueryLensInterceptor(sp)` — wires
  the DI-registered interceptor into a `DbContextOptionsBuilder`.
- `QueryLensEfCore` — non-DI entry point (`CreatePipelineAndInterceptor`,
  `CreateInterceptor`) plus `AsyncLocal<string>`-based request correlation
  (the modern equivalent of EF6's `CallContext`).
- **Core addition this phase**: `DbCommandExecutionBuilder` moved from
  `QueryLens.EF6` into `QueryLens.Core.Capture` — both adapters (and Phase
  4's) now share one implementation.
- Sample: `samples/ModernEfCore/` — ASP.NET Core 8 minimal API + Sqlite
  (self-contained, no external DB needed), middleware bridging
  `HttpContext.TraceIdentifier` into request correlation, `/customers`
  (deliberate N+1) and `/querylens/report` endpoints.

**Validation**: same split as EF6 — the shared `DbCommandExecutionBuilder`
is fully validated; `QueryLensEfCoreInterceptor` (touches real
`Microsoft.EntityFrameworkCore.Diagnostics` types) was not compiled here,
though it's thinner and therefore lower-risk than EF6's.

---

### Phase 4: `QueryLens.AdoNet`

For raw ADO.NET and (transparently) Dapper, on either .NET Framework or
modern .NET. Targets `netstandard2.0`, zero external dependencies.

- `QueryLensDbConnection` (`Capture/`) — wraps a real `DbConnection`;
  `CreateDbCommand()` returns a `QueryLensDbCommand`; `BeginDbTransaction`
  returns a `QueryLensDbTransaction`.
- `QueryLensDbCommand` — wraps a real `DbCommand`. Times
  `ExecuteReader`/`ExecuteNonQuery`/`ExecuteScalar` **and** their `Async`
  counterparts; `Parameters` returns the real underlying
  `DbParameterCollection` directly (never copied/wrapped).
- `QueryLensDbTransaction` — wraps a real `DbTransaction` so
  `command.Transaction = wrappedTx` correctly unwraps back to the real one.
- `QueryLensAdoNet` (`Extensions/`) — `Wrap(connection, pipeline, ...)`,
  `CreatePipelineAndWrap(...)`, `SetCurrentRequestId`/`GetCurrentRequestId`
  (`AsyncLocal`-based).
- **Because `QueryLensDbConnection` is still a real `DbConnection`/
  `IDbConnection`, Dapper works against it with zero changes on Dapper's
  side** — this is the adapter that makes Dapper first-class.
- Every wrapper type exposes an `Inner*` escape hatch (`InnerConnection`,
  `InnerCommand`, `InnerTransaction`) for provider-specific needs the
  wrapper doesn't surface (e.g. casting to `SqlConnection` for
  `SqlBulkCopy`).
- Sample: `samples/NetFrameworkAdoNet/` — .NET Framework console app showing
  raw ADO.NET *and* Dapper through the same wrapped connection.

**Validation**: **fully validated** — the only adapter where this is true
without caveats. `DbConnection`/`DbCommand`/`DbTransaction`/`DbDataReader`
are BCL types, not NuGet packages, so the entire adapter (not a slice of it)
was compiled and exercised against real base-class overrides: property
forwarding, sync+async execution, transaction commit forwarding, N+1
detection through the wrapper, and — proved via a recording sink, not just
asserted — that a real parameter value goes in and only name/type/size
comes back out.

---

### Phase 5: `QueryLens.SqlServer` (Query Store)

Reads SQL Server's Query Store and correlates it with whatever the
application-side adapters captured. Targets `netstandard2.0`, zero external
dependencies (`QueryStoreReader` takes a plain `DbConnection`, not a
SqlClient-specific type).

- `QueryStoreEntry` (`QueryStore/`) — one query's Query Store data:
  `QueryId`, `PlanId`, `QueryText`, `ExecutionCount`, `AvgDurationMs`,
  `AvgCpuMs`, `AvgLogicalReads`, `AvgPhysicalReads`, `AvgMemoryGrantKb`,
  `LastExecutionTimeUtc`, `ExecutionPlanXml` (raw, unparsed until Phase 6).
- `QueryStoreReader` — runs a weighted-average aggregation query across
  `sys.query_store_query`/`_query_text`/`_plan`/`_runtime_stats`/
  `_runtime_stats_interval`, converting Query Store's microsecond-based
  timings to milliseconds. Checks `sys.database_query_store_options` first
  and throws `QueryStoreNotEnabledException` (with the exact `ALTER
  DATABASE ... SET QUERY_STORE = ON` fix in the message) rather than
  silently returning nothing.
- `QueryStoreCorrelator` (`Correlation/`) — **the interesting design
  problem**: QueryLens's own `QueryHash` and SQL Server's internal
  `query_hash` are computed differently and must never be assumed to match.
  Instead of comparing hashes, it re-runs Query Store's captured SQL text
  through QueryLens's *own* `SqlQueryFingerprinter`, making the two sides
  directly comparable. Best-effort, not guaranteed — documented as such.
  `CorrelatedQuery` pairs an app-side `QueryStatistics` with a
  `QueryStoreEntry` (either side, or both, can be null).
- `HighCpuRule` (`HIGHCPU001`) / `HighLogicalReadsRule` (`HIGHREADS001`)
  (`Rules/`) — the first rules to populate the "CPU"/"Logical Reads" score
  categories that Phase 1's scorer reserved but nothing filled until now.
- `QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection, ...)`
  — orchestrates read → correlate → evaluate → re-score, returning a
  `QueryStoreReport` (`CorrelatedQueries`, `Findings`, `Score`).
- **Core additions this phase**: `ThresholdOptions.HighCpuMs`/
  `HighLogicalReads`; scorer category mappings for the two new RuleIds.
- Sample: `samples/QueryStoreDemo/` — EF Core capture + Query Store
  enrichment against SQL Server/LocalDB, prints both sides per query.

**Validation**: everything *except the literal T-SQL* is fully validated —
row-mapping, the enabled-check, the correlator (using the real, already-
validated fingerprinter), both rules, and the full orchestration (via an
injected fake `IQueryStoreReader`). **What's genuinely unverified**: the
actual `sys.query_store_*` joins in `QueryStoreReader.SelectSql`/
`EnabledCheckSql` — there is no offline substitute for a real SQL Server
with Query Store enabled, so this is the one place in the whole project
where "written to spec" and "proven" are different things.

---

### Phase 6: Execution plan analysis (`QueryLens.SqlServer/ExecutionPlans/`)

Parses the plan XML Query Store has captured since Phase 5 but never
analyzed. Same project, same zero-dependency `netstandard2.0` target — pure
`System.Xml.Linq` over a string, no SQL Server dependency at all.

- `PlanOperator` — one node (`RelOp`) in the plan tree: `PhysicalOp`,
  `LogicalOp`, `EstimateRows`, `EstimateExecutions`,
  `EffectiveEstimatedRows` (= `EstimateRows * EstimateExecutions` — the
  metric every threshold rule actually uses, since a Key Lookup returning 1
  row but running 15,000 times is a much bigger deal than "1 row" suggests),
  `EstimatedTotalSubtreeCost`, `ActualRows` (nullable — only present on
  *actual*, not estimated, plans), `Parallel`, `ObjectName`, `Children`.
- `MissingIndexSuggestion` — SQL Server's own suggestion, if the plan
  included one: `Table`, `Impact` (%), `EqualityColumns`,
  `InequalityColumns`, `IncludedColumns`.
- `ExecutionPlanParser` — matches elements by **local name**, not exact
  namespace/version (ShowPlan XML has had version bumps since 2005), and
  finds child operators by searching descendants and stopping at the first
  nested `RelOp` — sidesteps needing to know every operator-specific wrapper
  element name (`<Hash>`, `<NestedLoops>`, `<Sort>`, ...). Throws
  `ExecutionPlanParseException` on genuinely malformed XML; returns an empty
  plan (no throw) for null/empty input.
- Nine rules (`Rules/`), all capped at `Severity.Warning` (never `Critical`
  — a deliberate design principle: "this scan might indicate a missing
  index" is a judgment call in a way "this query took 1500ms" isn't):

  | RuleId | Class | Fires when |
  |---|---|---|
  | `LARGESCAN001` | `LargeScanRule` | Table/Clustered Index/Index Scan, effective rows ≥ `LargeScanRowThreshold` |
  | `KEYLOOKUP001` | `KeyLookupRule` | Key/RID Lookup, effective rows ≥ `KeyLookupRowThreshold` |
  | `EXPENSIVESORT001` | `ExpensiveSortRule` | Sort, effective rows ≥ `ExpensiveSortRowThreshold` |
  | `EXPENSIVEHASH001` | `ExpensiveHashMatchRule` | Hash Match, effective rows ≥ `ExpensiveHashRowThreshold` |
  | `NESTEDLOOPSLARGE001` | `NestedLoopsRule` | Nested Loops, outer (first) child's effective rows ≥ `LargeScanRowThreshold` |
  | `SPOOL001` | `SpoolRule` | any `*Spool` operator, effective rows ≥ `SpoolRowThreshold` |
  | `PARALLELISM001` | `ParallelismRule` | any operator has `Parallel=true` — **Info**, not Warning; purely informational |
  | `ROWCOUNTDISCREPANCY001` | `RowCountDiscrepancyRule` | `ActualRows` present and differs from `EstimateRows` by ≥ `RowCountDiscrepancyRatio`×. **Usually finds nothing on Query Store plans**, which are compile-time estimates without `RunTimeInformation` — ready for when an actual-plan source exists |
  | `MISSINGINDEX001` | `MissingIndexRule` | plan includes a `MissingIndexGroup`; relays SQL Server's own suggestion (Warning if Impact ≥ 50%, else Info) |

- `ExecutionPlanRuleEngine.CreateDefault()` wires up all nine; respects
  `options.Analysis.ExecutionPlan` (defined in Phase 1, unused until now).
- **Wired directly into Phase 5's orchestrator** — no new entry point.
  `AnalyzeWithQueryStoreAsync` now parses each correlated query's plan
  automatically, populates `CorrelatedQuery.ParsedExecutionPlan`, and merges
  plan findings into the same combined `Findings`/`Score`. A plan that fails
  to parse is skipped (doesn't break the rest of the report).
- **Core additions this phase**: six new `ThresholdOptions` fields (listed
  in the rule table above); scorer category mappings for all nine RuleIds
  (scan/lookup/missing-index → "Logical Reads"; sort/hash/nested-loops/
  spool → "CPU"; parallelism and row-count-discrepancy → "Other", since
  neither maps cleanly to an existing category).

**Validation**: fully validated — parser, all nine rules, the rule engine,
and the full Query-Store-plus-execution-plan orchestration were exercised
against hand-written ShowPlan XML fixtures, then re-validated a second time
directly against the final, shipped code after being split into its
one-rule-per-file layout. **What's genuinely unverified**: whether the
hand-written XML fixtures are a faithful match for real SQL Server output —
written carefully against Microsoft's stable, documented-since-2016 schema,
but there was no live server to capture a real plan from and compare
against.

---

## Part 2 — Cross-cutting systems (current full state)

### `QueryLensOptions` reference

```
QueryLensOptions
├── Enabled                             (bool, default true)
├── Capture
│   ├── Sql                             (bool, default true)
│   ├── Parameters                      (bool, default false)   ← gates QueryExecution.Parameters capture
│   └── Source                          (bool, default true)     — reserved; no adapter populates Source yet
├── Thresholds
│   ├── SlowQueryMs                     (500)
│   ├── CriticalQueryMs                 (1000)
│   ├── MaxQueriesPerRequestWarning     (20)
│   ├── MaxQueriesPerRequestCritical    (50)
│   ├── MaxRowsReturned                 (10000)
│   ├── NPlusOneMinExecutions           (3)
│   ├── NPlusOneMinConfidence           (0.6)
│   ├── HighCpuMs                       (200)                    ← Phase 5
│   ├── HighLogicalReads                (10000)                  ← Phase 5
│   ├── LargeScanRowThreshold           (10000)                  ← Phase 6
│   ├── KeyLookupRowThreshold           (1000)                   ← Phase 6
│   ├── ExpensiveSortRowThreshold       (10000)                  ← Phase 6
│   ├── ExpensiveHashRowThreshold       (10000)                  ← Phase 6
│   ├── SpoolRowThreshold               (10000)                  ← Phase 6
│   └── RowCountDiscrepancyRatio        (10.0)                   ← Phase 6
├── Analysis
│   ├── NPlusOne                        (bool, default true)
│   ├── QueryRegression                 (bool, default true)     — reserved; nothing implements regression detection yet (Phase 7)
│   ├── ExecutionPlan                   (bool, default true)     ← used starting Phase 6
│   └── QueryStore                      (bool, default true)     ← used starting Phase 5
└── Privacy
    ├── CaptureParameterValues          (bool, default false)    — reserved; no code path captures values regardless (QueryParameter has no Value field)
    └── RedactSensitiveData             (bool, default true)     — reserved; no redaction logic implemented yet
```

Two options exist today with **no code reading them yet**:
`Analysis.QueryRegression` and `Privacy.RedactSensitiveData`/
`CaptureParameterValues` are placeholders for Phase 7 and a future
parameter-redaction pass, respectively. They don't currently change any
behavior — this is a known, intentional gap (the options were defined early
so the schema wouldn't need to change shape later), not a bug.

### Scoring category map (full, current)

| Category | RuleIds mapped to it |
|---|---|
| Duration | `SLOWQUERY001`, `CRITICALQUERY001` |
| Logical Reads | `HIGHREADS001`, `LARGESCAN001`, `KEYLOOKUP001`, `MISSINGINDEX001` |
| CPU | `HIGHCPU001`, `EXPENSIVESORT001`, `EXPENSIVEHASH001`, `NESTEDLOOPSLARGE001`, `SPOOL001` |
| Query Count | `QUERYEXPLOSION001` |
| N+1 | `NPLUSONE001` |
| Result Size | `LARGERESULT001` |
| Regression | `QUERYREGRESSION001` (reserved — nothing produces this RuleId yet; Phase 7) |
| Other (catch-all) | any unrecognized RuleId, plus `PARALLELISM001` and `ROWCOUNTDISCREPANCY001` (informational/edge-case, deliberately not mapped) |

### Privacy model (consistent across every adapter)

- Parameter *values* are architecturally impossible to capture: `QueryParameter`
  has no `Value` property, full stop.
- Parameter *metadata* (name/type/size) is opt-in via `Capture.Parameters`
  (default off), respected identically by the EF6, EF Core, and ADO.NET
  adapters.
- No adapter interpolates literal values into captured SQL text beyond what
  the underlying ADO.NET/ORM layer already put in `CommandText`.

### Testing/validation philosophy, summarized across all six phases

| Phase | What's fully validated | What isn't (and why) |
|---|---|---|
| 1 (Core) | Everything — zero external deps, retargeted to `net8.0` and tested directly | — |
| 2 (EF6) | Shared capture logic (now in Core) | The EF6-interface glue itself — `EntityFramework` is NuGet-only, unreachable in this sandbox |
| 3 (EF Core) | Shared capture logic | The EF Core-interface glue — same NuGet-access constraint, though this glue is thinner (no manual timing) |
| 4 (ADO.NET) | Everything — `DbConnection`/`DbCommand`/etc. are BCL, not NuGet | — |
| 5 (Query Store) | Row mapping, correlation, rules, orchestration (via fakes) | The literal `sys.query_store_*` T-SQL — no offline substitute for a real SQL Server exists |
| 6 (Execution plans) | Everything — pure `System.Xml.Linq` | Whether hand-written XML fixtures match real ShowPlan XML output |

The pattern: **anything reachable through the BCL alone was fully validated
in-sandbox; anything requiring a NuGet-only package (EF6, EF Core) or a live
external service (SQL Server) could not be**, and every phase's README says
so explicitly rather than blending unverified code in with proven code.

---

## Part 3 — What's remaining

The original phased plan (unchanged since Phase 1) has five phases left.
None of this exists yet — no partial implementation, no stubs — this is a
from-scratch description of each based on the same source spec the first
six phases were built from.

### Phase 7: Advanced rules — regression baselines

**Goal**: detect when a query's performance has regressed compared to a
prior baseline (e.g. the last release), not just against a static
threshold.

**What it needs**:
- A baseline storage format — most naturally a JSON snapshot of
  `QueryStatistics` (+ optionally Query Store/execution-plan data) keyed by
  `QueryHash`, written by a new "save baseline" operation.
- A comparison engine: given a baseline snapshot and a current
  `PerformanceReport`/`QueryStoreReport`, compute percentage deltas in
  duration, CPU, logical reads, and execution count per query.
- A new rule (or small rule set) producing `QUERYREGRESSION001` findings —
  **the RuleId and scorer category ("Regression") already exist**, reserved
  since Phase 1 for exactly this.
- Configurable tolerance (e.g. "flag anything that regressed more than
  20%") — likely new `ThresholdOptions` fields, following the established
  pattern.

**What it can build on**: the entire `Finding`/`Severity`/scoring pipeline
already exists and already has a slot reserved for this. `QueryStatistics`
already has every metric needed for comparison. No Core changes should be
needed beyond possibly adding threshold fields — this phase is mostly new,
self-contained comparison logic plus a storage format decision.

**Validatable in this kind of sandbox?** Yes, fully — baseline comparison is
pure data transformation (two `QueryStatistics` snapshots in, a percentage
delta and a `Finding` out), no external dependency at all. This should be
one of the more thoroughly-provable phases, similar to Phase 4/6.

### Phase 8: CLI (`QueryLens.Cli`)

**Goal**: a command-line tool so results don't require writing C# to view —
`querylens analyze`, `report`, `compare`, `verify`, per the original spec.

**What it needs**:
- A new console app project (`src/QueryLens.Cli/`).
- A stable, versioned JSON schema for `PerformanceReport`/`QueryStoreReport`
  (the spec calls for `queryHash`, `normalizedSql`, `executionCount`,
  `averageDurationMs`, `p95DurationMs`, `logicalReads`, `findings[]` with
  `ruleId`/`severity`/`confidence` — all of this data already exists on the
  existing model types; this phase is largely "serialize what already
  exists to a stable JSON shape," not new analysis logic).
- `analyze` — run against a live app/connection and print/save a report.
- `report --format json|html` — render a previously-saved report (HTML
  rendering is Phase 9's concern; the CLI would just invoke it).
- `compare baseline.json current.json` — thin CLI wrapper over Phase 7's
  comparison engine.
- `verify --max-duration-regression 20 ...` — exit-code-based gate for
  CI/CD (0 = pass, non-zero = fail); this is effectively Phase 8 and Phase
  10 meeting at the same command.

**What it can build on**: every report type the CLI would serialize already
exists (`PerformanceReport`, `QueryStoreReport`). This phase is primarily
plumbing — argument parsing, JSON serialization, exit codes — rather than
new analytical logic.

**Validatable in this kind of sandbox?** Mostly yes — a console app with no
external package dependencies (or only `System.CommandLine`/similar, which
would need NuGet) can be built and its logic tested without any live
database, since it operates on already-captured report data. The main
question is whether the chosen argument-parsing library needs NuGet.

### Phase 9: HTML report

**Goal**: a single, self-contained HTML file summarizing a report — no
server needed, matching the spec's example format (score, top slow/CPU/IO
queries, N+1 candidates, query explosion, regressions, recommendations).

**What it needs**:
- An HTML template (could be a plain string-templated `.cshtml`-free
  approach, or a minimal templating library) that takes a
  `PerformanceReport`/`QueryStoreReport` and renders it.
- Decisions on visual presentation (a static page is explicitly required —
  "usable without requiring a server" per the spec) — likely inline CSS, no
  external asset dependencies, consistent with how the rest of this project
  avoids unnecessary dependencies.

**What it can build on**: identical input data to Phase 8's `report`
command — this phase is a rendering concern layered on top of already-
existing model types, not new analysis.

**Validatable in this kind of sandbox?** Yes, fully — HTML generation from
an in-memory object graph has no external dependency, similar to Phase 6's
XML parsing (just the reverse direction: object → markup instead of
markup → object).

### Phase 10: Regression baselines / CI-CD gate

**Goal**: the `querylens verify` command becomes a real CI/CD gate —
failing a build when performance regresses beyond a configured tolerance,
per the spec's example:

```
querylens verify --max-duration-regression 20 --max-query-count 30
```

**What it needs**: this phase is really "Phase 7's comparison engine,
Phase 8's CLI, wired together with CI-friendly exit codes and output
formatting (e.g. a PR-comment-friendly summary)." No new analytical
capability beyond what Phase 7 already provides — this is packaging and
integration work (documenting how to wire it into GitHub Actions/Azure
DevOps/etc., choosing exit code conventions).

**Validatable in this kind of sandbox?** Yes — same reasoning as Phase 7/8;
this is deterministic logic over already-captured data, no live external
service needed to prove the comparison/gating logic works.

### Phase 11: Dashboard

**Goal**: a persistent web UI — score trends, query lists/details, N+1/
regression views, execution plans rendered visually — per the spec's mockup
(`Performance Score 74/100`, `Queries 1,842`, `Top Slow Queries` list, etc).

**What it needs**:
- A web application project (the spec doesn't mandate a specific stack;
  given this project's ASP.NET Core precedent from the `ModernEfCore`
  sample, an ASP.NET Core Razor Pages or Blazor app is a natural fit, but
  this is an open decision, not yet made).
- Persistent storage for historical reports (something implementing
  `IQueryMetricsSink`, which exists as an interface since Phase 1 but has no
  concrete implementation yet — this phase would need at least one, e.g. a
  SQL Server or SQLite-backed sink).
- Visualizations for trends over time, which requires the *storage* piece
  above to exist first (there's currently no mechanism to retain reports
  across process restarts).

**What it can build on**: every report/finding/score type already exists;
this phase is UI plus a persistence layer that nothing before it has needed
(every phase so far has operated on in-memory, single-run data).

**Validatable in this kind of sandbox?** Partially — UI rendering and a
SQLite-backed sink could be built and tested without external services (in
the same spirit as Phase 4/6), but a full dashboard experience benefits from
a real browser/hosting environment to verify end to end, which this
delivery pattern hasn't required until now.

---

## Part 4 — Consolidated known limitations (all phases)

- No `IQueryMetricsSink` implementations ship yet (console/file/SQL Server/
  OpenTelemetry) — only the interface exists (Phase 1).
- `Analysis.QueryRegression` and the `Privacy.CaptureParameterValues`/
  `RedactSensitiveData` toggles exist in configuration but nothing reads
  them yet (Phase 7 and a future redaction pass, respectively).
- Query Store correlation is best-effort, not guaranteed (Phase 5) —
  documented in `QueryStoreCorrelator`.
- `RowCountDiscrepancyRule` needs an *actual* (not estimated) execution
  plan to fire on anything — Query Store plans are typically estimated-only
  (Phase 6).
- No automatic index creation, DDL generation, or query rewriting anywhere
  in the project — every finding is advisory text, by explicit design.
- No scheduling/automation — every analysis call (`Analyze()`,
  `AnalyzeWithQueryStoreAsync`) is on-demand; nothing runs in the
  background yet (that's implicitly part of Phase 11's territory).

## Part 5: suggested order for what's left

Phases 7 → 8 → 9 → 10 → 11 is also the dependency order: 7 defines the
comparison logic, 8 exposes it (and everything before it) via CLI, 9 adds a
rendering target the CLI can call, 10 is largely 7+8 packaged for CI, and
11 is the only phase needing new infrastructure (persistent storage) that
nothing earlier required. There's no technical reason they couldn't be
reordered, but 7 before 10 and 8 before 9 are the two orderings that avoid
building something on top of a piece that doesn't exist yet.
