using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using QueryLens.Core.Pipeline;

namespace QueryLens.AdoNet.Capture
{
    /// <summary>
    /// Wraps a real <see cref="DbConnection"/> so every command it creates is
    /// automatically timed and captured - including commands issued by Dapper,
    /// since Dapper executes through whatever <see cref="System.Data.IDbConnection"/>
    /// you hand it. Create one via <see cref="Extensions.QueryLensAdoNet.Wrap"/>.
    /// </summary>
    public sealed class QueryLensDbConnection : DbConnection
    {
        private readonly DbConnection _inner;
        private readonly QueryLensPipeline _pipeline;
        private readonly Func<string> _requestIdProvider;
        private readonly string _provider;
        private readonly string _orm;

        internal QueryLensDbConnection(
            DbConnection inner,
            QueryLensPipeline pipeline,
            Func<string> requestIdProvider,
            string provider,
            string orm)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _requestIdProvider = requestIdProvider;
            _provider = provider;
            _orm = orm;
        }

        /// <summary>The wrapped connection - use this if you need the real provider-specific instance.</summary>
        public DbConnection InnerConnection => _inner;

        public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new QueryLensDbTransaction(_inner.BeginTransaction(isolationLevel), this);

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        protected override DbCommand CreateDbCommand() =>
            new QueryLensDbCommand(_inner.CreateCommand(), _pipeline, _requestIdProvider, _provider, _orm) { Connection = this };

        public override void Open() => _inner.Open();

        public override Task OpenAsync(CancellationToken cancellationToken) => _inner.OpenAsync(cancellationToken);

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
