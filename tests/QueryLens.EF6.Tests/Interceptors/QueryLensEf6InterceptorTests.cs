using System;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EF6.Interceptors;
using QueryLens.EF6.Tests.TestHelpers;
using Xunit;

namespace QueryLens.EF6.Tests.Interceptors
{
    // NOTE ON VALIDATION: unlike DbCommandExecutionBuilderTests in
    // QueryLens.Core.Tests (which was run end-to-end in a sandbox with no
    // EF6/NuGet access, using the same Fake* helpers copied here), this file
    // exercises QueryLensEf6Interceptor directly against real EF6 interception
    // types (DbCommandInterceptionContext<T>) and has NOT been compiled or run
    // anywhere - this sandbox cannot restore the EntityFramework package. The
    // interceptor is a thin wrapper (start a Stopwatch on *Executing, hand off
    // to the validated DbCommandExecutionBuilder on *Executed), so the risk
    // surface here is small, but if EF6's exact API shape differs from what's
    // assumed below (e.g. DbCommandInterceptionContext<T> constructor
    // visibility), this file may need a small fix on a machine with real EF6 -
    // please build/run it there before relying on it.
    public class QueryLensEf6InterceptorTests
    {
        [Fact]
        public void ReaderExecuted_CapturesDurationAndForwardsToPipeline()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());
            var interceptor = new QueryLensEf6Interceptor(pipeline, requestIdProvider: () => "req-42");

            var command = new FakeDbCommand { CommandText = "SELECT * FROM Users WHERE Id = 1" };
            var context = new DbCommandInterceptionContext<System.Data.Common.DbDataReader>();

            interceptor.ReaderExecuting(command, context);
            System.Threading.Thread.Sleep(5); // ensure a non-zero, measurable duration
            interceptor.ReaderExecuted(command, context);

            var report = pipeline.Analyze();

            Assert.Single(report.Statistics);
            var stats = report.Statistics.Single();
            Assert.Equal(1, stats.ExecutionCount);
            Assert.True(stats.AverageDurationMs > 0);
        }

        [Fact]
        public void MultipleCommands_AreTrackedIndependently()
        {
            var pipeline = new QueryLensPipeline(new QueryLensOptions());
            var interceptor = new QueryLensEf6Interceptor(pipeline, requestIdProvider: () => "req-1");

            var commandA = new FakeDbCommand { CommandText = "SELECT * FROM Users WHERE Id = 1" };
            var commandB = new FakeDbCommand { CommandText = "SELECT * FROM Orders WHERE Id = 1" };
            var contextA = new DbCommandInterceptionContext<System.Data.Common.DbDataReader>();
            var contextB = new DbCommandInterceptionContext<System.Data.Common.DbDataReader>();

            interceptor.ReaderExecuting(commandA, contextA);
            interceptor.ReaderExecuting(commandB, contextB);
            interceptor.ReaderExecuted(commandA, contextA);
            interceptor.ReaderExecuted(commandB, contextB);

            var report = pipeline.Analyze();

            Assert.Equal(2, report.Statistics.Count);
        }
    }
}
