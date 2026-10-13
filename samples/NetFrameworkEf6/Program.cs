using System;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.EF6.Extensions;

namespace NetFrameworkEf6Sample
{
    /// <summary>
    /// Illustrative .NET Framework 4.7.2 + EF6 console app. Requires a real SQL
    /// Server/LocalDB instance (see App.config) to actually run - this sample
    /// was not built or executed in the delivery sandbox (no Windows/.NET
    /// Framework/EF6 assembly available there). Wiring shown here follows the
    /// documented EF6 interception API; sanity-check against your EF6/package
    /// versions before relying on it in production.
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            // One line to wire QueryLens into every DbContext in the process -
            // no changes to SampleContext or any LINQ query required.
            var pipeline = QueryLensEf6.Register(new QueryLensOptions
            {
                Thresholds = { SlowQueryMs = 200, CriticalQueryMs = 1000 }
            });

            using (var db = new SampleContext())
            {
                db.Database.CreateIfNotExists();
                SeedIfEmpty(db);

                // A per-request correlation id would normally come from an
                // HttpModule/action filter; here we set one manually to
                // demonstrate N+1 detection within "one unit of work".
                QueryLensEf6.SetCurrentRequestId(Guid.NewGuid().ToString());

                // Deliberate N+1: one query per customer instead of a single
                // join/Include. QueryLens should flag this once Analyze() runs.
                foreach (var customer in db.Customers.ToList())
                {
                    var orderCount = db.Orders.Count(o => o.CustomerId == customer.Id);
                    Console.WriteLine($"{customer.Name}: {orderCount} orders");
                }
            }

            var report = pipeline.Analyze();

            Console.WriteLine();
            Console.WriteLine($"QueryLens score: {report.Score.Score}/100");
            foreach (var component in report.Score.Components)
            {
                if (component.Penalty != 0)
                {
                    Console.WriteLine($"  {component.Name}: {component.Penalty} ({component.Reason})");
                }
            }

            Console.WriteLine();
            foreach (var finding in report.Findings)
            {
                Console.WriteLine($"[{finding.Severity}] {finding.Title} (confidence {finding.Confidence:P0})");
                Console.WriteLine($"  {finding.Description}");
                Console.WriteLine($"  -> {finding.Recommendation}");
            }
        }

        private static void SeedIfEmpty(SampleContext db)
        {
            if (db.Customers.Any())
            {
                return;
            }

            for (var i = 1; i <= 20; i++)
            {
                var customer = new Customer { Name = $"Customer {i}" };
                customer.Orders = Enumerable.Range(1, 3)
                    .Select(n => new Order { Total = n * 10m })
                    .ToList();
                db.Customers.Add(customer);
            }

            db.SaveChanges();
        }
    }
}
