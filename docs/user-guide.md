# QueryLens — Complete User Guide

A step-by-step guide to installing, configuring, and using **QueryLens** in any .NET project.

---

## Table of Contents
1. [Overview & Architecture](#1-overview--architecture)
2. [Step 1: Installation & Package Selection](#step-1-installation--package-selection)
3. [Step 2: Configuration & Thresholds](#step-2-configuration--thresholds)
4. [Step 3: Integrating with Your Data Access Layer](#step-3-integrating-with-your-data-access-layer)
   - [Track A: ASP.NET Core & EF Core](#track-a-aspnet-core--entity-framework-core)
   - [Track B: Raw ADO.NET & Dapper](#track-b-raw-adonet--dapper)
   - [Track C: .NET Framework & EF6](#track-c-net-framework--entity-framework-6)
   - [Track D: Background Workers, Message Handlers & Console Apps](#track-d-background-workers-message-handlers--console-apps)
5. [Step 4: SQL Server Query Store & Execution Plan Analysis](#step-4-sql-server-query-store--execution-plan-analysis)
6. [Step 5: Reading Results in Code & Exposing Diagnostic Endpoints](#step-5-reading-results-in-code--exposing-diagnostic-endpoints)
7. [Step 6: Using the QueryLens CLI (JSON, HTML Reports & CI Gates)](#step-6-using-the-querylens-cli-json-html-reports--ci-gates)
8. [Step 7: Performance Baselines & Regression Detection](#step-7-performance-baselines--regression-detection)
9. [Production Best Practices & Privacy Model](#production-best-practices--privacy-model)

---

## 1. Overview & Architecture

QueryLens is a lightweight, cross-version .NET database performance analyzer. It intercepts database commands at runtime, normalizes and fingerprints queries, aggregates execution metrics using reservoir sampling, and evaluates rules to produce actionable performance findings and a 0–100 health score.

```
+-------------------------------------------------------------------------+
|                        Application Data Access Layer                    |
|       EF Core Interceptor   |   EF6 Interceptor   |   ADO.NET Wrapper   |
+-------------------------------------------------------------------------+
                                     │
                        QueryExecution (Sql, Duration, RequestId)
                                     ▼
+─────────────────────────────────────────────────────────────────────────+
|                         QueryLens Core Pipeline                         |
|  1. SQL Fingerprinter (collapses literals, masks IN lists)             |
|  2. Reservoir Aggregator (bounded memory, P50/P90/P95/P99 percentiles)  |
|  3. Rule Engine (Slow Query, Critical, N+1, Explosion, Scans, etc.)     |
|  4. Performance Scorer (0-100 score with category penalty breakdown)    |
+─────────────────────────────────────────────────────────────────────────+
             │                                              │
             ▼                                              ▼
  In-Memory Reports / API                   CLI Tool / Offline HTML Reports
 (JSON, Telemetry, Baselines)              (querylens analyze/report/verify)
```

### Core Tenet: Zero Parameter Values Captured
Parameter **values are never recorded or stored under any circumstance**. The `QueryParameter` model only exposes `Name`, `DbType`, and `Size`. Parameter capture is opt-in metadata-only (`options.Capture.Parameters = true`).

---

## Step 1: Installation & Package Selection

Choose the package(s) matching your application stack:

| Package | Target Runtimes | What it's for |
|---|---|---|
| **`QueryLens.Core`** | `netstandard2.0` (.NET Core, .NET 6/8/9/10, .NET Framework 4.6.1+) | Core pipeline, models, fingerprinting, aggregation, rules, scoring |
| **`QueryLens.EFCore`** | `net8.0+` | Interceptors and DI extensions for EF Core |
| **`QueryLens.EF6`** | `net472;net48` | Interceptor and ambient correlation for Entity Framework 6 |
| **`QueryLens.AdoNet`** | `netstandard2.0` | Transparent connection/command decorator for raw ADO.NET and Dapper |
| **`QueryLens.SqlServer`** | `netstandard2.0` | SQL Server Query Store reader, correlator, and ShowPlan XML parser |
| **`QueryLens.Cli`** | `net8.0+` (Global Tool) | Command-line tool for analysis, HTML reports, comparisons, and CI gates |

### Installing via .NET CLI:
```bash
# For modern EF Core projects:
dotnet add package QueryLens.EFCore

# For Dapper / ADO.NET projects:
dotnet add package QueryLens.AdoNet

# For SQL Server Query Store & Execution Plan analysis:
dotnet add package QueryLens.SqlServer

# Install the QueryLens CLI globally:
dotnet tool install --global QueryLens.Cli
```

### Installing in .NET Framework / Legacy projects:
```powershell
# In Visual Studio Package Manager Console:
Install-Package QueryLens.EF6
# or for Dapper/ADO.NET:
Install-Package QueryLens.AdoNet
```

### Project Reference (if building from source):
```xml
<ItemGroup>
  <ProjectReference Include="..\QueryLens.EFCore\QueryLens.EFCore.csproj" />
  <!-- or QueryLens.AdoNet, QueryLens.Core, etc. -->
</ItemGroup>
```

---

## Step 2: Configuration & Thresholds

QueryLens works out of the box with sensible defaults, but every threshold and analysis rule is customizable.

### Option A: Via `appsettings.json` (ASP.NET Core)
Add a `QueryLens` section to your configuration:

```json
{
  "QueryLens": {
    "Enabled": true,
    "Capture": {
      "Sql": true,
      "Parameters": false
    },
    "Thresholds": {
      "SlowQueryMs": 300,
      "CriticalQueryMs": 1000,
      "MaxQueriesPerRequestWarning": 25,
      "MaxQueriesPerRequestCritical": 50,
      "MaxRowsReturned": 5000,
      "NPlusOneMinExecutions": 3,
      "NPlusOneMinConfidence": 0.6,
      "HighCpuMs": 200,
      "HighLogicalReads": 10000,
      "RegressionTolerancePercent": 20
    },
    "Analysis": {
      "NPlusOne": true,
      "QueryRegression": true,
      "ExecutionPlan": true,
      "QueryStore": true
    }
  }
}
```

### Option B: In-Code Configuration
```csharp
using QueryLens.Core.Configuration;

var options = new QueryLensOptions();
options.Thresholds.SlowQueryMs = 250;
options.Thresholds.CriticalQueryMs = 800;
options.Capture.Parameters = true; // Captures names/types/sizes only, never values
```

---

## Step 3: Integrating with Your Data Access Layer

### Track A: ASP.NET Core & Entity Framework Core

#### 1. Register QueryLens in `Program.cs`:
```csharp
using QueryLens.EFCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register QueryLens pipeline in DI (binds options from appsettings.json automatically)
builder.Services.AddQueryLens(builder.Configuration.GetSection("QueryLens"));

// Alternatively, configure via code:
// builder.Services.AddQueryLens(options => {
//     options.Thresholds.SlowQueryMs = 300;
// });

// Register your DbContext and attach the QueryLens interceptor
builder.Services.AddDbContext<AppDbContext>((serviceProvider, dbOptions) =>
{
    dbOptions.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
             .AddQueryLensInterceptor(serviceProvider);
});
```

#### 2. Add Request Correlation Middleware:
To enable **N+1 query detection** and **Query Explosion** rules, bridge the HTTP request identifier to ambient correlation:

```csharp
var app = builder.Build();

app.Use(async (context, next) =>
{
    // Sets ambient RequestId for the duration of the HTTP request
    QueryLensEfCore.SetCurrentRequestId(context.TraceIdentifier);
    await next();
});
```

#### 3. Standalone / Non-DI EF Core (e.g., Console or Test Runner):
```csharp
var (pipeline, interceptor) = QueryLensEfCore.CreatePipelineAndInterceptor();

var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(interceptor)
    .Options;

using var context = new AppDbContext(dbOptions);
```

---

### Track B: Raw ADO.NET & Dapper

The `QueryLens.AdoNet` adapter wraps any `DbConnection` (`SqlConnection`, `NpgsqlConnection`, `SqliteConnection`, etc.) with zero changes required to your queries or Dapper calls.

#### 1. Wrap the Connection at Creation:
```csharp
using System.Data.SqlClient; // or Microsoft.Data.SqlClient
using QueryLens.AdoNet.Extensions;
using QueryLens.Core.Pipeline;

// Create or inject a singleton pipeline
var pipeline = new QueryLensPipeline();

// Wrap your raw connection:
using var rawConn = new SqlConnection(connectionString);
using var connection = QueryLensAdoNet.Wrap(rawConn, pipeline);

connection.Open();
```

#### 2. Execute with Dapper or Raw ADO.NET:
Because the returned object is a standard `DbConnection`, Dapper works transparently:

```csharp
using Dapper;

// Ambient RequestId for N+1 grouping:
QueryLensAdoNet.SetCurrentRequestId(Guid.NewGuid().ToString());

// Dapper queries are captured automatically:
var customer = connection.QueryFirstOrDefault<Customer>(
    "SELECT * FROM Customers WHERE Id = @Id", new { Id = 42 });

// Raw ADO.NET commands are also captured automatically:
using var cmd = connection.CreateCommand();
cmd.CommandText = "UPDATE Accounts SET Balance = Balance - @Amount WHERE Id = @Id";
// cmd.Parameters.Add(...);
cmd.ExecuteNonQuery();
```

#### 3. Escape Hatch for Provider-Specific APIs:
If you need provider-specific features (such as `SqlBulkCopy`):
```csharp
if (connection is QueryLensDbConnection wrapped)
{
    var realConn = (SqlConnection)wrapped.InnerConnection;
    using var bulk = new SqlBulkCopy(realConn);
    // ...
}
```

---

### Track C: .NET Framework & Entity Framework 6

#### 1. Register Once in `Global.asax.cs`:
```csharp
using QueryLens.EF6.Extensions;
using QueryLens.Core.Configuration;

public class MvcApplication : System.Web.HttpApplication
{
    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();
        RouteConfig.RegisterRoutes(RouteTable.Routes);

        // One-line registration: hooks into EF6's DbInterception
        QueryLensEf6.Register(new QueryLensOptions
        {
            Thresholds = { SlowQueryMs = 400 }
        });
    }

    protected void Application_BeginRequest()
    {
        // Correlation for N+1 detection in legacy apps (uses CallContext)
        var requestId = Request.Headers["X-Request-Id"] ?? Guid.NewGuid().ToString();
        QueryLensEf6.SetCurrentRequestId(requestId);
    }
}
```

---

### Track D: Background Workers, Message Handlers & Console Apps

For background processors, queue consumers (RabbitMQ, Azure Service Bus, Kafka), or Hangfire jobs, set a `RequestId` per message:

```csharp
public async Task ProcessMessageAsync(OrderMessage message)
{
    // Group queries within this message unit of work
    QueryLensAdoNet.SetCurrentRequestId(message.MessageId);

    // Queries executed here will be grouped for N+1 and explosion detection
    var order = await _db.QueryAsync<Order>("SELECT * FROM Orders WHERE Id = @Id", new { message.OrderId });
    // ...
}
```

---

## Step 4: SQL Server Query Store & Execution Plan Analysis

Client-side timing only tells part of the story. By combining client capture with SQL Server's **Query Store**, QueryLens correlates application queries with server-side CPU, logical reads, and parses ShowPlan XML execution plans.

### 1. Enable Query Store on your SQL Server Database:
```sql
ALTER DATABASE YourDatabase SET QUERY_STORE = ON;
```

### 2. Run Enriched Analysis in C#:
```csharp
using QueryLens.SqlServer;

// Pass your pipeline and an open connection to your database
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection);

// 1. Overall health score
Console.WriteLine($"Score: {report.Score.Score}/100");

// 2. Correlated application + server metrics
foreach (var query in report.CorrelatedQueries)
{
    Console.WriteLine($"Query: {query.NormalizedSql}");
    if (query.HasApplicationData)
        Console.WriteLine($"  App Avg Duration: {query.ApplicationStatistics.AverageDurationMs:F1}ms");
    if (query.HasQueryStoreData)
        Console.WriteLine($"  SQL Server CPU: {query.QueryStoreEntry.AvgCpuMs:F1}ms, Reads: {query.QueryStoreEntry.AvgLogicalReads:N0}");

    // 3. Parsed Execution Plan Operators & Missing Indexes
    if (query.ParsedExecutionPlan != null)
    {
        foreach (var op in query.ParsedExecutionPlan.AllOperators)
        {
            if (op.PhysicalOp.Contains("Scan"))
                Console.WriteLine($"  [Plan] {op.PhysicalOp} on {op.ObjectName} (~{op.EffectiveEstimatedRows:N0} rows)");
        }

        foreach (var idx in query.ParsedExecutionPlan.MissingIndexes)
        {
            Console.WriteLine($"  [Index Suggestion] Table: {idx.Table}, Impact: {idx.Impact}%");
            Console.WriteLine($"    Equality: {string.Join(", ", idx.EqualityColumns)}");
        }
    }
}
```

---

## Step 5: Reading Results in Code & Exposing Diagnostic Endpoints

### In-Code Analysis:
```csharp
var report = pipeline.Analyze();

Console.WriteLine($"Performance Score: {report.Score.Score}/100");

// Check findings
foreach (var finding in report.Findings.OrderByDescending(f => f.Severity))
{
    Console.WriteLine($"[{finding.Severity}] {finding.Title} (Rule: {finding.RuleId}, Confidence: {finding.Confidence:P0})");
    Console.WriteLine($"  Description: {finding.Description}");
    Console.WriteLine($"  Recommendation: {finding.Recommendation}");
}
```

### Exposing an Admin Diagnostic Endpoint in ASP.NET Core:
```csharp
app.MapGet("/diagnostics/querylens", (QueryLensPipeline pipeline) =>
{
    var report = pipeline.Analyze();
    return Results.Ok(new
    {
        Score = report.Score.Score,
        Queries = report.Statistics.Count,
        Findings = report.Findings
    });
}).RequireAuthorization("AdminPolicy");
```

---

## Step 6: Using the QueryLens CLI (JSON, HTML Reports & CI Gates)

Install the CLI tool:
```bash
dotnet tool install --global QueryLens.Cli
```

### 1. Generating a Self-Contained HTML Report:
Turn captured statistics or reports into an offline, interactive HTML report with embedded CSS, color scorecards, top performance culprits, and query catalogs:

```bash
# Render directly from a saved statistics snapshot:
querylens report current.json --format html --output performance-report.html --label "Sprint 42 Release"

# Render in JSON format:
querylens report current.json --format json --output report.json
```

### 2. Comparing Two Runs (Terminal Summary):
Compare current performance against a previous baseline:

```bash
querylens compare baseline.json current.json
```
Output:
```text
Comparing baseline (12 queries) to current (14 queries):

Hash          D-Avg (%)   D-P95 (%)   CPU (%)    Reads (%)   Execs (%)  Status
---------------------------------------------------------------------------------
abc123456789  +45.2%      +52.1%      +40.0%     +0.0%       +10.0%     Regressed
def987654321  -12.0%      -8.5%       -15.0%     -5.0%       0.0%       Improved
ghi111222333  ---         ---         ---        ---         ---        New
```

### 3. CI/CD Performance Gate (`verify`):
Break automated build pipelines (exit code 1) if queries regress or query explosion limits are breached:

```bash
# Fail if any query regressed by more than 20% compared to baseline:
querylens verify current.json --baseline baseline.json --max-duration-regression 20

# Fail if total database executions exceed 100:
querylens verify current.json --max-query-count 100

# Combined gate:
querylens verify current.json --baseline baseline.json --max-duration-regression 15 --max-query-count 50
```

---

## Step 7: Performance Baselines & Regression Detection

Save snapshots across releases to detect regressions over time:

```csharp
// 1. Create and persist a baseline snapshot
var baseline = pipeline.CreateBaseline("Release-1.0");
var json = BaselineSerializer.Serialize(baseline);
File.WriteAllText("baseline-1.0.json", json);

// 2. Later: analyze a current run against the saved baseline
var savedBaseline = BaselineSerializer.Deserialize(File.ReadAllText("baseline-1.0.json"));
var currentReport = pipeline.AnalyzeAgainstBaseline(savedBaseline);

// Automatically includes 'QUERYREGRESSION001' findings for any regressed queries!
foreach (var reg in currentReport.Findings.Where(f => f.RuleId == "QUERYREGRESSION001"))
{
    Console.WriteLine($"Regression in {reg.QueryHash}: {reg.Description}");
}
```

---

## Production Best Practices & Privacy Model

1. **Shared Singleton Pipeline**:
   The `QueryLensPipeline` is fully thread-safe and designed to run as a single shared instance across the entire application lifecycle.
2. **Bounded Memory via Reservoir Sampling**:
   QueryLens automatically bounds memory usage. It retains a maximum reservoir sample size (default 5,000 durations per query hash) and bounds the raw request buffer (default 10,000 executions). Memory consumption remains constant even in high-throughput applications running for months.
3. **Privacy by Design**:
   - Parameter values cannot be leaked because `QueryParameter` has no `Value` property.
   - Captured SQL text normalizes string and numeric literals into `?` placeholders.
4. **Advisory Rule Engine**:
   Execution plan and Query Store findings top out at `Severity.Warning` — recommendations are diagnostic leads to guide engineering decisions, not automated schema modifications.
