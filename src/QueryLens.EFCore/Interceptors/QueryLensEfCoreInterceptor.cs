using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryLens.Core.Capture;
using QueryLens.Core.Pipeline;

namespace QueryLens.EFCore.Interceptors
{
    /// <summary>
    /// EF Core command interceptor. Simpler than the EF6 equivalent because EF
    /// Core's diagnostics already compute <c>eventData.Duration</c> for you - no
    /// manual timing needed. Both the sync and async Reader/NonQuery/Scalar
    /// hooks are implemented, since which pair fires depends on whether the
    /// calling code used the sync or async EF Core APIs (ToList() vs
    /// ToListAsync(), etc.) - covering only one would silently miss queries.
    /// </summary>
    public sealed class QueryLensEfCoreInterceptor : DbCommandInterceptor
    {
        private readonly QueryLensPipeline _pipeline;
        private readonly Func<string> _requestIdProvider;

        public QueryLensEfCoreInterceptor(QueryLensPipeline pipeline, Func<string> requestIdProvider = null)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _requestIdProvider = requestIdProvider ?? (() => null);
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Capture(command, eventData);
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            await CaptureAsync(command, eventData, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            Capture(command, eventData);
            return result;
        }

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await CaptureAsync(command, eventData, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public override object ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object result)
        {
            Capture(command, eventData);
            return result;
        }

        public override async ValueTask<object> ScalarExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, object result, CancellationToken cancellationToken = default)
        {
            await CaptureAsync(command, eventData, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private void Capture(DbCommand command, CommandExecutedEventData eventData)
        {
            // The sync interception surface can't await, and CaptureAsync only
            // performs in-memory work (fingerprinting/aggregation) unless a
            // blocking IQueryMetricsSink is configured - see the EF6 adapter's
            // notes for the same trade-off. Keep any sink you register non-blocking.
            var execution = BuildExecution(command, eventData);
            _pipeline.CaptureAsync(execution).GetAwaiter().GetResult();
        }

        private Task CaptureAsync(DbCommand command, CommandExecutedEventData eventData, CancellationToken cancellationToken)
        {
            var execution = BuildExecution(command, eventData);
            return _pipeline.CaptureAsync(execution, cancellationToken);
        }

        private Core.Models.QueryExecution BuildExecution(DbCommand command, CommandExecutedEventData eventData)
        {
            string requestId;
            try
            {
                requestId = _requestIdProvider();
            }
            catch
            {
                requestId = null;
            }

            return DbCommandExecutionBuilder.Build(
                command,
                eventData.Duration,
                requestId,
                _pipeline.Options.Capture.Parameters,
                provider: "EFCore",
                orm: "EFCore",
                framework: ".NET");
        }
    }
}
