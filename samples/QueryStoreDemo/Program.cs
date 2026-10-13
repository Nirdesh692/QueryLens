using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QueryLens.Core.Configuration;
using QueryLens.EFCore.Extensions;
using QueryLens.SqlServer;
using QueryStoreDemoSample;

// Illustrative .NET 8 + EF Core console app. Needs a real SQL Server instance
// (LocalDB works) with Query Store enabled - this sample was not built or run
// in the delivery sandbox (no SQL Server available there). See the Phase 5
// notes in the root README before relying on the QueryLens.SqlServer pieces.
const string connectionString =
    "Server=(localdb)\\MSSQLLocalDB;Database=QueryLensQueryStoreDemo;Trusted_Connection=True;TrustServerCertificate=True";

// Query Store is off by default for new user databases (it's on by default
// only for Azure SQL Database). Enabling it is normally a one-time DBA/setup
// step, not something application code does on every run - shown here inline
// only to make this sample self-contained.
await using (var setupConnection = new SqlConnection(connectionString))
{
    await setupConnection.OpenAsync();
    await using var enableQueryStore = setupConnection.CreateCommand();
    enableQueryStore.CommandText = "ALTER DATABASE CURRENT SET QUERY_STORE = ON;";
    await enableQueryStore.ExecuteNonQueryAsync();
}

// One call registers the EF Core interceptor and gives you a pipeline back.
var pipeline = new QueryLens.Core.Pipeline.QueryLensPipeline(new QueryLensOptions
{
    Thresholds = { HighCpuMs = 50, HighLogicalReads = 1000 }
});
var interceptor = QueryLensEfCore.CreateInterceptor(pipeline);

var options = new DbContextOptionsBuilder<SampleContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(interceptor)
    .Options;

await using (var db = new SampleContext(options))
{
    await db.Database.EnsureCreatedAsync();
    await SeedIfEmptyAsync(db);

    QueryLensEfCore.SetCurrentRequestId(Guid.NewGuid().ToString());

    // Deliberate N+1, same as the other samples - one query per customer
    // instead of a join/Include.
    foreach (var customer in await db.Customers.ToListAsync())
    {
        var orderCount = await db.Orders.CountAsync(o => o.CustomerId == customer.Id);
        Console.WriteLine($"{customer.Name}: {orderCount} orders");
    }
}

// Now enrich with what SQL Server itself recorded - a separate connection to
// the same database is all that's needed; QueryLens.SqlServer takes a plain
// DbConnection, no SqlClient-specific dependency of its own. This also
// parses each query's execution plan automatically (Table Scan/Sort/Hash
// Match/etc. detection, plus SQL Server's own missing-index suggestions if
// present) - report.Findings below already includes those alongside the
// duration/CPU/reads findings, no extra call needed.
await using var queryStoreConnection = new SqlConnection(connectionString);
await queryStoreConnection.OpenAsync();

var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, queryStoreConnection);

Console.WriteLine();
Console.WriteLine($"QueryLens score: {report.Score.Score}/100");
Console.WriteLine();

foreach (var query in report.CorrelatedQueries.Where(q => q.HasApplicationData || q.HasQueryStoreData))
{
    Console.WriteLine($"Query: {query.QueryHash}");
    if (query.HasApplicationData)
    {
        Console.WriteLine($"  Application duration: {query.ApplicationStatistics!.AverageDurationMs:F0}ms avg ({query.ApplicationStatistics.ExecutionCount} executions)");
    }

    if (query.HasQueryStoreData)
    {
        var qs = query.QueryStoreEntry!;
        Console.WriteLine($"  SQL Server duration:  {qs.AvgDurationMs:F0}ms avg");
        Console.WriteLine($"  CPU:                  {qs.AvgCpuMs:F0}ms avg");
        Console.WriteLine($"  Logical reads:        {qs.AvgLogicalReads:N0} avg");
    }

    Console.WriteLine();
}

foreach (var finding in report.Findings)
{
    Console.WriteLine($"[{finding.Severity}] {finding.Title} (confidence {finding.Confidence:P0})");
    Console.WriteLine($"  {finding.Description}");
    Console.WriteLine($"  -> {finding.Recommendation}");
}

static async Task SeedIfEmptyAsync(SampleContext db)
{
    if (await db.Customers.AnyAsync())
    {
        return;
    }

    for (var i = 1; i <= 20; i++)
    {
        db.Customers.Add(new Customer
        {
            Name = $"Customer {i}",
            Orders = Enumerable.Range(1, 3).Select(n => new Order { Total = n * 10m }).ToList()
        });
    }

    await db.SaveChangesAsync();
}
