using System;
using System.Linq;
using System.Threading.Tasks;
using QueryLens.SqlServer.QueryStore;
using QueryLens.SqlServer.Tests.TestHelpers;
using Xunit;

namespace QueryLens.SqlServer.Tests.QueryStore
{
    // NOTE ON VALIDATION: QueryStoreReader's SQL text (querying
    // sys.query_store_* catalog views) could not be validated against a real
    // SQL Server in this delivery's sandbox - no SQL Server instance was
    // available, and Query Store is a proprietary SQL Server feature with no
    // offline/apt substitute. The tests below exercise everything BUT that SQL
    // text: MapRow only depends on IDataRecord (part of the BCL), and
    // ReadAsync's control flow (the enabled check, then reading rows) was
    // exercised end-to-end against a fake connection/reader that returns
    // canned data regardless of what SQL was sent. All of this - including
    // these exact Fake* helpers - was compiled and run in a throwaway .NET 8
    // harness before being committed here. What's unverified is narrow and
    // specific: whether the T-SQL in QueryStoreReader.SelectSql is valid
    // against a real Query Store schema. Please run this against a real SQL
    // Server (with Query Store enabled) before relying on it in production.
    public class QueryStoreReaderTests
    {
        private static readonly string[] Columns =
        {
            "QueryId", "PlanId", "QueryText", "ExecutionCount", "AvgDurationUs", "AvgCpuUs",
            "AvgLogicalReads", "AvgPhysicalReads", "AvgMemoryKb", "LastExecutionTime", "ExecutionPlanXml"
        };

        [Fact]
        public void MapRow_ConvertsMicrosecondsToMillisecondsAndMapsAllFields()
        {
            var reader = new FakeDbDataReader(Columns, new[]
            {
                new object[]
                {
                    101L, 501L, "SELECT * FROM Orders WHERE CustomerId = 42", 15821L,
                    420000.0, 180000.0, 32821.0, 12.0, 2048.0, new DateTime(2026, 9, 1, 10, 0, 0), "<ShowPlanXML/>"
                }
            });
            reader.Read();

            var entry = QueryStoreReader.MapRow(reader);

            Assert.Equal(101L, entry.QueryId);
            Assert.Equal(501L, entry.PlanId);
            Assert.Equal("SELECT * FROM Orders WHERE CustomerId = 42", entry.QueryText);
            Assert.Equal(15821L, entry.ExecutionCount);
            Assert.Equal(420.0, entry.AvgDurationMs);
            Assert.Equal(180.0, entry.AvgCpuMs);
            Assert.Equal(32821.0, entry.AvgLogicalReads);
            Assert.Equal(12.0, entry.AvgPhysicalReads);
            Assert.Equal(2048.0, entry.AvgMemoryGrantKb);
            Assert.Equal(new DateTime(2026, 9, 1, 10, 0, 0), entry.LastExecutionTimeUtc);
            Assert.Equal("<ShowPlanXML/>", entry.ExecutionPlanXml);
        }

        [Fact]
        public void MapRow_HandlesNullableColumnsBeingNull()
        {
            var reader = new FakeDbDataReader(Columns, new[]
            {
                new object[] { 1L, 1L, "SELECT 1", 1L, 1000.0, 500.0, 8.0, 0.0, null, null, null }
            });
            reader.Read();

            var entry = QueryStoreReader.MapRow(reader);

            Assert.Null(entry.AvgMemoryGrantKb);
            Assert.Null(entry.LastExecutionTimeUtc);
            Assert.Null(entry.ExecutionPlanXml);
        }

        [Fact]
        public async Task ReadAsync_ReturnsOneEntryPerRow()
        {
            var connection = new FakeDbConnection { NextScalarResult = 1 };
            connection.NextReaderResult = new FakeDbDataReader(Columns, new[]
            {
                new object[] { 1L, 1L, "SELECT * FROM A", 5L, 1000.0, 500.0, 100.0, 10.0, 512.0, DateTime.UtcNow, "<plan/>" },
                new object[] { 2L, 2L, "SELECT * FROM B", 10L, 2000.0, 800.0, 200.0, 20.0, 1024.0, DateTime.UtcNow, "<plan/>" }
            });

            var entries = await new QueryStoreReader().ReadAsync(connection);

            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public async Task ReadAsync_ThrowsWhenQueryStoreIsNotEnabled()
        {
            var connection = new FakeDbConnection { NextScalarResult = 0 }; // actual_state = Off

            await Assert.ThrowsAsync<QueryStoreNotEnabledException>(() => new QueryStoreReader().ReadAsync(connection));
        }

        [Fact]
        public async Task ReadAsync_WithNullConnection_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => new QueryStoreReader().ReadAsync(null));
        }
    }
}
