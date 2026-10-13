using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EFCore.Interceptors;
using Xunit;

namespace QueryLens.EFCore.Tests
{
    // NOTE ON VALIDATION: this test was NOT compiled or run in the delivery
    // sandbox - it has no access to nuget.org, and Microsoft.EntityFrameworkCore
    // is NuGet-only. Unlike the EF6 adapter's interceptor tests, this one uses a
    // real relational provider (Sqlite, in-memory) rather than hand-constructed
    // event data, because EF Core's CommandExecutedEventData requires internal
    // diagnostic infrastructure (EventDefinitionBase etc.) that isn't practical
    // to fake correctly - a real provider is the standard, EF-team-recommended
    // way to test a DbCommandInterceptor. The underlying field-extraction logic
    // (DbCommandExecutionBuilder) IS validated - see QueryLens.Core.Tests. This
    // file has not been proven to compile; please build/run it on a real
    // machine before relying on it.
    public class QueryLensEfCoreInterceptorTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public QueryLensEfCoreInterceptorTests()
        {
            // Sqlite's in-memory mode destroys the database when the connection
            // closes, so the connection must stay open for the test's lifetime.
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public void Dispose() => _connection.Dispose();

        private TestDbContext CreateContext(QueryLensPipeline pipeline)
        {
            var interceptor = new QueryLensEfCoreInterceptor(pipeline, requestIdProvider: () => "req-1");

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptor)
                .Options;

            var context = new TestDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public void RunningAQuery_CapturesItInThePipeline()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());

            using (var db = CreateContext(pipeline))
            {
                db.Customers.Add(new TestCustomer { Name = "Alice" });
                db.SaveChanges();

                _ = db.Customers.ToList();
            }

            var report = pipeline.Analyze();

            Assert.NotEmpty(report.Statistics);
            Assert.Contains(report.Statistics, s => s.NormalizedSql.Contains("Customers", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RepeatedQueriesWithinOneRequest_TriggerNPlusOneFinding()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());

            using (var db = CreateContext(pipeline))
            {
                for (var i = 1; i <= 10; i++)
                {
                    db.Customers.Add(new TestCustomer { Name = $"Customer {i}" });
                }

                db.SaveChanges();

                // Deliberate N+1: one query per customer instead of a join/Include.
                foreach (var customer in db.Customers.ToList())
                {
                    _ = db.Orders.Count(o => o.CustomerId == customer.Id);
                }
            }

            var report = pipeline.Analyze();

            Assert.Contains(report.Findings, f => f.RuleId == "NPLUSONE001");
        }

        [Fact]
        public void AsyncQueries_AreCapturedTheSameAsSyncQueries()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());

            using (var db = CreateContext(pipeline))
            {
                db.Customers.Add(new TestCustomer { Name = "Bob" });
                db.SaveChangesAsync().GetAwaiter().GetResult();

                _ = db.Customers.ToListAsync().GetAwaiter().GetResult();
            }

            var report = pipeline.Analyze();

            Assert.NotEmpty(report.Statistics);
        }
    }
}
