using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;
using QueryLens.SqlServer.QueryStore;
using QueryLens.SqlServer.Tests.TestHelpers;
using Xunit;

namespace QueryLens.SqlServer.Tests
{
    public class QueryLensSqlServerTests
    {
        private sealed class FakeQueryStoreReader : IQueryStoreReader
        {
            private readonly IReadOnlyList<QueryStoreEntry> _entries;

            public FakeQueryStoreReader(IReadOnlyList<QueryStoreEntry> entries) => _entries = entries;

            public Task<IReadOnlyList<QueryStoreEntry>> ReadAsync(
                System.Data.Common.DbConnection connection, QueryStoreReadOptions options = null, CancellationToken cancellationToken = default) =>
                Task.FromResult(_entries);
        }

        [Fact]
        public async Task AnalyzeWithQueryStoreAsync_CombinesApplicationAndQueryStoreFindings()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());
            await pipeline.CaptureAsync(new QueryExecution
            {
                Sql = "SELECT * FROM Orders WHERE Id = 1",
                Duration = TimeSpan.FromMilliseconds(50)
            });

            var fakeReader = new FakeQueryStoreReader(new[]
            {
                new QueryStoreEntry
                {
                    QueryText = "SELECT * FROM Orders WHERE Id = 2", // same shape, different literal
                    ExecutionCount = 100,
                    AvgCpuMs = 999,
                    AvgLogicalReads = 50
                }
            });

            var connection = new FakeDbConnection { NextScalarResult = 1 };

            var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection, reader: fakeReader);

            Assert.NotEmpty(report.CorrelatedQueries);
            Assert.Contains(report.Findings, f => f.RuleId == "HIGHCPU001");
            Assert.True(report.Score.Score < 100);
        }

        [Fact]
        public async Task AnalyzeWithQueryStoreAsync_WithNullPipeline_Throws()
        {
            var connection = new FakeDbConnection { NextScalarResult = 1 };
            var fakeReader = new FakeQueryStoreReader(Array.Empty<QueryStoreEntry>());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                QueryLensSqlServer.AnalyzeWithQueryStoreAsync(null, connection, reader: fakeReader));
        }

        [Fact]
        public async Task AnalyzeWithQueryStoreAsync_WithNullConnection_Throws()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());
            var fakeReader = new FakeQueryStoreReader(Array.Empty<QueryStoreEntry>());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, null, reader: fakeReader));
        }

        private const string PlanXmlWithLargeScan = @"
<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan"">
  <BatchSequence><Batch><Statements><StmtSimple StatementText=""x"" StatementId=""1"">
    <QueryPlan>
      <RelOp NodeId=""0"" PhysicalOp=""Table Scan"" EstimateRows=""50000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""12.5"" Parallel=""false"">
        <TableScan><Object Database=""[MyDb]"" Schema=""[dbo]"" Table=""[Orders]"" /></TableScan>
      </RelOp>
    </QueryPlan>
  </StmtSimple></Statements></Batch></BatchSequence>
</ShowPlanXML>";

        [Fact]
        public async Task AnalyzeWithQueryStoreAsync_ParsesPlanAndCombinesAllThreeFindingSources()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());
            await pipeline.CaptureAsync(new QueryExecution
            {
                Sql = "SELECT * FROM Orders WHERE Id = 1",
                Duration = TimeSpan.FromMilliseconds(50)
            });

            var fakeReader = new FakeQueryStoreReader(new[]
            {
                new QueryStoreEntry
                {
                    QueryText = "SELECT * FROM Orders WHERE Id = 2",
                    ExecutionCount = 100,
                    AvgCpuMs = 999,
                    AvgLogicalReads = 50000,
                    ExecutionPlanXml = PlanXmlWithLargeScan
                }
            });

            var connection = new FakeDbConnection { NextScalarResult = 1 };

            var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection, reader: fakeReader);

            Assert.Contains(report.CorrelatedQueries, q => q.HasApplicationData && q.HasQueryStoreData);
            Assert.Contains(report.CorrelatedQueries, q => q.ParsedExecutionPlan != null);
            Assert.Contains(report.Findings, f => f.RuleId == "HIGHCPU001");
            Assert.Contains(report.Findings, f => f.RuleId == "HIGHREADS001");
            Assert.Contains(report.Findings, f => f.RuleId == "LARGESCAN001");
        }

        [Fact]
        public async Task AnalyzeWithQueryStoreAsync_ExecutionPlanDisabled_SkipsPlanParsingButKeepsQueryStoreFindings()
        {
            var options = new QueryLensOptions();
            options.Analysis.ExecutionPlan = false;
            var pipeline = new QueryLensPipeline(options);

            var fakeReader = new FakeQueryStoreReader(new[]
            {
                new QueryStoreEntry { QueryText = "SELECT 1", ExecutionCount = 1, AvgCpuMs = 999, ExecutionPlanXml = PlanXmlWithLargeScan }
            });

            var connection = new FakeDbConnection { NextScalarResult = 1 };

            var report = await QueryLensSqlServer.AnalyzeWithQueryStoreAsync(pipeline, connection, reader: fakeReader);

            Assert.DoesNotContain(report.Findings, f => f.RuleId == "LARGESCAN001");
            Assert.All(report.CorrelatedQueries, q => Assert.Null(q.ParsedExecutionPlan));
            Assert.Contains(report.Findings, f => f.RuleId == "HIGHCPU001"); // Query Store rules unaffected by this toggle
        }
    }
}
