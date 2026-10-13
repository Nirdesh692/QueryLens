# QueryLens — User Guide

This guide covers how to use what's actually built today: **`QueryLens.Core`**
(Phase 1), the **EF6 adapter** (Phase 2), the **EF Core adapter** (Phase 3),
and the **ADO.NET adapter** (Phase 4) - which also covers Dapper, since
Dapper executes through whatever connection you hand it. Every supported
project type now has an official one-line (or close to it) registration
path; nothing left in this guide requires hand-rolled glue code.

## 1. The one concept you need: the pipeline

Everything goes through `QueryLensPipeline`:

```csharp
using QueryLens.Core.Pipeline;
using QueryLens.Core.Configuration;

// One pipeline instance per application (it's thread-safe — share it).
var pipeline = new QueryLensPipeline(new QueryLensOptions
{
    Thresholds = { SlowQueryMs = 300, CriticalQueryMs = 800 }
});
```

Two methods matter:

- **`CaptureAsync(QueryExecution)`** — call this once per database command
  your app runs. Give it at least `Sql` and `Duration`; the pipeline
  fingerprints and aggregates it for you.
- **`Analyze()`** — call this whenever you want a report. Returns a
  `PerformanceReport` with `Statistics`, `Findings`, and `Score`.

That's the entire contract. Everything below is just different ways of
calling `CaptureAsync` depending on how your project talks to the database.

## 2. Referencing QueryLens.Core

Add a project reference (or, once you package it, a `<PackageReference>`)
from your app to `QueryLens.Core.csproj`. It targets `netstandard2.0`, so it
works from a .NET Framework 4.7.2/4.8 project just as well as a modern
.NET project — no special configuration needed.

## 3. Wiring it into different project types

### ASP.NET Core + EF Core — official adapter (Phase 3)

This one's built too. Register it in DI, then wire it into your `DbContext`:

```csharp
using QueryLens.EFCore.Extensions;

builder.Services.AddQueryLens(options =>
{
    options.Thresholds.SlowQueryMs = 300;
    options.Thresholds.CriticalQueryMs = 800;
});

builder.Services.AddDbContext<MyDbContext>((sp, options) =>
    options.UseSqlServer(connectionString).AddQueryLensInterceptor(sp));
```

For N+1 and query-explosion detection, bridge the framework's per-request id
into QueryLens - a couple of lines of middleware:

```csharp
app.Use(async (context, next) =>
{
    QueryLensEfCore.SetCurrentRequestId(context.TraceIdentifier);
    await next();
});
```

This uses `AsyncLocal<string>` under the hood (the modern equivalent of
EF6's `CallContext`), which flows across the async calls EF Core makes while
running a query. No DI, e.g. a console app? Skip `AddQueryLens` entirely:

```csharp
var (pipeline, interceptor) = QueryLensEfCore.CreatePipelineAndInterceptor();

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(interceptor)
    .Options;
```

Want parameter names/types/sizes captured too (never values)? Same flag as
EF6:

```csharp
builder.Services.AddQueryLens(options => options.Capture.Parameters = true);
```

**A caveat worth knowing:** same story as EF6 - the shared capture logic
(`DbCommandExecutionBuilder`, used by both adapters) was validated in a
sandboxed environment, but the EF Core-specific interceptor code that calls
into it wasn't compiled against a real `Microsoft.EntityFrameworkCore`
package. It's intentionally thin (EF Core hands you the duration directly,
so there's no timing logic to get wrong, unlike EF6). Build
`QueryLens.EFCore.Tests` on a real machine before leaning on this in
production.

### .NET Framework + EF6 — official adapter (Phase 2)

This one's built. Reference `QueryLens.EF6` and register it once at startup
(e.g. `Global.asax`'s `Application_Start`):

```csharp
using QueryLens.Core.Configuration;
using QueryLens.EF6.Extensions;

var pipeline = QueryLensEf6.Register(new QueryLensOptions
{
    Thresholds = { SlowQueryMs = 300, CriticalQueryMs = 800 }
});
```

That's it - no `DbContext` or LINQ query changes needed. For N+1 and
query-explosion detection, set a request id per unit of work (e.g. in an
`HttpModule`'s `BeginRequest`):

```csharp
QueryLensEf6.SetCurrentRequestId(HttpContext.Current.Request.Headers["X-Request-Id"] ?? Guid.NewGuid().ToString());
```

This uses `CallContext` under the hood, which flows across the async calls
EF6 makes while running a query. If you'd rather source the id from
somewhere else, pass your own delegate instead:

```csharp
var pipeline = QueryLensEf6.Register(requestIdProvider: () => HttpContext.Current?.Items["RequestId"] as string);
```

Want parameter names/types/sizes captured too (never values - see the Core
README)? Turn it on explicitly:

```csharp
QueryLensEf6.Register(new QueryLensOptions { Capture = { Parameters = true } });
```

**A caveat worth knowing:** this adapter was built and its core logic
(SQL/duration/parameter extraction) was validated in a sandboxed environment
without access to a real EF6 assembly - see `src/QueryLens.EF6`'s notes in
the root README. The EF6-interface plumbing itself is small and written
against EF6's long-stable interception API, but hasn't been compiled against
a real `EntityFramework` package. Build `QueryLens.EF6.Tests` on a real
machine before leaning on this in production.

### Raw ADO.NET and Dapper — official adapter (Phase 4)

This one's built too, and it's the adapter to reach for regardless of
project type - it works identically on .NET Framework and modern .NET.
Wrap your connection once, right after creating it:

```csharp
using QueryLens.AdoNet.Extensions;

var (pipeline, connection) = QueryLensAdoNet.CreatePipelineAndWrap(
    new SqlConnection(connectionString),
    new QueryLensOptions { Thresholds = { SlowQueryMs = 300 } });

connection.Open();
```

From here, use `connection` exactly like you would the real one - it *is* a
real `DbConnection`/`IDbConnection`, just wrapped. Raw ADO.NET:

```csharp
using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Orders WHERE CustomerId = @Id";
// ... add parameters, ExecuteReader/ExecuteNonQuery/ExecuteScalar as usual ...
```

Dapper, through the exact same connection - this is the one that matters,
since Dapper has no interception points of its own and previously had no
way to be captured transparently:

```csharp
var orders = connection.Query<Order>("SELECT * FROM Orders WHERE CustomerId = @Id", new { Id = 42 });
```

Both get captured automatically. Already have a pipeline from another
adapter (e.g. you're also using EF Core elsewhere) and just want ADO.NET/
Dapper folded into the same one? Use `Wrap` instead of
`CreatePipelineAndWrap`:

```csharp
var connection = QueryLensAdoNet.Wrap(new SqlConnection(connectionString), existingPipeline);
```

For N+1 detection, same pattern as the other adapters:

```csharp
QueryLensAdoNet.SetCurrentRequestId(Guid.NewGuid().ToString()); // or your framework's request/operation id
```

Need the real, unwrapped provider object for something the wrapper doesn't
expose (e.g. casting to `SqlConnection` for `SqlBulkCopy`)? Every wrapper
type has an escape hatch: `connection.InnerConnection`, `command.InnerCommand`,
`transaction.InnerTransaction`.

**A caveat worth knowing, and a better one this time:** unlike EF6/EF Core,
`DbConnection`/`DbCommand`/`DbTransaction` are part of the BCL, not a
NuGet-only package - so this entire adapter, not just a slice of it, was
actually compiled and run against real base classes in a sandboxed
environment before being shipped. It's the most-validated adapter of the
four. See the root README's testing notes for exactly what that covered.

### Console apps / background workers / message processors

If you're using ADO.NET or Dapper (most likely), use the adapter above -
just set a `RequestId` per unit of work instead of per HTTP request:

```csharp
async Task ProcessMessage(Message message)
{
    QueryLensAdoNet.SetCurrentRequestId(message.MessageId);
    var orders = await connection.QueryAsync<Order>("...", new { message.CustomerId });
}
```

If there's genuinely no `DbCommand` involved (e.g. you're calling a data
store some other way, or writing a from-scratch capture path), fall back to
calling the pipeline directly:

```csharp
async Task ProcessMessage(Message message)
{
    var sw = Stopwatch.StartNew();
    var orders = await LoadOrders(message.CustomerId); // your data access code
    sw.Stop();

    await pipeline.CaptureAsync(new QueryExecution
    {
        Sql = "SELECT * FROM Orders WHERE CustomerId = @CustomerId",
        Duration = sw.Elapsed,
        RequestId = message.MessageId, // groups queries for N+1/explosion detection
        Provider = "AdoNet"
    });
}
```

Without a `RequestId`, `CaptureAsync` still aggregates duration/percentile
statistics fine — you just won't get N+1 or query-explosion findings, since
those rules need to know which queries happened together.

## 4. Enriching with SQL Server Query Store and execution plans (Phases 5-6)

Everything above captures queries as your *application* sees them - SQL
sent, time elapsed. SQL Server's Query Store separately records what
actually happened server-side: CPU, logical reads, and the execution plan
itself. If you're on SQL Server, you can pull that in and get both sides of
the story for the same query:

```csharp
using QueryLens.SqlServer;

await using var connection = new SqlConnection(connectionString); // same database your app used
await connection.OpenAsync();

var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection);
```

`report.Findings` and `report.Score` include everything from Core's rules
*and* two new ones this brings: high-CPU and high-logical-reads, using
Query Store's own numbers (which nothing else can see - client-side timing
alone can't tell you CPU or reads) - *and*, as of this delivery, findings
from parsing each query's execution plan: large scans, expensive sorts and
hash matches, frequent key lookups, nested loops over a large driving input,
spools, parallelism, and SQL Server's own missing-index suggestions if it
made one. No extra call needed - it's all folded into the same
`report.Findings`.

`report.CorrelatedQueries` pairs up the two sides, and now carries the
parsed plan too:

```csharp
foreach (var query in report.CorrelatedQueries)
{
    if (query.HasApplicationData)
        Console.WriteLine($"App: {query.ApplicationStatistics.AverageDurationMs:F0}ms");
    if (query.HasQueryStoreData)
        Console.WriteLine($"SQL Server: {query.QueryStoreEntry.AvgCpuMs:F0}ms CPU, {query.QueryStoreEntry.AvgLogicalReads:N0} reads");
    if (query.ParsedExecutionPlan != null)
        foreach (var op in query.ParsedExecutionPlan.AllOperators)
            Console.WriteLine($"  {op.PhysicalOp}: ~{op.EffectiveEstimatedRows:N0} rows");
}
```

A query can have just one side populated - something Query Store retained
from before your app started capturing, or something your app captured that
fell out of Query Store's retention window. Both are still useful; check
`HasApplicationData`/`HasQueryStoreData` before assuming both are there.
`ParsedExecutionPlan` is null when there was no plan XML to parse, or when
`options.Analysis.ExecutionPlan` is turned off.

**Query Store has to actually be turned on** - it's off by default for new
user databases (on by default only in Azure SQL Database). That's normally
a one-time setup step:

```sql
ALTER DATABASE YourDatabase SET QUERY_STORE = ON;
```

If it's off, `AnalyzeWithQueryStoreAsync` throws `QueryStoreNotEnabledException`
with that exact fix in the message, rather than silently returning nothing.

Want to tune the thresholds?

```csharp
var pipeline = new QueryLensPipeline(new QueryLensOptions
{
    Thresholds =
    {
        HighCpuMs = 100,
        HighLogicalReads = 5000,
        LargeScanRowThreshold = 5000,    // Table/Clustered Index/Index Scan
        KeyLookupRowThreshold = 500,     // Key/RID Lookup, adjusted for repeat executions
        ExpensiveSortRowThreshold = 5000,
        ExpensiveHashRowThreshold = 5000,
        SpoolRowThreshold = 5000
    }
});
```

Or turn execution-plan analysis off entirely while keeping Query Store's
CPU/reads rules: `options.Analysis.ExecutionPlan = false`.

Every plan-based finding tops out at `Severity.Warning` - never `Critical`
- and even `MissingIndexRule` (which relays SQL Server's own suggestion,
not a guess QueryLens made up) says explicitly to verify against your actual
workload before acting on it. These are leads to investigate, not verdicts.

**A caveat worth knowing:** correlation between your app's captured SQL and
Query Store's own record of it is best-effort, not guaranteed - the two
sides compute hashes differently, so QueryLens re-fingerprints Query Store's
SQL text with its own fingerprinter to line them up. This works well in the
common case (the same text reaches the server either way) but isn't
foolproof. Also, unlike every other adapter in this guide, none of the
actual `sys.query_store_*` querying logic (or the exact shape of real
ShowPlan XML) could be checked against a real SQL Server in this project's
sandbox - see the root README's testing notes for exactly what was and
wasn't validated.

## 5. Configuring thresholds

`QueryLensOptions` is a plain object graph, so if you're on ASP.NET Core you
can bind it straight from `appsettings.json` with the standard configuration
binder — no QueryLens-specific config code needed:

```json
{
  "QueryLens": {
    "Thresholds": { "SlowQueryMs": 300, "CriticalQueryMs": 800, "MaxRowsReturned": 5000 },
    "Analysis": { "NPlusOne": true }
  }
}
```

```csharp
var options = new QueryLensOptions();
configuration.GetSection("QueryLens").Bind(options);
var pipeline = new QueryLensPipeline(options);
```

## 6. Reading the report

```csharp
var report = pipeline.Analyze();

Console.WriteLine($"Performance score: {report.Score.Score}/100");
foreach (var component in report.Score.Components.Where(c => c.Penalty != 0))
{
    Console.WriteLine($"  {component.Name}: {component.Penalty} ({component.Reason})");
}

foreach (var finding in report.Findings.OrderByDescending(f => f.Severity))
{
    Console.WriteLine($"[{finding.Severity}] {finding.Title} (confidence {finding.Confidence:P0})");
    Console.WriteLine($"  {finding.Description}");
    Console.WriteLine($"  → {finding.Recommendation}");
}
```

There's no CLI or HTML renderer yet (that's Phase 8-9) — for now, call
`Analyze()` on a timer (e.g. every few minutes, or on-demand from an admin
endpoint) and print, log, or export `PerformanceReport` however suits your
project.

## 7. Practical notes

- **The pipeline is thread-safe.** One shared instance per application is
  the right pattern (register it as a singleton in DI) — `CaptureAsync` can
  be called concurrently from many requests at once.
- **`RequestId` is what unlocks N+1 and query-explosion detection.** If two
  `QueryExecution`s share a `RequestId`, they're treated as part of the same
  unit of work. Anything without a `RequestId` still contributes to
  duration/percentile stats, just not to those two rules.
- **Parameter capture is opt-in and metadata-only.** `QueryExecution.Parameters`
  holds `Name`/`DbType`/`Size` only - there's no `Value` property on
  `QueryParameter` at all, so a capture adapter can't leak parameter values
  even by accident. It's off by default (`QueryLensOptions.Capture.Parameters`);
  all three adapters (EF6, EF Core, ADO.NET) respect this flag automatically.
- **Memory is bounded automatically.** You don't need to worry about a
  long-running process accumulating unbounded data — aggregation keeps a
  capped, reservoir-sampled set of durations per query (default 5,000), and
  the pipeline's raw-execution buffer for request-scoped rules is capped too
  (default 10,000, configurable via `QueryLensPipeline`'s constructor).
- **Findings can repeat across `Analyze()` calls.** Each call re-evaluates
  rules over everything aggregated so far — it's a snapshot, not an event
  stream. If you want "only tell me about new problems," diff two reports
  yourself for now (baseline comparison is Phase 10).

## Quick reference

| Project type | Today | Coming later |
|---|---|---|
| .NET Framework + EF6 | `QueryLensEf6.Register(...)` (§3) - built | - |
| ASP.NET Core / any host + EF Core | `services.AddQueryLens()` + `AddQueryLensInterceptor()` (§3) - built | - |
| Raw ADO.NET | `QueryLensAdoNet.Wrap(...)` (§3) - built | - |
| Dapper | Same wrapper as ADO.NET (§3) - built | - |
| Console / background | Wrapped connection, or manual `CaptureAsync` (§3) | - |
| SQL Server CPU/logical reads | `QueryLensSqlServer.AnalyzeWithQueryStoreAsync(...)` (§4) - built | - |
| Execution plan analysis (scans, sorts, missing indexes, ...) | Same call as above (§4) - built | - |
| Reading results | `pipeline.Analyze()` in code (§6) | CLI (Phase 8), HTML report (Phase 9) |
