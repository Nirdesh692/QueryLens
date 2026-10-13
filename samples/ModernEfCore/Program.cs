using Microsoft.EntityFrameworkCore;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EFCore.Extensions;
using ModernEfCoreSample;

// Illustrative ASP.NET Core 8 + EF Core minimal API. Uses Sqlite so it runs
// standalone with no external database - `dotnet run` and hit the endpoints
// below. This sample was not built or executed in the delivery sandbox (no
// internet access to restore Microsoft.EntityFrameworkCore.Sqlite there) -
// see the Phase 3 notes in the root README before relying on it.
var builder = WebApplication.CreateBuilder(args);

// One call to register the pipeline + interceptor in DI.
builder.Services.AddQueryLens(options =>
{
    options.Thresholds.SlowQueryMs = 200;
    options.Thresholds.CriticalQueryMs = 1000;
});

builder.Services.AddDbContext<SampleContext>((sp, options) =>
    options.UseSqlite("Data Source=querylens_sample.db").AddQueryLensInterceptor(sp));

var app = builder.Build();

// Bridge ASP.NET Core's per-request TraceIdentifier into QueryLens' ambient
// request id, so N+1/query-explosion detection has something to group on.
// This is the one piece of glue code the ASP.NET Core host needs to provide -
// everything else is handled by the adapter.
app.Use(async (context, next) =>
{
    QueryLensEfCore.SetCurrentRequestId(context.TraceIdentifier);
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleContext>();
    db.Database.EnsureCreated();
    SeedIfEmpty(db);
}

app.MapGet("/customers", async (SampleContext db) =>
{
    // Deliberate N+1: one query per customer instead of a single join/Include.
    // QueryLens should flag this once /querylens/report is hit.
    var customers = await db.Customers.ToListAsync();
    var result = new List<object>();

    foreach (var customer in customers)
    {
        var orderCount = await db.Orders.CountAsync(o => o.CustomerId == customer.Id);
        result.Add(new { customer.Name, orderCount });
    }

    return Results.Ok(result);
});

app.MapGet("/querylens/report", (QueryLensPipeline pipeline) =>
{
    var report = pipeline.Analyze();

    return Results.Ok(new
    {
        score = report.Score.Score,
        components = report.Score.Components
            .Where(c => c.Penalty != 0)
            .Select(c => new { c.Name, c.Penalty, c.Reason }),
        findings = report.Findings.Select(f => new
        {
            f.RuleId,
            severity = f.Severity.ToString(),
            f.Title,
            f.Description,
            f.Recommendation,
            confidence = f.Confidence
        })
    });
});

app.Run();

static void SeedIfEmpty(SampleContext db)
{
    if (db.Customers.Any())
    {
        return;
    }

    for (var i = 1; i <= 20; i++)
    {
        var customer = new Customer
        {
            Name = $"Customer {i}",
            Orders = Enumerable.Range(1, 3).Select(n => new Order { Total = n * 10m }).ToList()
        };
        db.Customers.Add(customer);
    }

    db.SaveChanges();
}
