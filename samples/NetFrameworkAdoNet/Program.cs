using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using QueryLens.AdoNet.Extensions;
using QueryLens.Core.Configuration;

namespace NetFrameworkAdoNetSample
{
    /// <summary>
    /// Illustrative .NET Framework 4.7.2 console app. Requires a real SQL
    /// Server/LocalDB instance (see App.config) to actually run - this sample
    /// was not built or executed in the delivery sandbox (no Windows/.NET
    /// Framework available there). Shows two things deliberately in one
    /// sample: raw ADO.NET capture, and that Dapper gets captured "for free"
    /// once it's handed a wrapped connection - Dapper has no interception
    /// points of its own, so this is the only way to get first-class Dapper
    /// support without changing every call site.
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["SampleDb"].ConnectionString;

            // One call wraps the connection and gives you a pipeline back.
            var (pipeline, connection) = QueryLensAdoNet.CreatePipelineAndWrap(
                new SqlConnection(connectionString),
                new QueryLensOptions { Thresholds = { SlowQueryMs = 200, CriticalQueryMs = 1000 } });

            using (connection)
            {
                connection.Open();
                EnsureSchemaAndSeed(connection);

                // A per-request correlation id would normally come from
                // wherever your app defines a "unit of work"; set one manually
                // here to demonstrate N+1 detection.
                QueryLensAdoNet.SetCurrentRequestId(Guid.NewGuid().ToString());

                Console.WriteLine("-- Raw ADO.NET --");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name FROM Customers";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"  {reader["Name"]}");
                        }
                    }
                }

                Console.WriteLine("-- Dapper, through the same wrapped connection --");
                // Deliberate N+1: one query per customer instead of a single
                // join. QueryLens should flag this once Analyze() runs, and it
                // works even though QueryLens never had to know Dapper exists -
                // Dapper just executed against the IDbConnection it was given.
                var customers = connection.Query<Customer>("SELECT Id, Name FROM Customers").ToList();
                foreach (var customer in customers)
                {
                    var orderCount = connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId",
                        new { CustomerId = customer.Id });
                    Console.WriteLine($"  {customer.Name}: {orderCount} orders");
                }
            }

            var report = pipeline.Analyze();

            Console.WriteLine();
            Console.WriteLine($"QueryLens score: {report.Score.Score}/100");
            foreach (var finding in report.Findings)
            {
                Console.WriteLine($"[{finding.Severity}] {finding.Title} (confidence {finding.Confidence:P0})");
                Console.WriteLine($"  {finding.Description}");
                Console.WriteLine($"  -> {finding.Recommendation}");
            }
        }

        private static void EnsureSchemaAndSeed(System.Data.Common.DbConnection connection)
        {
            connection.Execute(@"
                IF OBJECT_ID('Customers') IS NULL
                BEGIN
                    CREATE TABLE Customers (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100));
                    CREATE TABLE Orders (Id INT IDENTITY PRIMARY KEY, CustomerId INT, Total DECIMAL(10,2));
                END");

            var existing = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers");
            if (existing > 0)
            {
                return;
            }

            for (var i = 1; i <= 20; i++)
            {
                var customerId = connection.ExecuteScalar<int>(
                    "INSERT INTO Customers (Name) OUTPUT INSERTED.Id VALUES (@Name)",
                    new { Name = $"Customer {i}" });

                for (var n = 1; n <= 3; n++)
                {
                    connection.Execute(
                        "INSERT INTO Orders (CustomerId, Total) VALUES (@CustomerId, @Total)",
                        new { CustomerId = customerId, Total = n * 10m });
                }
            }
        }

        private class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
