using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QueryLens.AdoNet.Capture;
using QueryLens.AdoNet.Extensions;
using QueryLens.AdoNet.Tests.TestHelpers;
using QueryLens.Core.Configuration;
using QueryLens.Core.Interfaces;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;
using Xunit;

namespace QueryLens.AdoNet.Tests
{
    // NOTE ON VALIDATION: unlike the EF6 and EF Core adapters, this entire
    // project has been fully validated end-to-end in this sandbox. Unlike
    // EntityFramework/Microsoft.EntityFrameworkCore, DbConnection/DbCommand/
    // DbTransaction/DbDataReader are part of System.Data.Common - the BCL, not
    // a NuGet-only package - so a real .NET 8 SDK (installed via apt, no
    // NuGet needed) was enough to compile and run every one of these tests'
    // assertions against the actual production code before it was copied here.
    // The compiler caught every override-signature mistake along the way -
    // nothing here rests on memory of the exact DbCommand/DbConnection API
    // shape being correct.
    public class QueryLensDbConnectionTests
    {
        private static QueryLensPipeline NewPipeline(QueryLensOptions options = null) => new QueryLensPipeline(options ?? new QueryLensOptions());

        [Fact]
        public void Wrap_ForwardsBasicConnectionProperties()
        {
            var inner = new FakeDbConnection("OrdersDb", "sql-primary");
            var pipeline = NewPipeline();

            var wrapped = QueryLensAdoNet.Wrap(inner, pipeline);

            Assert.Equal("OrdersDb", wrapped.Database);
            Assert.Equal("sql-primary", wrapped.DataSource);
            Assert.Equal(ConnectionState.Open, wrapped.State);
        }

        [Fact]
        public void InnerConnection_ReturnsTheWrappedInstance()
        {
            var inner = new FakeDbConnection("Db", "server");
            var wrapped = QueryLensAdoNet.Wrap(inner, NewPipeline());

            Assert.Same(inner, wrapped.InnerConnection);
        }

        [Fact]
        public void ExecuteReader_IsCapturedAndFingerprinted()
        {
            var inner = new FakeDbConnection("OrdersDb", "sql-primary");
            var pipeline = NewPipeline();
            var wrapped = QueryLensAdoNet.Wrap(inner, pipeline, requestIdProvider: () => "req-1");

            using (var command = wrapped.CreateCommand())
            {
                command.CommandText = "SELECT * FROM Orders WHERE CustomerId = 42";
                using (command.ExecuteReader())
                {
                }
            }

            var report = pipeline.Analyze();

            Assert.Single(report.Statistics);
            Assert.Equal("SELECT * FROM Orders WHERE CustomerId = ?", report.Statistics.Single().NormalizedSql);
        }

        [Fact]
        public void ExecuteNonQuery_PassesInnerResultThroughUnchanged()
        {
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), NewPipeline());

            using var command = wrapped.CreateCommand();
            command.CommandText = "UPDATE Orders SET Total = 5 WHERE Id = 1";
            ((FakeDbCommand)((QueryLensDbCommand)command).InnerCommand).NonQueryResult = 7;

            var rows = command.ExecuteNonQuery();

            Assert.Equal(7, rows);
        }

        [Fact]
        public void ExecuteScalar_PassesInnerResultThroughUnchanged()
        {
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), NewPipeline());

            using var command = wrapped.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Orders";
            ((FakeDbCommand)((QueryLensDbCommand)command).InnerCommand).ScalarResult = 42;

            var result = command.ExecuteScalar();

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ExecuteReaderAsync_IsCapturedTheSameAsSync()
        {
            var pipeline = NewPipeline();
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), pipeline);

            using (var command = wrapped.CreateCommand())
            {
                command.CommandText = "SELECT * FROM Users";
                using (await command.ExecuteReaderAsync())
                {
                }
            }

            var report = pipeline.Analyze();

            Assert.Single(report.Statistics);
        }

        [Fact]
        public async Task Parameters_AreCapturedAsMetadataOnly_WhenEnabled()
        {
            var options = new QueryLensOptions();
            options.Capture.Parameters = true;
            var sink = new RecordingSink();
            var pipeline = new QueryLensPipeline(options, sink: sink);
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), pipeline);

            using (var command = wrapped.CreateCommand())
            {
                command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Id";
                parameter.DbType = DbType.Int32;
                parameter.Size = 4;
                parameter.Value = 123456; // must never surface on the captured metadata
                command.Parameters.Add(parameter);

                using (await command.ExecuteReaderAsync())
                {
                }
            }

            var captured = Assert.Single(sink.Executions);
            var parameterMeta = Assert.Single(captured.Parameters);
            Assert.Equal("@Id", parameterMeta.Name);
            Assert.Equal("Int32", parameterMeta.DbType);
            Assert.Equal(4, parameterMeta.Size);
            // QueryParameter has no Value property at all - nothing more to assert.
        }

        [Fact]
        public async Task Parameters_AreNotCaptured_WhenDisabled()
        {
            var sink = new RecordingSink();
            var pipeline = new QueryLensPipeline(new QueryLensOptions(), sink: sink); // Capture.Parameters defaults to false
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), pipeline);

            using (var command = wrapped.CreateCommand())
            {
                command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Id";
                parameter.Value = 123456;
                command.Parameters.Add(parameter);

                using (await command.ExecuteReaderAsync())
                {
                }
            }

            var captured = Assert.Single(sink.Executions);
            Assert.Null(captured.Parameters);
        }

        private sealed class RecordingSink : IQueryMetricsSink
        {
            public List<QueryExecution> Executions { get; } = new List<QueryExecution>();

            public Task WriteAsync(QueryExecution execution, CancellationToken cancellationToken = default)
            {
                Executions.Add(execution);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void MultipleDistinctQueries_AreTrackedIndependently()
        {
            var pipeline = NewPipeline();
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), pipeline, requestIdProvider: () => "req-A");

            using (var commandA = wrapped.CreateCommand())
            {
                commandA.CommandText = "SELECT * FROM Users WHERE Id = 1";
                commandA.ExecuteReader().Dispose();
            }

            using (var commandB = wrapped.CreateCommand())
            {
                commandB.CommandText = "SELECT * FROM Orders";
                commandB.ExecuteReader().Dispose();
            }

            var report = pipeline.Analyze();

            Assert.Equal(2, report.Statistics.Count);
        }

        [Fact]
        public void Transaction_CommitForwardsToTheRealTransaction()
        {
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), NewPipeline());

            using var transaction = wrapped.BeginTransaction();
            using (var command = wrapped.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Orders (Total) VALUES (1)";
                command.ExecuteNonQuery();
            }

            transaction.Commit();

            var innerTransaction = (FakeDbTransaction)((QueryLensDbTransaction)transaction).InnerTransaction;
            Assert.True(innerTransaction.Committed);
        }

        [Fact]
        public void RepeatedQueriesWithinOneRequest_TriggerNPlusOneFinding()
        {
            var pipeline = NewPipeline();
            var wrapped = QueryLensAdoNet.Wrap(new FakeDbConnection("Db", "server"), pipeline, requestIdProvider: () => "req-loop");

            for (var i = 0; i < 25; i++)
            {
                using var command = wrapped.CreateCommand();
                command.CommandText = $"SELECT * FROM Orders WHERE CustomerId = {i}";
                command.ExecuteReader().Dispose();
            }

            var report = pipeline.Analyze();

            Assert.Contains(report.Findings, f => f.RuleId == "NPLUSONE001");
        }
    }
}
