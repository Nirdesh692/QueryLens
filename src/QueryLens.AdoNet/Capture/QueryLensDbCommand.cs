using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using QueryLens.Core.Capture;
using QueryLens.Core.Pipeline;

namespace QueryLens.AdoNet.Capture
{
    /// <summary>
    /// Wraps a real <see cref="DbCommand"/>, timing every execution and forwarding
    /// everything else unchanged. Created via <see cref="QueryLensDbConnection.CreateCommand"/> -
    /// don't construct directly.
    /// </summary>
    public sealed class QueryLensDbCommand : DbCommand
    {
        private readonly DbCommand _inner;
        private readonly QueryLensPipeline _pipeline;
        private readonly Func<string> _requestIdProvider;
        private readonly string _provider;
        private readonly string _orm;
        private QueryLensDbConnection _connection;
        private QueryLensDbTransaction _transaction;

        internal QueryLensDbCommand(
            DbCommand inner,
            QueryLensPipeline pipeline,
            Func<string> requestIdProvider,
            string provider,
            string orm)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _requestIdProvider = requestIdProvider ?? (() => null);
            _provider = provider;
            _orm = orm;
        }

        /// <summary>The wrapped command - use this if you need the real provider-specific instance.</summary>
        public DbCommand InnerCommand => _inner;

        public override string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
        public override int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
        public override CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
        public override bool DesignTimeVisible { get => _inner.DesignTimeVisible; set => _inner.DesignTimeVisible = value; }
        public override UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

        protected override DbConnection DbConnection
        {
            get => _connection;
            set
            {
                if (value == null)
                {
                    _connection = null;
                    _inner.Connection = null;
                    return;
                }

                if (!(value is QueryLensDbConnection wrapped))
                {
                    throw new InvalidOperationException(
                        "QueryLensDbCommand can only be attached to a QueryLensDbConnection. Create commands via a wrapped connection's CreateCommand().");
                }

                _connection = wrapped;
                _inner.Connection = wrapped.InnerConnection;
            }
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => _transaction;
            set
            {
                if (value == null)
                {
                    _transaction = null;
                    _inner.Transaction = null;
                    return;
                }

                if (!(value is QueryLensDbTransaction wrapped))
                {
                    throw new InvalidOperationException(
                        "QueryLensDbCommand can only enlist in a QueryLensDbTransaction returned from a wrapped connection's BeginTransaction().");
                }

                _transaction = wrapped;
                _inner.Transaction = wrapped.InnerTransaction;
            }
        }

        public override void Cancel() => _inner.Cancel();

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        public override void Prepare() => _inner.Prepare();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var stopwatch = Stopwatch.StartNew();
            var reader = _inner.ExecuteReader(behavior);
            stopwatch.Stop();
            Capture(stopwatch.Elapsed);
            return reader;
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var reader = await _inner.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await CaptureAsync(stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
            return reader;
        }

        public override int ExecuteNonQuery()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = _inner.ExecuteNonQuery();
            stopwatch.Stop();
            Capture(stopwatch.Elapsed, rowsAffected: result);
            return result;
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _inner.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await CaptureAsync(stopwatch.Elapsed, cancellationToken, rowsAffected: result).ConfigureAwait(false);
            return result;
        }

        public override object ExecuteScalar()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = _inner.ExecuteScalar();
            stopwatch.Stop();
            Capture(stopwatch.Elapsed);
            return result;
        }

        public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _inner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await CaptureAsync(stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private void Capture(TimeSpan duration, long? rowsAffected = null)
        {
            var execution = BuildExecution(duration, rowsAffected);
            _pipeline.CaptureAsync(execution).GetAwaiter().GetResult();
        }

        private Task CaptureAsync(TimeSpan duration, CancellationToken cancellationToken, long? rowsAffected = null)
        {
            var execution = BuildExecution(duration, rowsAffected);
            return _pipeline.CaptureAsync(execution, cancellationToken);
        }

        private Core.Models.QueryExecution BuildExecution(TimeSpan duration, long? rowsAffected)
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

            var execution = DbCommandExecutionBuilder.Build(
                _inner,
                duration,
                requestId,
                _pipeline.Options.Capture.Parameters,
                provider: _provider,
                orm: _orm,
                framework: ".NET");

            if (rowsAffected.HasValue)
            {
                execution.RowsAffected = rowsAffected.Value;
            }

            return execution;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
