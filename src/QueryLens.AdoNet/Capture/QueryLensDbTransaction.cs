using System;
using System.Data;
using System.Data.Common;

namespace QueryLens.AdoNet.Capture
{
    /// <summary>
    /// Wraps a real <see cref="DbTransaction"/> so <see cref="QueryLensDbCommand"/> can
    /// enlist in it while still forwarding to the real transaction underneath.
    /// Created via <see cref="QueryLensDbConnection.BeginTransaction()"/> - don't
    /// construct directly.
    /// </summary>
    public sealed class QueryLensDbTransaction : DbTransaction
    {
        private readonly DbTransaction _inner;
        private readonly QueryLensDbConnection _connection;

        internal QueryLensDbTransaction(DbTransaction inner, QueryLensDbConnection connection)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>The wrapped transaction - use this if you need the real provider-specific instance.</summary>
        public DbTransaction InnerTransaction => _inner;

        public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

        protected override DbConnection DbConnection => _connection;

        public override void Commit() => _inner.Commit();

        public override void Rollback() => _inner.Rollback();

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
