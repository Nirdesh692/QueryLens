using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;
using QueryLens.Core.Capture;
using QueryLens.Core.Pipeline;

namespace QueryLens.EF6.Interceptors
{
    /// <summary>
    /// EF6 command interceptor. Deliberately thin: it only times each command
    /// (EF6's interception API does not hand you a pre-computed duration the way
    /// EF Core's does) and hands the raw <see cref="DbCommand"/> to
    /// <see cref="DbCommandExecutionBuilder"/> (in QueryLens.Core), which has no
    /// EF6 dependency and is unit tested directly against a fake DbCommand.
    /// </summary>
    internal sealed class QueryLensEf6Interceptor : DbCommandInterceptor
    {
        private readonly QueryLensPipeline _pipeline;
        private readonly Func<string> _requestIdProvider;

        // Keyed by command reference: EF6 passes the same DbCommand instance to
        // the Executing and Executed callbacks for a single execution, and a
        // single DbCommand is not safely used for concurrent executions anyway,
        // so this is a safe correlation key.
        private readonly ConcurrentDictionary<DbCommand, Stopwatch> _timers =
            new ConcurrentDictionary<DbCommand, Stopwatch>();

        public QueryLensEf6Interceptor(QueryLensPipeline pipeline, Func<string> requestIdProvider)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _requestIdProvider = requestIdProvider ?? (() => null);
        }

        public override void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext) => Start(command);

        public override void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext) => Finish(command, interceptionContext.Exception);

        public override void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext) => Start(command);

        public override void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext) => Finish(command, interceptionContext.Exception);

        public override void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext) => Start(command);

        public override void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext) => Finish(command, interceptionContext.Exception);

        private void Start(DbCommand command)
        {
            _timers[command] = Stopwatch.StartNew();
        }

        private void Finish(DbCommand command, Exception exception)
        {
            if (!_timers.TryRemove(command, out var stopwatch))
            {
                return; // defensive: shouldn't happen, but never let telemetry break the app
            }

            stopwatch.Stop();

            if (exception != null)
            {
                // A failed command's timing/shape isn't representative of a normal
                // execution; skip it for this phase rather than risk polluting
                // aggregates with failure noise. Revisit if failure-rate tracking
                // becomes a goal.
                return;
            }

            string requestId;
            try
            {
                requestId = _requestIdProvider();
            }
            catch
            {
                requestId = null;
            }

            var execution = DbCommandExecutionBuilder.Build(
                command,
                stopwatch.Elapsed,
                requestId,
                _pipeline.Options.Capture.Parameters,
                provider: "EF6",
                orm: "EF6",
                framework: ".NETFramework");

            // EF6's interception API is synchronous, and CaptureAsync only performs
            // in-memory fingerprinting/aggregation unless a blocking IQueryMetricsSink
            // is configured - so blocking here is a deliberate, documented trade-off.
            // Keep any sink you register non-blocking (see its interface docs).
            _pipeline.CaptureAsync(execution).GetAwaiter().GetResult();
        }
    }
}
